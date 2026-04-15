using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Services;
using MakinaEditor.Core;
using System.Text.Json;
using System.Linq;

namespace MakinaEditor.ViewModels;

public class RecentProjectItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
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

    // --- [1. 프로젝트 및 내비게이션 상태] ---
    private string _projectName = "Untitled Project";
    public string ProjectName { get => _projectName; set => this.RaiseAndSetIfChanged(ref _projectName, value); }

    private EditorMode _currentMode = EditorMode.Flow_Timeline;
    public EditorMode CurrentMode
    {
        get => _currentMode;
        set {
            this.RaiseAndSetIfChanged(ref _currentMode, value);
            this.RaisePropertyChanged(nameof(IsUiDesignMode)); // 👈 XAML 에러 해결 포인트
            this.RaisePropertyChanged(nameof(IsScenarioMode));
            this.RaisePropertyChanged(nameof(IsFlowMode));
        }
    }

    // XAML 바인딩용 속성들 (에러 났던 이름들로 깔끔하게 정리)
    public bool IsUiDesignMode => CurrentMode == EditorMode.UI_Design;
    public bool IsScenarioMode => CurrentMode == EditorMode.Scenario_Graph;
    public bool IsFlowMode => CurrentMode == EditorMode.Flow_Timeline;

    // --- [2. 데이터 컨텍스트] ---
    public ObservableCollection<AssetNode> ProjectAssets { get; } = new();
    public ObservableCollection<FlowCommand> ActiveUserFlow { get; } = new();
    
    private MakinaScenario _activeScenario = new("New Project");

    public MainWindowViewModel()
    {
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

    // --- [3. 에셋 및 프로젝트 로직] ---
    public async Task OpenProjectFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        // 1. 프로젝트 파일(.makina) 탐색
        string projectFilePath = Directory.GetFiles(path, "*.makina").FirstOrDefault() ?? "Untitled Project";
        
        if (projectFilePath != null)
        {
            // 파일이 있으면 읽어오기 (역직렬화)
            var json = await File.ReadAllTextAsync(projectFilePath);
            var meta = JsonSerializer.Deserialize<ProjectMetadata>(json);
            ProjectName = meta?.Name ?? Path.GetFileName(path);
        }
        else
        {
            // 파일이 없으면 폴더명으로 새 프로젝트 생성 및 파일 굽기
            ProjectName = Path.GetFileName(path);
            var newMeta = new ProjectMetadata { Name = ProjectName };
            string savePath = Path.Combine(path, $"{ProjectName}.makina");
            await File.WriteAllTextAsync(savePath, JsonSerializer.Serialize(newMeta));
        }

        // 2. 에셋 브라우저 갱신 (기존 로직)
        ProjectAssets.Clear();
        var rootNode = new AssetNode(path) { Name = ProjectName };
        await Task.Run(() => AssetService.ScanDirectory(path, rootNode.Children));
        ProjectAssets.Add(rootNode);

        // 🎯 프로젝트 로드 완료 상태로 변경
        IsProjectLoaded = true;
        
        // 최근 리스트 업데이트 및 설정 저장
        UpdateRecentProjects(path);
        SaveSettings();
    }

    public async Task CreateNewProject(string parentPath)
{
    // 1. 프로젝트 이름 결정 (일단 임시로 NewProject, 나중에 다이얼로그로 입력받아도 됨)
    string projectName = "NewMakinaProject";
    string projectPath = Path.Combine(parentPath, projectName);

    // 2. 물리적 폴더 및 기본 서브폴더 생성
    if (!Directory.Exists(projectPath))
    {
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Scenarios"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Scripts"));
    }

    // 3. .makina 프로젝트 파일 생성
    var meta = new ProjectMetadata { Name = projectName, Version = "1.0.0" };
    string metaPath = Path.Combine(projectPath, $"{projectName}.makina");
    await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta));

    // 4. 바로 해당 프로젝트 열기
    await OpenProjectFolder(projectPath);
}
    private void UpdateRecentProjects(string path)
    {
        var existing = RecentProjects.FirstOrDefault(x => x.Path == path);
        if (existing != null) RecentProjects.Remove(existing);
        
        RecentProjects.Insert(0, new RecentProjectItem { 
            Name = Path.GetFileName(path), 
            Path = path 
        });

        // 최대 10개만 유지
        while (RecentProjects.Count > 10) RecentProjects.RemoveAt(10);
    }
    private void SaveLastProjectPath(string path)
    {
        try 
        {
            // 리눅스/윈도우 공용 로컬 설정 저장 (실행 파일 근처에 저장)
            File.WriteAllText("last_project.txt", path);
        }
        catch { /* 권한 에러 등 무시 */ }
    }

    public string? GetLastProjectPath()
    {
        if (File.Exists("last_project.txt"))
            return File.ReadAllText("last_project.txt");
        return null;
    }

    private void SaveSettings() { /* JsonSerializer 사용 */ }
    private void LoadSettings() { /* JsonSerializer 사용 */ }

    // --- [4. 플로우 커맨드 로직] ---
    public void AddTextCommand(FlowCommand? target) => InsertOrAdd(target, new TextCommand { TextContent = "새 대사..." });
    public void AddBgCommand(FlowCommand? target) => InsertOrAdd(target, new BgCommand { AssetId = "default_bg" });
    public void RemoveCommand(FlowCommand target) => ActiveUserFlow.Remove(target);

    private void InsertOrAdd(FlowCommand? target, FlowCommand newNode)
    {
        if (target == null) ActiveUserFlow.Insert(0, newNode);
        else ActiveUserFlow.Insert(ActiveUserFlow.IndexOf(target) + 1, newNode);
    }
}