using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Services;
using MakinaEditor.Core;

namespace MakinaEditor.ViewModels;

public class RecentProjectItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}

public class EditorSettings
{
    public List<RecentProjectItem> RecentProjects { get; set; } = new();
}

public class ProjectViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    private string _projectName = "Untitled Project";
    public string ProjectName
    {
        get => _projectName;
        set => this.RaiseAndSetIfChanged(ref _projectName, value);
    }

    private string? _currentProjectPath;
    public string? CurrentProjectPath
    {
        get => _currentProjectPath;
        private set => this.RaiseAndSetIfChanged(ref _currentProjectPath, value);
    }

    public ObservableCollection<RecentProjectItem> RecentProjects { get; } = new();
    public ObservableCollection<VariableDefinition> ProjectVariables { get; } = new();

    public ProjectViewModel(MainWindowViewModel main)
    {
        _main = main;
        LoadSettings();
    }

    public async Task OpenProjectFolder(object? arg)
    {
        string? path = null;
        if (arg is RecentProjectItem item) path = item.Path;
        else if (arg is string s) path = s;

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            _main.StatusText = "유효하지 않은 프로젝트 경로입니다.";
            return;
        }

        CurrentProjectPath = path;

        // 1. 프로젝트 파일(.makina) 탐색
        string? projectFilePath = Directory.GetFiles(path, "*.makina").FirstOrDefault();
        
        if (projectFilePath != null)
        {
            try
            {
                var json = await File.ReadAllTextAsync(projectFilePath);
                var meta = JsonSerializer.Deserialize<ProjectMetadata>(json);
                ProjectName = meta?.Name ?? Path.GetFileName(path);

                _main.Assets.ProjectResources.Clear();
                if (meta?.Resources != null)
                {
                    foreach (var r in meta.Resources)
                    {
                        _main.Assets.ProjectResources.Add(r);
                    }
                }
                _main.Assets.UpdateRegisteredResourcesList();

                ProjectVariables.Clear();
                if (meta?.Variables != null)
                {
                    foreach (var v in meta.Variables)
                    {
                        ProjectVariables.Add(v);
                    }
                }
            }
            catch (Exception ex)
            {
                ProjectName = Path.GetFileName(path);
                _main.StatusText = $"프로젝트 파일 읽기 실패: {ex.Message}";
            }
        }
        else
        {
            // 파일이 없으면 폴더명으로 새 프로젝트 생성 및 파일 생성
            ProjectName = Path.GetFileName(path);
            var newMeta = new ProjectMetadata { Name = ProjectName };
            string savePath = Path.Combine(path, $"{ProjectName}.makina");
            try
            {
                await File.WriteAllTextAsync(savePath, JsonSerializer.Serialize(newMeta));
            }
            catch (Exception ex)
            {
                _main.StatusText = $"프로젝트 메타 생성 실패: {ex.Message}";
            }
        }

        // 2. 에셋 브라우저 갱신 및 라벨링
        await _main.Assets.RefreshAssets();

        // 3. 시나리오 폴더 스캔 및 로드
        _main.Scenario.Scenarios.Clear();
        string scenarioFolder = Path.Combine(path, "Scenarios");
        if (!Directory.Exists(scenarioFolder))
        {
            Directory.CreateDirectory(scenarioFolder);
        }

        // 3-1. 디렉터리 기준 수집
        var dirs = Directory.GetDirectories(scenarioFolder);
        foreach (var dir in dirs)
        {
            _main.Scenario.Scenarios.Add(Path.GetFileName(dir));
        }

        // 3-2. 레거시 단일 JSON 파일 기준 수집 (하위 호환 마이그레이션 대상)
        var scenarioFiles = Directory.GetFiles(scenarioFolder, "*.json");
        foreach (var file in scenarioFiles)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!_main.Scenario.Scenarios.Contains(name))
            {
                _main.Scenario.Scenarios.Add(name);
            }
        }

        if (_main.Scenario.Scenarios.Count == 0)
        {
            // 기본 시나리오 생성
            string scenarioName = "intro";
            string scenarioDir = Path.Combine(scenarioFolder, scenarioName);
            Directory.CreateDirectory(scenarioDir);
            Directory.CreateDirectory(Path.Combine(scenarioDir, "Flows"));

            var defaultScenario = new MakinaScenario(scenarioName);
            var startText = new TextCommand { Speaker = "지우", TextContent = "마키나 에디터에 오신 것을 환영합니다!" };
            
            // 시나리오 메타데이터 저장 (UserFlows는 직렬화에서 배제됨)
            string introPath = Path.Combine(scenarioDir, "intro.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(introPath, JsonSerializer.Serialize(defaultScenario, options));

            // 기본 플로우 저장
            string flowPath = Path.Combine(scenarioDir, "Flows", "flow_default.json");
            var defaultFlow = new ObservableCollection<FlowCommand> { startText };
            await File.WriteAllTextAsync(flowPath, JsonSerializer.Serialize(defaultFlow, options));

            _main.Scenario.Scenarios.Add(scenarioName);
        }

        // 4. 첫 시나리오 로딩
        _main.IsProjectLoaded = true;
        _main.Scenario.SelectedScenarioName = _main.Scenario.Scenarios.FirstOrDefault();

        _main.StatusText = $"프로젝트 '{ProjectName}'를 성공적으로 로드했습니다.";
        
        // 최근 리스트 업데이트 및 설정 저장
        UpdateRecentProjects(path);
        SaveSettings();
    }

    public async Task CreateNewProject(string parentPath, string projectName)
    {
        string projectPath = Path.Combine(parentPath, projectName);

        try
        {
            Directory.CreateDirectory(projectPath);
            Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
            Directory.CreateDirectory(Path.Combine(projectPath, "Scenarios"));
            Directory.CreateDirectory(Path.Combine(projectPath, "Scripts"));

            // 기본 시나리오 파일 쓰기
            var defaultScenario = new MakinaScenario("intro");
            var startText = new TextCommand { Speaker = "지우", TextContent = "새 프로젝트의 첫 대사입니다. 이곳을 더블클릭하여 수정하세요!" };
            defaultScenario.UserFlows["flow_default"] = new ObservableCollection<FlowCommand> { startText };
            
            string introPath = Path.Combine(projectPath, "Scenarios", "intro.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(introPath, JsonSerializer.Serialize(defaultScenario, options));

            // .makina 파일 생성
            var meta = new ProjectMetadata { Name = projectName, Version = "1.0.0" };
            string metaPath = Path.Combine(projectPath, $"{projectName}.makina");
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta, options));

            _main.StatusText = $"새 프로젝트 '{projectName}'를 생성했습니다.";
            await OpenProjectFolder(projectPath);
        }
        catch (Exception ex)
        {
            _main.StatusText = $"프로젝트 생성 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Exception in CreateNewProject: {ex}");
        }
    }

    public async Task SaveProject()
    {
        if (string.IsNullOrEmpty(CurrentProjectPath) || _main.Scenario.ActiveScenario == null)
        {
            _main.StatusText = "저장할 활성 프로젝트가 없습니다.";
            return;
        }

        try
        {
            // 1. 활성 Flow 상태 동기화 백업
            if (!string.IsNullOrEmpty(_main.Flow.SelectedFlowName))
            {
                _main.Scenario.ActiveScenario.UserFlows[_main.Flow.SelectedFlowName] = new ObservableCollection<FlowCommand>(_main.Flow.ActiveUserFlow);
            }

            string scenarioDir = Path.Combine(CurrentProjectPath, "Scenarios", _main.Scenario.ActiveScenario.ScenarioName);
            Directory.CreateDirectory(scenarioDir);

            string flowsDir = Path.Combine(scenarioDir, "Flows");
            Directory.CreateDirectory(flowsDir);

            // 2. 시나리오 메타데이터 저장 (UserFlows는 JsonIgnore되어 제외됨)
            string scenarioFilePath = Path.Combine(scenarioDir, $"{_main.Scenario.ActiveScenario.ScenarioName}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(scenarioFilePath, JsonSerializer.Serialize(_main.Scenario.ActiveScenario, options));

            // 3. 개별 Flow 저장
            foreach (var kvp in _main.Scenario.ActiveScenario.UserFlows)
            {
                string flowFilePath = Path.Combine(flowsDir, $"{kvp.Key}.json");
                await File.WriteAllTextAsync(flowFilePath, JsonSerializer.Serialize(kvp.Value, options));
            }

            // 4. 물리적으로 존재하나 메모리에서 삭제된 Flow 파일 정리 (Garbage Collection)
            if (Directory.Exists(flowsDir))
            {
                var existingFiles = Directory.GetFiles(flowsDir, "*.json");
                foreach (var file in existingFiles)
                {
                    string fId = Path.GetFileNameWithoutExtension(file);
                    if (!_main.Scenario.ActiveScenario.UserFlows.ContainsKey(fId))
                    {
                        File.Delete(file);
                    }
                }
            }

            // 5. 프로젝트 메타데이터 저장
            string? projectFilePath = Directory.GetFiles(CurrentProjectPath, "*.makina").FirstOrDefault();
            if (string.IsNullOrEmpty(projectFilePath))
            {
                projectFilePath = Path.Combine(CurrentProjectPath, $"{ProjectName}.makina");
            }
            var meta = new ProjectMetadata 
            { 
                Name = ProjectName, 
                Version = "1.0.0",
                Resources = _main.Assets.ProjectResources.ToList(),
                Variables = new ObservableCollection<VariableDefinition>(ProjectVariables)
            };
            await File.WriteAllTextAsync(projectFilePath, JsonSerializer.Serialize(meta, options));

            _main.StatusText = $"프로젝트를 저장했습니다. ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            _main.StatusText = $"프로젝트 저장 실패: {ex.Message}";
        }
    }

    public void CloseProject()
    {
        _main.UndoRedo.Clear();
        _main.IsProjectLoaded = false;
        CurrentProjectPath = null;
        ProjectName = "Untitled Project";
        _main.Flow.ActiveUserFlow.Clear();
        _main.Flow.AvailableFlows.Clear();
        _main.Scenario.Scenarios.Clear();
        _main.Assets.AssetRegistry.Clear();
        _main.Assets.ProjectAssets.Clear();
        _main.Assets.ProjectResources.Clear();
        ProjectVariables.Clear();
        _main.Assets.RegisteredBgs.Clear();
        _main.Assets.RegisteredCharacters.Clear();
        _main.Assets.NewResourceIdInput = "";
        _main.Assets.NewResourcePathInput = "";
        _main.Assets.NewPoseNameInput = "";
        _main.Assets.NewPosePathInput = "";
        _main.Scenario.SelectedScenarioName = null;
        _main.Flow.SelectedFlowName = null;
        _main.Assets.SelectedAssetNode = null;
        _main.Assets.SelectedResource = null;
        _main.StatusText = "프로젝트가 닫혔습니다.";
    }

    public void RunProject()
    {
        _main.StatusText = "프로젝트 실행 기능은 현재 개발 중입니다. (미래 컴파일 파이프라인 연동 예정)";
    }

    private void UpdateRecentProjects(string path)
    {
        var existing = RecentProjects.FirstOrDefault(x => x.Path == path);
        if (existing != null) RecentProjects.Remove(existing);
        
        RecentProjects.Insert(0, new RecentProjectItem { 
            Name = Path.GetFileName(path), 
            Path = path 
        });

        while (RecentProjects.Count > 10) RecentProjects.RemoveAt(10);
    }

    private string GetSettingsFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "MakinaEditor");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        return Path.Combine(folder, "settings.json");
    }

    public void SaveSettings()
    {
        try
        {
            var settings = new EditorSettings();
            settings.RecentProjects.AddRange(RecentProjects);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetSettingsFilePath(), json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"설정 저장 실패: {ex.Message}");
        }
    }

    public void LoadSettings()
    {
        try
        {
            string path = GetSettingsFilePath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<EditorSettings>(json);
                if (settings != null)
                {
                    RecentProjects.Clear();
                    foreach (var item in settings.RecentProjects)
                    {
                        RecentProjects.Add(item);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"설정 로드 실패: {ex.Message}");
        }
    }
}
