using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Services;
using MakinaEditor.Core;
using System.Text.Json;
using System.Linq;
using Avalonia.Threading;

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

public class MainWindowViewModel : ViewModelBase
{
    // --- [런처 상태 제어] ---
    private bool _isProjectLoaded = false;
    public bool IsProjectLoaded 
    { 
        get => _isProjectLoaded; 
        set => this.RaiseAndSetIfChanged(ref _isProjectLoaded, value); 
    }

    public ObservableCollection<RecentProjectItem> RecentProjects { get; } = new();
    
    // 시나리오 리스트 추가
    public ObservableCollection<string> Scenarios { get; } = new();

    private string? _selectedScenarioName;
    public string? SelectedScenarioName
    {
        get => _selectedScenarioName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedScenarioName, value);
            if (!string.IsNullOrEmpty(value))
            {
                _ = LoadScenario(value);
            }
        }
    }

    // --- [1. 프로젝트 및 내비게이션 상태] ---
    private string _projectName = "Untitled Project";
    public string ProjectName { get => _projectName; set => this.RaiseAndSetIfChanged(ref _projectName, value); }

    private EditorMode _currentMode = EditorMode.Flow_Timeline;
    public EditorMode CurrentMode
    {
        get => _currentMode;
        set {
            this.RaiseAndSetIfChanged(ref _currentMode, value);
            this.RaisePropertyChanged(nameof(IsUiDesignMode));
            this.RaisePropertyChanged(nameof(IsScenarioMode));
            this.RaisePropertyChanged(nameof(IsFlowMode));
        }
    }

    public bool IsUiDesignMode => CurrentMode == EditorMode.UI_Design;
    public bool IsScenarioMode => CurrentMode == EditorMode.Scenario_Graph;
    public bool IsFlowMode => CurrentMode == EditorMode.Flow_Timeline;

    // --- [2. 데이터 컨텍스트] ---
    public ObservableCollection<AssetNode> ProjectAssets { get; } = new();
    public ObservableCollection<FlowCommand> ActiveUserFlow { get; } = new();
    
    private MakinaScenario _activeScenario = new("New Project");
    private string? _currentProjectPath;

    // --- [3. 프리뷰 상태 속성들] ---
    private string? _previewBg;
    public string? PreviewBg { get => _previewBg; set => this.RaiseAndSetIfChanged(ref _previewBg, value); }

    private string _previewSpeaker = "";
    public string PreviewSpeaker { get => _previewSpeaker; set => this.RaiseAndSetIfChanged(ref _previewSpeaker, value); }

    private string _previewText = "";
    public string PreviewText { get => _previewText; set => this.RaiseAndSetIfChanged(ref _previewText, value); }

    private string? _previewLeftChar;
    public string? PreviewLeftChar { get => _previewLeftChar; set => this.RaiseAndSetIfChanged(ref _previewLeftChar, value); }

    private string? _previewCenterChar;
    public string? PreviewCenterChar { get => _previewCenterChar; set => this.RaiseAndSetIfChanged(ref _previewCenterChar, value); }

    private string? _previewRightChar;
    public string? PreviewRightChar { get => _previewRightChar; set => this.RaiseAndSetIfChanged(ref _previewRightChar, value); }

    private string? _previewBgm;
    public string? PreviewBgm { get => _previewBgm; set => this.RaiseAndSetIfChanged(ref _previewBgm, value); }

    private string? _previewShader;
    public string? PreviewShader { get => _previewShader; set => this.RaiseAndSetIfChanged(ref _previewShader, value); }

    private string _statusText = "준비됨";
    public string StatusText { get => _statusText; set => this.RaiseAndSetIfChanged(ref _statusText, value); }

    private FlowCommand? _selectedCommand;
    public FlowCommand? SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCommand, value);
            UpdatePreviewState();
        }
    }

    private readonly DispatcherTimer _previewTimer;
    public bool IsPreviewPlaying => _previewTimer.IsEnabled;

    public MainWindowViewModel()
    {
        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _previewTimer.Tick += PreviewTimer_Tick;

        LoadSettings(); // 최근 프로젝트 및 설정 로드
        
        // 💡 자동 실행 기능: 최근 항목이 있다면 즉시 로드 시도
        if (RecentProjects.Count > 0)
        {
            var lastPath = RecentProjects[0].Path;
            if (Directory.Exists(lastPath))
            {
                _ = OpenProjectFolder(lastPath); // 비동기 실행
            }
        }
    }

    // --- [4. 에셋 및 프로젝트 로직] ---
    public async Task OpenProjectFolder(object? arg)
    {
        string? path = null;
        if (arg is RecentProjectItem item) path = item.Path;
        else if (arg is string s) path = s;

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            StatusText = "유효하지 않은 프로젝트 경로입니다.";
            return;
        }

        _currentProjectPath = path;

        // 1. 프로젝트 파일(.makina) 탐색
        string? projectFilePath = Directory.GetFiles(path, "*.makina").FirstOrDefault();
        
        if (projectFilePath != null)
        {
            try
            {
                var json = await File.ReadAllTextAsync(projectFilePath);
                var meta = JsonSerializer.Deserialize<ProjectMetadata>(json);
                ProjectName = meta?.Name ?? Path.GetFileName(path);
            }
            catch (Exception ex)
            {
                ProjectName = Path.GetFileName(path);
                StatusText = $"프로젝트 파일 읽기 실패: {ex.Message}";
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
                StatusText = $"프로젝트 메타 생성 실패: {ex.Message}";
            }
        }

        // 2. 에셋 브라우저 갱신 (비동기 최적화 스캔 사용)
        ProjectAssets.Clear();
        var rootNode = new AssetNode(path) { Name = ProjectName };
        await AssetService.ScanDirectoryAsync(path, rootNode.Children);
        ProjectAssets.Add(rootNode);

        // 3. 시나리오 폴더 스캔 및 로드
        Scenarios.Clear();
        string scenarioFolder = Path.Combine(path, "Scenarios");
        if (!Directory.Exists(scenarioFolder))
        {
            Directory.CreateDirectory(scenarioFolder);
        }

        var scenarioFiles = Directory.GetFiles(scenarioFolder, "*.json");
        foreach (var file in scenarioFiles)
        {
            Scenarios.Add(Path.GetFileNameWithoutExtension(file));
        }

        if (Scenarios.Count == 0)
        {
            // 기본 시나리오 생성
            var defaultScenario = new MakinaScenario("intro");
            var startText = new TextCommand { Speaker = "지우", TextContent = "마키나 에디터에 오신 것을 환영합니다!" };
            defaultScenario.UserFlows["flow_default"] = new ObservableCollection<FlowCommand> { startText };
            string introPath = Path.Combine(scenarioFolder, "intro.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(introPath, JsonSerializer.Serialize(defaultScenario, options));
            Scenarios.Add("intro");
        }

        // 4. 첫 시나리오 로딩
        IsProjectLoaded = true;
        SelectedScenarioName = Scenarios.FirstOrDefault();

        StatusText = $"프로젝트 '{ProjectName}'를 성공적으로 로드했습니다.";
        
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

            StatusText = $"새 프로젝트 '{projectName}'를 생성했습니다.";
            await OpenProjectFolder(projectPath);
        }
        catch (Exception ex)
        {
            StatusText = $"프로젝트 생성 실패: {ex.Message}";
        }
    }

    public async Task LoadScenario(string scenarioName)
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return;

        string filePath = Path.Combine(_currentProjectPath, "Scenarios", $"{scenarioName}.json");
        if (!File.Exists(filePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var scenario = JsonSerializer.Deserialize<MakinaScenario>(json);
            if (scenario != null)
            {
                _activeScenario = scenario;
                _activeScenario.ScenarioName = scenarioName;

                ActiveUserFlow.Clear();
                if (scenario.UserFlows.TryGetValue("flow_default", out var flow))
                {
                    foreach (var cmd in flow)
                    {
                        ActiveUserFlow.Add(cmd);
                    }
                }
                else
                {
                    var defaultCmd = new TextCommand { Speaker = "시스템", TextContent = "새 플로우가 생성되었습니다." };
                    ActiveUserFlow.Add(defaultCmd);
                    _activeScenario.UserFlows["flow_default"] = new ObservableCollection<FlowCommand> { defaultCmd };
                }

                StatusText = $"시나리오 '{scenarioName}' 로드됨.";
                SelectedCommand = ActiveUserFlow.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"시나리오 로드 중 오류 발생: {ex.Message}";
        }
    }

    public async Task SaveProject()
    {
        if (string.IsNullOrEmpty(_currentProjectPath) || _activeScenario == null)
        {
            StatusText = "저장할 활성 프로젝트가 없습니다.";
            return;
        }

        try
        {
            // 1. 활성 시나리오 데이터 저장
            _activeScenario.UserFlows["flow_default"] = new ObservableCollection<FlowCommand>(ActiveUserFlow);
            string scenarioFilePath = Path.Combine(_currentProjectPath, "Scenarios", $"{_activeScenario.ScenarioName}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(scenarioFilePath, JsonSerializer.Serialize(_activeScenario, options));

            // 2. 프로젝트 메타데이터 저장
            string? projectFilePath = Directory.GetFiles(_currentProjectPath, "*.makina").FirstOrDefault();
            if (string.IsNullOrEmpty(projectFilePath))
            {
                projectFilePath = Path.Combine(_currentProjectPath, $"{ProjectName}.makina");
            }
            var meta = new ProjectMetadata { Name = ProjectName, Version = "1.0.0" };
            await File.WriteAllTextAsync(projectFilePath, JsonSerializer.Serialize(meta, options));

            StatusText = $"프로젝트를 저장했습니다. ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            StatusText = $"프로젝트 저장 실패: {ex.Message}";
        }
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

    private void SaveSettings()
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

    private void LoadSettings()
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

    // --- [5. 플로우 커맨드 로직] ---
    public void AddTextCommand(FlowCommand? target) => InsertOrAdd(target, new TextCommand { Speaker = "지우", TextContent = "새 대사..." });
    public void AddBgCommand(FlowCommand? target) => InsertOrAdd(target, new BgCommand { AssetId = "default_bg" });
    public void AddCharCommand(FlowCommand? target) => InsertOrAdd(target, new ShowCharCommand { CharacterId = "지우", Pose = "기본", Position = "Center" });
    public void AddBgmCommand(FlowCommand? target) => InsertOrAdd(target, new PlayBgmCommand { AssetId = "bgm_sunny_day", Volume = 1.0f });
    public void RemoveCommand(FlowCommand target)
    {
        int index = ActiveUserFlow.IndexOf(target);
        ActiveUserFlow.Remove(target);
        if (SelectedCommand == target)
        {
            if (ActiveUserFlow.Count > 0)
            {
                SelectedCommand = ActiveUserFlow[Math.Max(0, index - 1)];
            }
            else
            {
                SelectedCommand = null;
            }
        }
        else
        {
            UpdatePreviewState();
        }
    }

    private void InsertOrAdd(FlowCommand? target, FlowCommand newNode)
    {
        if (target == null) ActiveUserFlow.Insert(0, newNode);
        else ActiveUserFlow.Insert(ActiveUserFlow.IndexOf(target) + 1, newNode);
        SelectedCommand = newNode; // 자동 선택하여 프리뷰 동기화
    }

    // --- [6. 프리뷰 및 조작 시스템] ---
    public void UpdatePreviewState()
    {
        // 프리뷰 모델 상태 초기화
        PreviewBg = null;
        PreviewSpeaker = "";
        PreviewText = "";
        PreviewLeftChar = null;
        PreviewCenterChar = null;
        PreviewRightChar = null;
        PreviewBgm = null;
        PreviewShader = null;

        if (ActiveUserFlow.Count == 0) return;

        int targetIndex = SelectedCommand != null ? ActiveUserFlow.IndexOf(SelectedCommand) : ActiveUserFlow.Count - 1;
        if (targetIndex < 0) targetIndex = ActiveUserFlow.Count - 1;

        for (int i = 0; i <= targetIndex; i++)
        {
            var cmd = ActiveUserFlow[i];
            if (cmd is BgCommand bg)
            {
                PreviewBg = bg.AssetId;
            }
            else if (cmd is ShowCharCommand ch)
            {
                string charDisplay = string.IsNullOrEmpty(ch.Pose) ? (ch.CharacterId ?? "") : $"{ch.CharacterId} ({ch.Pose})";
                if (ch.Position == "Left")
                {
                    PreviewLeftChar = (ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId)) ? null : charDisplay;
                }
                else if (ch.Position == "Center")
                {
                    PreviewCenterChar = (ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId)) ? null : charDisplay;
                }
                else if (ch.Position == "Right")
                {
                    PreviewRightChar = (ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId)) ? null : charDisplay;
                }
                else if (ch.Position == "Hide" || ch.CharacterId == "Hide" || ch.Pose == "Hide")
                {
                    if (PreviewLeftChar != null && PreviewLeftChar.StartsWith(ch.CharacterId ?? "---")) PreviewLeftChar = null;
                    if (PreviewCenterChar != null && PreviewCenterChar.StartsWith(ch.CharacterId ?? "---")) PreviewCenterChar = null;
                    if (PreviewRightChar != null && PreviewRightChar.StartsWith(ch.CharacterId ?? "---")) PreviewRightChar = null;
                }
            }
            else if (cmd is PlayBgmCommand bgm)
            {
                PreviewBgm = bgm.AssetId;
            }
            else if (cmd is ShaderCommand sh)
            {
                PreviewShader = $"{sh.ShaderId} (강도: {sh.Intensity})";
            }
            else if (cmd is TextCommand txt)
            {
                PreviewSpeaker = txt.Speaker ?? "";
                PreviewText = txt.TextContent ?? "";
            }
        }
    }

    public void SelectPrevCommand()
    {
        if (ActiveUserFlow.Count == 0) return;
        if (SelectedCommand == null)
        {
            SelectedCommand = ActiveUserFlow.LastOrDefault();
            return;
        }
        int index = ActiveUserFlow.IndexOf(SelectedCommand);
        if (index > 0)
        {
            SelectedCommand = ActiveUserFlow[index - 1];
        }
    }

    public void SelectNextCommand()
    {
        if (ActiveUserFlow.Count == 0) return;
        if (SelectedCommand == null)
        {
            SelectedCommand = ActiveUserFlow.FirstOrDefault();
            return;
        }
        int index = ActiveUserFlow.IndexOf(SelectedCommand);
        if (index < ActiveUserFlow.Count - 1)
        {
            SelectedCommand = ActiveUserFlow[index + 1];
        }
    }

    public void TogglePlayPreview()
    {
        if (_previewTimer.IsEnabled)
        {
            _previewTimer.Stop();
            StatusText = "프리뷰 자동 재생 일시 정지.";
        }
        else
        {
            if (ActiveUserFlow.Count == 0) return;
            if (SelectedCommand == null || ActiveUserFlow.IndexOf(SelectedCommand) >= ActiveUserFlow.Count - 1)
            {
                SelectedCommand = ActiveUserFlow.FirstOrDefault();
            }
            _previewTimer.Start();
            StatusText = "프리뷰 자동 재생 중...";
        }
        this.RaisePropertyChanged(nameof(IsPreviewPlaying));
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (SelectedCommand == null)
        {
            SelectedCommand = ActiveUserFlow.FirstOrDefault();
            return;
        }

        int currentIndex = ActiveUserFlow.IndexOf(SelectedCommand);
        if (currentIndex < ActiveUserFlow.Count - 1)
        {
            SelectedCommand = ActiveUserFlow[currentIndex + 1];
        }
        else
        {
            _previewTimer.Stop();
            this.RaisePropertyChanged(nameof(IsPreviewPlaying));
            StatusText = "프리뷰 자동 재생 완료.";
        }
    }
}