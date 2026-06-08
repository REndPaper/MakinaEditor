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
using Avalonia.Media.Imaging;

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
    public static MainWindowViewModel? Instance { get; private set; }

    public ObservableCollection<string> AvailableFlows { get; } = new();
    public Dictionary<string, string> AssetRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

    // --- [리소스 객체 정의 속성들] ---
    public ObservableCollection<ResourceObject> ProjectResources { get; } = new();
    public ObservableCollection<string> RegisteredBgs { get; } = new();
    public ObservableCollection<string> RegisteredCharacters { get; } = new();

    // XAML에서 캐릭터 아이디별 포즈 목록을 딕셔너리 인덱서 바인딩 형태로 사용 가능
    public ObservableCollection<string> this[string? characterId]
    {
        get
        {
            if (string.IsNullOrEmpty(characterId)) return new ObservableCollection<string>();
            var character = ProjectResources.FirstOrDefault(x => x.Type == ResourceType.Character && x.Id.Equals(characterId, StringComparison.OrdinalIgnoreCase));
            if (character != null)
            {
                return new ObservableCollection<string>(character.Variations.Keys);
            }
            return new ObservableCollection<string>();
        }
    }

    public ResourceType[] ResourceTypes => Enum.GetValues<ResourceType>();

    private string _newResourceIdInput = "";
    public string NewResourceIdInput
    {
        get => _newResourceIdInput;
        set => this.RaiseAndSetIfChanged(ref _newResourceIdInput, value);
    }

    private string _newResourcePathInput = "";
    public string NewResourcePathInput
    {
        get => _newResourcePathInput;
        set => this.RaiseAndSetIfChanged(ref _newResourcePathInput, value);
    }

    private string _newPoseNameInput = "";
    public string NewPoseNameInput
    {
        get => _newPoseNameInput;
        set => this.RaiseAndSetIfChanged(ref _newPoseNameInput, value);
    }

    private string _newPosePathInput = "";
    public string NewPosePathInput
    {
        get => _newPosePathInput;
        set => this.RaiseAndSetIfChanged(ref _newPosePathInput, value);
    }

    public ObservableCollection<KeyValuePair<string, string>> SelectedResourcePoses { get; } = new();

    private KeyValuePair<string, string>? _selectedPoseItem;
    public KeyValuePair<string, string>? SelectedPoseItem
    {
        get => _selectedPoseItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPoseItem, value);
            if (value != null)
            {
                NewPoseNameInput = value.Value.Key;
                NewPosePathInput = value.Value.Value;
            }
        }
    }

    public bool IsCharacterSelected => SelectedResource != null && SelectedResource.Type == ResourceType.Character;

    private ResourceType _newResourceTypeInput = ResourceType.Background;
    public ResourceType NewResourceTypeInput
    {
        get => _newResourceTypeInput;
        set => this.RaiseAndSetIfChanged(ref _newResourceTypeInput, value);
    }

    private ResourceObject? _selectedResource;
    public ResourceObject? SelectedResource
    {
        get => _selectedResource;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedResource, value);
            this.RaisePropertyChanged(nameof(IsCharacterSelected));
            UpdateSelectedResourcePosesList();

            if (value != null)
            {
                NewResourceIdInput = value.Id;
                NewResourceTypeInput = value.Type;
                NewResourcePathInput = value.Type == ResourceType.Background ? value.FilePath : "";
            }
            else
            {
                NewResourceIdInput = "";
                NewResourcePathInput = "";
            }
        }
    }

    private AssetNode? _selectedAssetNode;
    public AssetNode? SelectedAssetNode
    {
        get => _selectedAssetNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedAssetNode, value);
            if (value != null && value.Type == AssetType.Image && !string.IsNullOrEmpty(_currentProjectPath))
            {
                string relPath = Path.GetRelativePath(_currentProjectPath, value.FullPath).Replace('\\', '/');
                string fileName = Path.GetFileNameWithoutExtension(value.FullPath);
                
                // 캐릭터 객체 선택 시 포즈 추가 자동 제안
                if (IsCharacterSelected)
                {
                    NewPosePathInput = relPath;
                    
                    var parts = fileName.Split('_');
                    if (parts.Length >= 3 && (parts[0].Equals("char", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("character", StringComparison.OrdinalIgnoreCase)))
                    {
                        NewPoseNameInput = string.Join('_', parts, 2, parts.Length - 2);
                    }
                    else if (parts.Length >= 2)
                    {
                        NewPoseNameInput = string.Join('_', parts, 1, parts.Length - 1);
                    }
                    else
                    {
                        NewPoseNameInput = fileName;
                    }
                }
                else
                {
                    // 일반 자원 등록 제안
                    NewResourcePathInput = relPath;
                    
                    if (relPath.Contains("bg", StringComparison.OrdinalIgnoreCase) || relPath.Contains("background", StringComparison.OrdinalIgnoreCase))
                    {
                        NewResourceTypeInput = ResourceType.Background;
                        NewResourceIdInput = fileName;
                    }
                    else
                    {
                        NewResourceTypeInput = ResourceType.Character;
                        
                        var parts = fileName.Split('_');
                        if (parts.Length >= 3 && (parts[0].Equals("char", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("character", StringComparison.OrdinalIgnoreCase)))
                        {
                            NewResourceIdInput = parts[1];
                        }
                        else if (parts.Length >= 2 && (parts[0].Equals("bg", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("background", StringComparison.OrdinalIgnoreCase)))
                        {
                            NewResourceTypeInput = ResourceType.Background;
                            NewResourceIdInput = fileName;
                        }
                        else if (parts.Length >= 2)
                        {
                            NewResourceIdInput = parts[0];
                        }
                        else
                        {
                            NewResourceIdInput = fileName;
                        }
                    }
                }
            }
        }
    }

    private string _newScenarioNameInput = "";
    public string NewScenarioNameInput
    {
        get => _newScenarioNameInput;
        set => this.RaiseAndSetIfChanged(ref _newScenarioNameInput, value);
    }

    private string _newFlowNameInput = "";
    public string NewFlowNameInput
    {
        get => _newFlowNameInput;
        set => this.RaiseAndSetIfChanged(ref _newFlowNameInput, value);
    }

    private string? _selectedFlowName;
    public string? SelectedFlowName
    {
        get => _selectedFlowName;
        set
        {
            if (_selectedFlowName != value)
            {
                // 1. 기존 Flow의 명령어 백업
                if (!string.IsNullOrEmpty(_selectedFlowName) && _activeScenario != null)
                {
                    _activeScenario.UserFlows[_selectedFlowName] = new ObservableCollection<FlowCommand>(ActiveUserFlow);
                }

                this.RaiseAndSetIfChanged(ref _selectedFlowName, value);

                // 2. 신규 Flow의 명령어 로드
                if (!string.IsNullOrEmpty(value) && _activeScenario != null)
                {
                    ActiveUserFlow.Clear();
                    if (_activeScenario.UserFlows.TryGetValue(value, out var flow))
                    {
                        foreach (var cmd in flow)
                        {
                            ActiveUserFlow.Add(cmd);
                        }
                    }
                    SelectedCommand = ActiveUserFlow.FirstOrDefault();
                    RefreshAllShowCharCommandsPoses();
                }
            }
        }
    }
    
    private MakinaScenario _activeScenario = new("New Project");
    private string? _currentProjectPath;
    public string? CurrentProjectPath => _currentProjectPath;

    // --- [3. 프리뷰 상태 속성들] ---
    private string? _previewBg;
    public string? PreviewBg { get => _previewBg; set => this.RaiseAndSetIfChanged(ref _previewBg, value); }

    private Bitmap? _previewBgImage;
    public Bitmap? PreviewBgImage { get => _previewBgImage; set => this.RaiseAndSetIfChanged(ref _previewBgImage, value); }

    private string _previewSpeaker = "";
    public string PreviewSpeaker { get => _previewSpeaker; set => this.RaiseAndSetIfChanged(ref _previewSpeaker, value); }

    private string _previewText = "";
    public string PreviewText { get => _previewText; set => this.RaiseAndSetIfChanged(ref _previewText, value); }

    private string? _previewLeftChar;
    public string? PreviewLeftChar { get => _previewLeftChar; set => this.RaiseAndSetIfChanged(ref _previewLeftChar, value); }

    private Bitmap? _previewLeftCharImage;
    public Bitmap? PreviewLeftCharImage { get => _previewLeftCharImage; set => this.RaiseAndSetIfChanged(ref _previewLeftCharImage, value); }

    private string? _previewCenterChar;
    public string? PreviewCenterChar { get => _previewCenterChar; set => this.RaiseAndSetIfChanged(ref _previewCenterChar, value); }

    private Bitmap? _previewCenterCharImage;
    public Bitmap? PreviewCenterCharImage { get => _previewCenterCharImage; set => this.RaiseAndSetIfChanged(ref _previewCenterCharImage, value); }

    private string? _previewRightChar;
    public string? PreviewRightChar { get => _previewRightChar; set => this.RaiseAndSetIfChanged(ref _previewRightChar, value); }

    private Bitmap? _previewRightCharImage;
    public Bitmap? PreviewRightCharImage { get => _previewRightCharImage; set => this.RaiseAndSetIfChanged(ref _previewRightCharImage, value); }

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
        Instance = this;
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

                ProjectResources.Clear();
                if (meta?.Resources != null)
                {
                    foreach (var r in meta.Resources)
                    {
                        ProjectResources.Add(r);
                    }
                }
                UpdateRegisteredResourcesList();
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

        // 2. 에셋 브라우저 갱신 및 라벨링
        await RefreshAssets();

        // 3. 시나리오 폴더 스캔 및 로드
        Scenarios.Clear();
        string scenarioFolder = Path.Combine(path, "Scenarios");
        if (!Directory.Exists(scenarioFolder))
        {
            Directory.CreateDirectory(scenarioFolder);
        }

        // 3-1. 디렉터리 기준 수집
        var dirs = Directory.GetDirectories(scenarioFolder);
        foreach (var dir in dirs)
        {
            Scenarios.Add(Path.GetFileName(dir));
        }

        // 3-2. 레거시 단일 JSON 파일 기준 수집 (하위 호환 마이그레이션 대상)
        var scenarioFiles = Directory.GetFiles(scenarioFolder, "*.json");
        foreach (var file in scenarioFiles)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!Scenarios.Contains(name))
            {
                Scenarios.Add(name);
            }
        }

        if (Scenarios.Count == 0)
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

            Scenarios.Add(scenarioName);
        }

        // 4. 첫 시나리오 로딩
        IsProjectLoaded = true;
        SelectedScenarioName = Scenarios.FirstOrDefault();

        StatusText = $"프로젝트 '{ProjectName}'를 성공적으로 로드했습니다.";
        
        // 최근 리스트 업데이트 및 설정 저장
        UpdateRecentProjects(path);
        SaveSettings();
    }

    public async Task RefreshAssets()
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return;

        StatusText = "에셋 디렉터리를 스캔하는 중...";

        ProjectAssets.Clear();
        var rootNode = new AssetNode(_currentProjectPath) { Name = ProjectName };
        await AssetService.ScanDirectoryAsync(_currentProjectPath, rootNode.Children);
        ProjectAssets.Add(rootNode);

        // 에셋 라벨링 레지스트리 갱신
        AssetRegistry.Clear();
        BuildAssetRegistry(rootNode);

        // 비트맵 캐시 로딩 및 프리뷰 상태 업데이트
        UpdatePreviewState();

        StatusText = $"에셋 동기화 및 라벨링 완료. (총 {AssetRegistry.Count}개 에셋 식별)";
    }

    private void BuildAssetRegistry(AssetNode node)
    {
        if (node.Type == AssetType.Image || node.Type == AssetType.Audio)
        {
            string assetId = Path.GetFileNameWithoutExtension(node.FullPath);
            if (!string.IsNullOrEmpty(assetId))
            {
                AssetRegistry[assetId] = node.FullPath;
            }
        }

        foreach (var child in node.Children)
        {
            BuildAssetRegistry(child);
        }
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
            System.Diagnostics.Debug.WriteLine($"Exception in CreateNewProject: {ex}");
        }
    }

    public async Task LoadScenario(string scenarioName)
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return;

        string scenarioDir = Path.Combine(_currentProjectPath, "Scenarios", scenarioName);
        string scenarioFilePath = Path.Combine(scenarioDir, $"{scenarioName}.json");
        bool migrated = false;

        // 💡 하위 호환 마이그레이션: 레거시 단일 JSON 파일이 존재하면 폴더 구조로 전환
        if (!File.Exists(scenarioFilePath))
        {
            string legacyFilePath = Path.Combine(_currentProjectPath, "Scenarios", $"{scenarioName}.json");
            if (File.Exists(legacyFilePath))
            {
                try
                {
                    var legacyJson = await File.ReadAllTextAsync(legacyFilePath);
                    using (var doc = JsonDocument.Parse(legacyJson))
                    {
                        var root = doc.RootElement;
                        Directory.CreateDirectory(scenarioDir);
                        Directory.CreateDirectory(Path.Combine(scenarioDir, "Flows"));

                        // 1. 시나리오 메타만 기록 (저장 시점에도 JsonIgnore에 의해 Flows는 제외됨)
                        await File.WriteAllTextAsync(scenarioFilePath, legacyJson);

                        // 2. Flows 하위 요소들을 개별 파일로 분사
                        if (root.TryGetProperty("UserFlows", out var userFlowsProp) && userFlowsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in userFlowsProp.EnumerateObject())
                            {
                                string flowId = prop.Name;
                                string flowJson = prop.Value.GetRawText();
                                string flowFile = Path.Combine(scenarioDir, "Flows", $"{flowId}.json");
                                await File.WriteAllTextAsync(flowFile, flowJson);
                            }
                        }
                    }

                    // 레거시 단일 파일 제거
                    File.Delete(legacyFilePath);
                    migrated = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Migration failed for {scenarioName}: {ex.Message}");
                }
            }
            else
            {
                return; // 파일이 없으므로 불러오기 중단
            }
        }

        try
        {
            // 1. 시나리오 메타데이터 로드
            var json = await File.ReadAllTextAsync(scenarioFilePath);
            var scenario = JsonSerializer.Deserialize<MakinaScenario>(json);
            if (scenario != null)
            {
                _activeScenario = scenario;
                _activeScenario.ScenarioName = scenarioName;

                // 2. 개별 Flow 리스트 수집
                _activeScenario.UserFlows.Clear();
                string flowsDir = Path.Combine(scenarioDir, "Flows");
                if (Directory.Exists(flowsDir))
                {
                    var flowFiles = Directory.GetFiles(flowsDir, "*.json");
                    foreach (var file in flowFiles)
                    {
                        try
                        {
                            string flowId = Path.GetFileNameWithoutExtension(file);
                            var flowJson = await File.ReadAllTextAsync(file);
                            var flowCommands = JsonSerializer.Deserialize<ObservableCollection<FlowCommand>>(flowJson);
                            if (flowCommands != null)
                            {
                                _activeScenario.UserFlows[flowId] = flowCommands;
                            }
                        }
                        catch (Exception flowEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to load flow {file}: {flowEx.Message}");
                        }
                    }
                }

                // 3. Available Flows 캐시 갱신
                AvailableFlows.Clear();
                foreach (var flowKey in _activeScenario.UserFlows.Keys)
                {
                    AvailableFlows.Add(flowKey);
                }

                // 4. 만약 Flow가 비어있다면 flow_default 자동 추가
                if (AvailableFlows.Count == 0)
                {
                    var defaultCmd = new TextCommand { Speaker = "시스템", TextContent = "새 플로우가 생성되었습니다." };
                    var defaultFlow = new ObservableCollection<FlowCommand> { defaultCmd };
                    _activeScenario.UserFlows["flow_default"] = defaultFlow;
                    AvailableFlows.Add("flow_default");
                }

                // 5. Flow 포커싱 전환
                _selectedFlowName = null; // 초기화
                SelectedFlowName = AvailableFlows.Contains("flow_default") ? "flow_default" : AvailableFlows.FirstOrDefault();

                StatusText = migrated 
                    ? $"시나리오 '{scenarioName}' 마이그레이션 완료 및 로드됨."
                    : $"시나리오 '{scenarioName}' 로드됨.";
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
            // 1. 활성 Flow 상태 동기화 백업
            if (!string.IsNullOrEmpty(SelectedFlowName))
            {
                _activeScenario.UserFlows[SelectedFlowName] = new ObservableCollection<FlowCommand>(ActiveUserFlow);
            }

            string scenarioDir = Path.Combine(_currentProjectPath, "Scenarios", _activeScenario.ScenarioName);
            Directory.CreateDirectory(scenarioDir);

            string flowsDir = Path.Combine(scenarioDir, "Flows");
            Directory.CreateDirectory(flowsDir);

            // 2. 시나리오 메타데이터 저장 (UserFlows는 JsonIgnore되어 제외됨)
            string scenarioFilePath = Path.Combine(scenarioDir, $"{_activeScenario.ScenarioName}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(scenarioFilePath, JsonSerializer.Serialize(_activeScenario, options));

            // 3. 개별 Flow 저장
            foreach (var kvp in _activeScenario.UserFlows)
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
                    if (!_activeScenario.UserFlows.ContainsKey(fId))
                    {
                        File.Delete(file);
                    }
                }
            }

            // 5. 프로젝트 메타데이터 저장
            string? projectFilePath = Directory.GetFiles(_currentProjectPath, "*.makina").FirstOrDefault();
            if (string.IsNullOrEmpty(projectFilePath))
            {
                projectFilePath = Path.Combine(_currentProjectPath, $"{ProjectName}.makina");
            }
            var meta = new ProjectMetadata 
            { 
                Name = ProjectName, 
                Version = "1.0.0",
                Resources = ProjectResources.ToList()
            };
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
    // (1) 지정된 노드 뒤에 삽입
    public void AddTextCommand(FlowCommand? target) => InsertOrAdd(target, new TextCommand { Speaker = "지우", TextContent = "새 대사..." });
    public void AddBgCommand(FlowCommand? target) => InsertOrAdd(target, new BgCommand { AssetId = "default_bg" });
    public void AddCharCommand(FlowCommand? target) => InsertOrAdd(target, new ShowCharCommand { CharacterId = "지우", Pose = "기본", Position = "Center" });
    public void AddBgmCommand(FlowCommand? target) => InsertOrAdd(target, new PlayBgmCommand { AssetId = "bgm_sunny_day", Volume = 1.0f });
    public void AddShaderCommand(FlowCommand? target) => InsertOrAdd(target, new ShaderCommand { ShaderId = "default_shader", Intensity = 1.0f });

    // (2) 맨 처음에 삽입 (Gap 0)
    public void AddTextCommandStart() => InsertAtStart(new TextCommand { Speaker = "지우", TextContent = "새 대사..." });
    public void AddBgCommandStart() => InsertAtStart(new BgCommand { AssetId = "default_bg" });
    public void AddCharCommandStart() => InsertAtStart(new ShowCharCommand { CharacterId = "지우", Pose = "기본", Position = "Center" });
    public void AddBgmCommandStart() => InsertAtStart(new PlayBgmCommand { AssetId = "bgm_sunny_day", Volume = 1.0f });
    public void AddShaderCommandStart() => InsertAtStart(new ShaderCommand { ShaderId = "default_shader", Intensity = 1.0f });

    // (3) 맨 마지막에 추가
    public void AddTextCommandEnd() => AppendToEnd(new TextCommand { Speaker = "지우", TextContent = "새 대사..." });
    public void AddBgCommandEnd() => AppendToEnd(new BgCommand { AssetId = "default_bg" });
    public void AddCharCommandEnd() => AppendToEnd(new ShowCharCommand { CharacterId = "지우", Pose = "기본", Position = "Center" });
    public void AddBgmCommandEnd() => AppendToEnd(new PlayBgmCommand { AssetId = "bgm_sunny_day", Volume = 1.0f });
    public void AddShaderCommandEnd() => AppendToEnd(new ShaderCommand { ShaderId = "default_shader", Intensity = 1.0f });

    // (4) 노드 순서 위/아래 이동
    public void MoveCommandUp(FlowCommand target)
    {
        if (target == null) return;
        int index = ActiveUserFlow.IndexOf(target);
        if (index > 0)
        {
            ActiveUserFlow.Move(index, index - 1);
            SelectedCommand = target;
            UpdatePreviewState();
        }
    }

    public void MoveCommandDown(FlowCommand target)
    {
        if (target == null) return;
        int index = ActiveUserFlow.IndexOf(target);
        if (index >= 0 && index < ActiveUserFlow.Count - 1)
        {
            ActiveUserFlow.Move(index, index + 1);
            SelectedCommand = target;
            UpdatePreviewState();
        }
    }

    // (5) 노드 유형 즉석 변경
    public void ChangeToText(FlowCommand target) => ConvertCommand(target, "text");
    public void ChangeToBg(FlowCommand target) => ConvertCommand(target, "bg");
    public void ChangeToChar(FlowCommand target) => ConvertCommand(target, "char");
    public void ChangeToBgm(FlowCommand target) => ConvertCommand(target, "bgm");
    public void ChangeToShader(FlowCommand target) => ConvertCommand(target, "shader");

    private void ConvertCommand(FlowCommand oldCommand, string targetType)
    {
        if (oldCommand == null) return;
        int index = ActiveUserFlow.IndexOf(oldCommand);
        if (index < 0) return;

        FlowCommand newCommand;
        switch (targetType)
        {
            case "text":
                var txt = new TextCommand { Speaker = "지우", TextContent = "새 대사..." };
                if (oldCommand is ShowCharCommand sc) txt.Speaker = sc.CharacterId;
                newCommand = txt;
                break;
            case "bg":
                var bg = new BgCommand { AssetId = "default_bg" };
                if (oldCommand is PlayBgmCommand p) bg.AssetId = p.AssetId;
                newCommand = bg;
                break;
            case "char":
                var ch = new ShowCharCommand { CharacterId = "지우", Pose = "기본", Position = "Center" };
                if (oldCommand is TextCommand t) ch.CharacterId = t.Speaker;
                newCommand = ch;
                break;
            case "bgm":
                var bgm = new PlayBgmCommand { AssetId = "bgm_sunny_day", Volume = 1.0f };
                if (oldCommand is BgCommand b) bgm.AssetId = b.AssetId;
                newCommand = bgm;
                break;
            case "shader":
                var sh = new ShaderCommand { ShaderId = "default_shader", Intensity = 1.0f };
                if (oldCommand is BgCommand b2) sh.ShaderId = b2.AssetId;
                else if (oldCommand is PlayBgmCommand p2) sh.ShaderId = p2.AssetId;
                newCommand = sh;
                break;
            default:
                return;
        }

        ActiveUserFlow[index] = newCommand;
        SelectedCommand = newCommand;
        UpdatePreviewState();
    }

    // (6) 삭제
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
        SelectedCommand = newNode;
    }

    private void InsertAtStart(FlowCommand newNode)
    {
        ActiveUserFlow.Insert(0, newNode);
        SelectedCommand = newNode;
    }

    private void AppendToEnd(FlowCommand newNode)
    {
        ActiveUserFlow.Add(newNode);
        SelectedCommand = newNode;
    }

    // --- [6. 프리뷰 및 조작 시스템] ---
    private Bitmap? LoadAssetBitmap(string? assetId)
    {
        if (string.IsNullOrEmpty(assetId)) return null;

        // 1. 등록된 리소스 객체(ResourceObject)에서 검색
        var res = ProjectResources.FirstOrDefault(x => x.Id.Equals(assetId, StringComparison.OrdinalIgnoreCase));
        string? targetPath = null;
        if (res != null && !string.IsNullOrEmpty(_currentProjectPath))
        {
            if (res.Type == ResourceType.Background)
            {
                targetPath = Path.Combine(_currentProjectPath, res.FilePath);
            }
            else
            {
                var defaultPath = res.Variations.TryGetValue("default", out var dp) ? dp : res.Variations.Values.FirstOrDefault();
                if (!string.IsNullOrEmpty(defaultPath))
                {
                    targetPath = Path.Combine(_currentProjectPath, defaultPath);
                }
            }
        }
        
        // 2. 만약 등록된 리소스가 없으면, 기존의 파일명 라벨(AssetRegistry)에서 검색 (하위 호환성 유지)
        if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
        {
            if (AssetRegistry.TryGetValue(assetId, out var path))
            {
                targetPath = path;
            }
        }

        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
        {
            try
            {
                return new Bitmap(targetPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load bitmap {targetPath}: {ex.Message}");
            }
        }
        return null;
    }

    private Bitmap? FindCharacterBitmap(string? charId, string? pose)
    {
        if (string.IsNullOrEmpty(charId) || charId == "Hide" || pose == "Hide") return null;

        // 1. 등록된 리소스에서 캐릭터 탐색
        var res = ProjectResources.FirstOrDefault(x => x.Type == ResourceType.Character && x.Id.Equals(charId, StringComparison.OrdinalIgnoreCase));
        if (res != null && !string.IsNullOrEmpty(_currentProjectPath))
        {
            string? relPath = null;
            if (!string.IsNullOrEmpty(pose) && res.Variations.TryGetValue(pose, out var path))
            {
                relPath = path;
            }
            else if (res.Variations.TryGetValue("default", out var defPath))
            {
                relPath = defPath;
            }
            else if (res.Variations.Count > 0)
            {
                relPath = res.Variations.Values.First();
            }

            if (!string.IsNullOrEmpty(relPath))
            {
                string fullPath = Path.Combine(_currentProjectPath, relPath);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        return new Bitmap(fullPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load bitmap {fullPath}: {ex.Message}");
                    }
                }
            }
        }

        // 2. 하위 호환 폴백 로직
        // 1. char_{CharacterId}_{Pose}
        var bmp = LoadAssetBitmap($"char_{charId}_{pose}");
        if (bmp != null) return bmp;

        // 2. {CharacterId}_{Pose}
        bmp = LoadAssetBitmap($"{charId}_{pose}");
        if (bmp != null) return bmp;

        // 3. char_{CharacterId}_default
        bmp = LoadAssetBitmap($"char_{charId}_default");
        if (bmp != null) return bmp;

        // 4. {CharacterId}_default
        bmp = LoadAssetBitmap($"{charId}_default");
        if (bmp != null) return bmp;

        // 5. char_{CharacterId}
        bmp = LoadAssetBitmap($"char_{charId}");
        if (bmp != null) return bmp;

        // 6. {CharacterId}
        bmp = LoadAssetBitmap($"{charId}");
        return bmp;
    }

    public void UpdatePreviewState()
    {
        // 프리뷰 모델 상태 초기화
        PreviewBg = null;
        PreviewBgImage = null;
        PreviewSpeaker = "";
        PreviewText = "";
        PreviewLeftChar = null;
        PreviewLeftCharImage = null;
        PreviewCenterChar = null;
        PreviewCenterCharImage = null;
        PreviewRightChar = null;
        PreviewRightCharImage = null;
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
                PreviewBgImage = LoadAssetBitmap(bg.AssetId);
                if (PreviewBgImage == null && !string.IsNullOrEmpty(bg.AssetId))
                {
                    PreviewBgImage = LoadAssetBitmap($"bg_{bg.AssetId}");
                }
            }
            else if (cmd is ShowCharCommand ch)
            {
                string charDisplay = string.IsNullOrEmpty(ch.Pose) ? (ch.CharacterId ?? "") : $"{ch.CharacterId} ({ch.Pose})";
                var charBmp = FindCharacterBitmap(ch.CharacterId, ch.Pose);

                if (ch.Position == "Left")
                {
                    bool isHide = ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId);
                    PreviewLeftChar = isHide ? null : charDisplay;
                    PreviewLeftCharImage = isHide ? null : charBmp;
                }
                else if (ch.Position == "Center")
                {
                    bool isHide = ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId);
                    PreviewCenterChar = isHide ? null : charDisplay;
                    PreviewCenterCharImage = isHide ? null : charBmp;
                }
                else if (ch.Position == "Right")
                {
                    bool isHide = ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId);
                    PreviewRightChar = isHide ? null : charDisplay;
                    PreviewRightCharImage = isHide ? null : charBmp;
                }
                else if (ch.Position == "Hide" || ch.CharacterId == "Hide" || ch.Pose == "Hide")
                {
                    if (PreviewLeftChar != null && PreviewLeftChar.StartsWith(ch.CharacterId ?? "---"))
                    {
                        PreviewLeftChar = null;
                        PreviewLeftCharImage = null;
                    }
                    if (PreviewCenterChar != null && PreviewCenterChar.StartsWith(ch.CharacterId ?? "---"))
                    {
                        PreviewCenterChar = null;
                        PreviewCenterCharImage = null;
                    }
                    if (PreviewRightChar != null && PreviewRightChar.StartsWith(ch.CharacterId ?? "---"))
                    {
                        PreviewRightChar = null;
                        PreviewRightCharImage = null;
                    }
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

    // --- [7. 시나리오 및 플로우 CRUD 비즈니스 로직] ---
    public async Task CreateScenarioCommand()
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return;
        if (string.IsNullOrWhiteSpace(NewScenarioNameInput))
        {
            StatusText = "시나리오 이름을 입력해야 합니다.";
            return;
        }

        string scenarioName = NewScenarioNameInput.Trim();
        if (Scenarios.Contains(scenarioName))
        {
            StatusText = "이미 존재하는 시나리오 이름입니다.";
            return;
        }

        try
        {
            string scenarioFolder = Path.Combine(_currentProjectPath, "Scenarios");
            string scenarioDir = Path.Combine(scenarioFolder, scenarioName);
            Directory.CreateDirectory(scenarioDir);
            Directory.CreateDirectory(Path.Combine(scenarioDir, "Flows"));

            // 1. 새 시나리오 메타데이터 뼈대 생성 (UserFlows는 JsonIgnore 처리되어 제외됨)
            var newScenario = new MakinaScenario(scenarioName);
            string scenarioFilePath = Path.Combine(scenarioDir, $"{scenarioName}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(scenarioFilePath, JsonSerializer.Serialize(newScenario, options));

            // 2. 기본 플로우 파일 생성
            var startText = new TextCommand { Speaker = "시스템", TextContent = $"{scenarioName} 시나리오가 성공적으로 시작되었습니다." };
            var defaultFlow = new ObservableCollection<FlowCommand> { startText };
            string flowFilePath = Path.Combine(scenarioDir, "Flows", "flow_default.json");
            await File.WriteAllTextAsync(flowFilePath, JsonSerializer.Serialize(defaultFlow, options));

            // 목록에 추가 후 포커싱
            Scenarios.Add(scenarioName);
            NewScenarioNameInput = "";
            SelectedScenarioName = scenarioName;

            StatusText = $"새 시나리오 '{scenarioName}' 생성 완료.";
        }
        catch (Exception ex)
        {
            StatusText = $"시나리오 생성 실패: {ex.Message}";
        }
    }

    public async Task DeleteScenarioCommand()
    {
        if (string.IsNullOrEmpty(_currentProjectPath) || string.IsNullOrEmpty(SelectedScenarioName)) return;

        if (Scenarios.Count <= 1)
        {
            StatusText = "최소 한 개 이상의 시나리오는 존재해야 합니다. 삭제할 수 없습니다.";
            return;
        }

        try
        {
            string targetName = SelectedScenarioName;
            string scenarioDir = Path.Combine(_currentProjectPath, "Scenarios", targetName);

            if (Directory.Exists(scenarioDir))
            {
                Directory.Delete(scenarioDir, true);
            }

            Scenarios.Remove(targetName);
            SelectedScenarioName = Scenarios.FirstOrDefault();

            StatusText = $"시나리오 '{targetName}' 삭제 완료.";
        }
        catch (Exception ex)
        {
            StatusText = $"시나리오 삭제 실패: {ex.Message}";
        }
    }

    public void CreateFlowCommand()
    {
        if (_activeScenario == null) return;
        if (string.IsNullOrWhiteSpace(NewFlowNameInput))
        {
            StatusText = "플로우 이름을 입력해야 합니다.";
            return;
        }

        string flowName = NewFlowNameInput.Trim();
        if (AvailableFlows.Contains(flowName))
        {
            StatusText = "이미 존재하는 플로우 이름입니다.";
            return;
        }

        // 새 Flow 추가
        var defaultCmd = new TextCommand { Speaker = "시스템", TextContent = $"'{flowName}' 플로우 연출 시작" };
        _activeScenario.UserFlows[flowName] = new ObservableCollection<FlowCommand> { defaultCmd };
        
        AvailableFlows.Add(flowName);
        NewFlowNameInput = "";
        SelectedFlowName = flowName;

        StatusText = $"새 플로우 '{flowName}' 생성 완료.";
    }

    public void DeleteFlowCommand()
    {
        if (_activeScenario == null || string.IsNullOrEmpty(SelectedFlowName)) return;

        if (AvailableFlows.Count <= 1)
        {
            StatusText = "최소 한 개 이상의 플로우는 존재해야 합니다. 삭제할 수 없습니다.";
            return;
        }

        string targetName = SelectedFlowName;
        _activeScenario.UserFlows.Remove(targetName);
        AvailableFlows.Remove(targetName);
        
        SelectedFlowName = AvailableFlows.FirstOrDefault();

        StatusText = $"플로우 '{targetName}' 삭제 완료.";
    }

    public void CloseProject()
    {
        IsProjectLoaded = false;
        _currentProjectPath = null;
        ProjectName = "Untitled Project";
        ActiveUserFlow.Clear();
        AvailableFlows.Clear();
        Scenarios.Clear();
        AssetRegistry.Clear();
        ProjectAssets.Clear();
        ProjectResources.Clear();
        RegisteredBgs.Clear();
        RegisteredCharacters.Clear();
        NewResourceIdInput = "";
        NewResourcePathInput = "";
        NewPoseNameInput = "";
        NewPosePathInput = "";
        SelectedScenarioName = null;
        SelectedFlowName = null;
        SelectedAssetNode = null;
        SelectedResource = null;
        StatusText = "프로젝트가 닫혔습니다.";
    }

    public void RunProject()
    {
        StatusText = "프로젝트 실행 기능은 현재 개발 중입니다. (미래 컴파일 파이프라인 연동 예정)";
    }

    // --- [8. 리소스 객체 CRUD 비즈니스 로직] ---
    public void RegisterResourceCommand()
    {
        if (string.IsNullOrWhiteSpace(NewResourceIdInput))
        {
            StatusText = "리소스 ID를 입력해야 합니다.";
            return;
        }

        string id = NewResourceIdInput.Trim();
        string path = NewResourcePathInput.Trim();

        if (NewResourceTypeInput == ResourceType.Background && string.IsNullOrWhiteSpace(path))
        {
            StatusText = "배경 리소스는 파일 경로를 입력해야 합니다.";
            return;
        }

        // 중복 체크 및 업데이트
        var existing = ProjectResources.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Type = NewResourceTypeInput;
            if (NewResourceTypeInput == ResourceType.Background)
            {
                existing.FilePath = path;
                existing.Variations.Clear();
                StatusText = $"리소스 '{id}' 배경 수정 완료.";
            }
            else
            {
                existing.FilePath = "";
                StatusText = $"리소스 '{id}' 캐릭터 설정 유지 완료.";
            }
        }
        else
        {
            var res = new ResourceObject { Id = id, Type = NewResourceTypeInput };
            if (NewResourceTypeInput == ResourceType.Background)
            {
                res.FilePath = path;
            }
            ProjectResources.Add(res);
            StatusText = $"리소스 '{id}' 등록 완료.";
        }

        UpdateRegisteredResourcesList();
        
        NewResourceIdInput = "";
        NewResourcePathInput = "";
        SelectedResource = null;
        
        _ = SaveProject();
    }

    public void DeleteResourceCommand()
    {
        if (SelectedResource == null) return;
        
        string id = SelectedResource.Id;
        ProjectResources.Remove(SelectedResource);
        SelectedResource = null;
        
        UpdateRegisteredResourcesList();
        UpdateSelectedResourcePosesList();
        
        NewResourceIdInput = "";
        NewResourcePathInput = "";
        
        StatusText = $"리소스 '{id}' 제거 완료.";
        
        _ = SaveProject();
    }

    public void UpdateSelectedResourcePosesList()
    {
        SelectedResourcePoses.Clear();
        if (SelectedResource != null && SelectedResource.Type == ResourceType.Character)
        {
            foreach (var kvp in SelectedResource.Variations)
            {
                SelectedResourcePoses.Add(kvp);
            }
        }
    }

    // 캐릭터 전용 포즈 추가 커맨드
    public void AddPoseCommand()
    {
        if (SelectedResource == null || SelectedResource.Type != ResourceType.Character)
        {
            StatusText = "포즈를 추가할 캐릭터를 선택해야 합니다.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPoseNameInput) || string.IsNullOrWhiteSpace(NewPosePathInput))
        {
            StatusText = "포즈 이름과 이미지 상대 경로를 입력해야 합니다.";
            return;
        }

        string pose = NewPoseNameInput.Trim();
        string path = NewPosePathInput.Trim();

        SelectedResource.Variations[pose] = path;
        StatusText = $"캐릭터 '{SelectedResource.Id}'에 포즈 '{pose}' 추가 완료.";

        UpdateSelectedResourcePosesList();
        
        NewPoseNameInput = "";
        NewPosePathInput = "";

        // 인덱서 변경 통지 및 자동 저장
        this.RaisePropertyChanged("Item[]");
        _ = SaveProject();
    }

    // 캐릭터 전용 포즈 삭제 커맨드
    public void DeletePoseCommand()
    {
        if (SelectedResource == null || SelectedResource.Type != ResourceType.Character)
        {
            return;
        }

        string? poseToDelete = null;
        if (SelectedPoseItem != null)
        {
            poseToDelete = SelectedPoseItem.Value.Key;
        }
        else if (!string.IsNullOrWhiteSpace(NewPoseNameInput))
        {
            poseToDelete = NewPoseNameInput.Trim();
        }

        if (string.IsNullOrEmpty(poseToDelete))
        {
            StatusText = "삭제할 포즈를 선택하거나 이름을 입력해야 합니다.";
            return;
        }

        if (SelectedResource.Variations.Remove(poseToDelete))
        {
            StatusText = $"캐릭터 '{SelectedResource.Id}'에서 포즈 '{poseToDelete}' 삭제 완료.";
            UpdateSelectedResourcePosesList();
            SelectedPoseItem = null;
            NewPoseNameInput = "";
            NewPosePathInput = "";
            
            // 인덱서 변경 통지 및 자동 저장
            this.RaisePropertyChanged("Item[]");
            _ = SaveProject();
        }
        else
        {
            StatusText = $"포즈 '{poseToDelete}'를 찾을 수 없습니다.";
        }
    }

    private void UpdateRegisteredResourcesList()
    {
        RegisteredBgs.Clear();
        RegisteredCharacters.Clear();
        foreach (var r in ProjectResources)
        {
            if (r.Type == ResourceType.Background) RegisteredBgs.Add(r.Id);
            else RegisteredCharacters.Add(r.Id);
        }
        // 인덱서 갱신을 알리기 위해 Item[] 속성 변경 통지
        this.RaisePropertyChanged("Item[]");
        RefreshAllShowCharCommandsPoses();
    }

    public void RefreshAllShowCharCommandsPoses()
    {
        foreach (var cmd in ActiveUserFlow)
        {
            if (cmd is ShowCharCommand showChar)
            {
                showChar.UpdateAvailablePoses();
            }
        }
    }

    public void AddOrUpdateResource(ResourceObject res)
    {
        var existing = ProjectResources.FirstOrDefault(x => x.Id.Equals(res.Id, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Type = res.Type;
            existing.FilePath = res.FilePath;
            existing.Variations = res.Variations;
            StatusText = $"리소스 '{res.Id}' 수정 완료.";
        }
        else
        {
            ProjectResources.Add(res);
            StatusText = $"리소스 '{res.Id}' 등록 완료.";
        }

        UpdateRegisteredResourcesList();
        _ = SaveProject();
    }
}