using System;
using System.IO;
using ReactiveUI;
using MakinaEditor.Core;

namespace MakinaEditor.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public static MainWindowViewModel? Instance { get; private set; }

    // --- [서브 ViewModels] ---
    public ProjectViewModel Project { get; }
    public ScenarioViewModel Scenario { get; }
    public FlowEditorViewModel Flow { get; }
    public PreviewViewModel Preview { get; }
    public AssetBrowserViewModel Assets { get; }

    // --- [메인 레이아웃 및 런처 상태 제어] ---
    private bool _isProjectLoaded = false;
    public bool IsProjectLoaded 
    { 
        get => _isProjectLoaded; 
        set => this.RaiseAndSetIfChanged(ref _isProjectLoaded, value); 
    }

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

    private string _statusText = "준비됨";
    public string StatusText 
    { 
        get => _statusText; 
        set => this.RaiseAndSetIfChanged(ref _statusText, value); 
    }

    public MainWindowViewModel()
    {
        Instance = this;

        // 서브 ViewModel 인스턴스 생성 (Mediator 패턴 적용을 위해 parent 주입)
        Project = new ProjectViewModel(this);
        Scenario = new ScenarioViewModel(this);
        Flow = new FlowEditorViewModel(this);
        Preview = new PreviewViewModel(this);
        Assets = new AssetBrowserViewModel(this);

        // 자동 실행 기능: 최근 항목이 있다면 즉시 로드 시도
        if (Project.RecentProjects.Count > 0)
        {
            var lastPath = Project.RecentProjects[0].Path;
            if (Directory.Exists(lastPath))
            {
                _ = Project.OpenProjectFolder(lastPath); // 비동기 실행
            }
        }
    }
}