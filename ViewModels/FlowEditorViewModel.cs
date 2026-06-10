using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Core;

namespace MakinaEditor.ViewModels;

public class FlowEditorViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<string> AvailableFlows { get; } = new();
    public ObservableCollection<FlowCommand> ActiveUserFlow { get; } = new();

    // 에셋 브라우저 위임 프로퍼티 추가 (XAML 바인딩 단순화용)
    public ObservableCollection<string> RegisteredBgs => _main.Assets.RegisteredBgs;
    public ObservableCollection<string> RegisteredCharacters => _main.Assets.RegisteredCharacters;
    public ObservableCollection<string> this[string? characterId] => _main.Assets[characterId];

    private string? _selectedFlowName;
    public string? SelectedFlowName
    {
        get => _selectedFlowName;
        set
        {
            if (_selectedFlowName != value)
            {
                // 1. 기존 Flow의 명령어 백업
                if (!string.IsNullOrEmpty(_selectedFlowName) && _main.Scenario.ActiveScenario != null)
                {
                    _main.Scenario.ActiveScenario.UserFlows[_selectedFlowName] = new ObservableCollection<FlowCommand>(ActiveUserFlow);
                }

                this.RaiseAndSetIfChanged(ref _selectedFlowName, value);

                // 2. 신규 Flow의 명령어 로드
                if (!string.IsNullOrEmpty(value) && _main.Scenario.ActiveScenario != null)
                {
                    ActiveUserFlow.Clear();
                    if (_main.Scenario.ActiveScenario.UserFlows.TryGetValue(value, out var flow))
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

    private FlowCommand? _selectedCommand;
    public FlowCommand? SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCommand, value);
            _main.Preview.UpdatePreviewState();
        }
    }

    private string _newFlowNameInput = "";
    public string NewFlowNameInput
    {
        get => _newFlowNameInput;
        set => this.RaiseAndSetIfChanged(ref _newFlowNameInput, value);
    }

    public FlowEditorViewModel(MainWindowViewModel main)
    {
        _main = main;
    }

    public void ClearSelectedFlowNameSilently()
    {
        _selectedFlowName = null;
        this.RaisePropertyChanged(nameof(SelectedFlowName));
    }

    public void CreateFlowCommand()
    {
        if (_main.Scenario.ActiveScenario == null) return;
        if (string.IsNullOrWhiteSpace(NewFlowNameInput))
        {
            _main.StatusText = "플로우 이름을 입력해야 합니다.";
            return;
        }

        string flowName = NewFlowNameInput.Trim();
        if (AvailableFlows.Contains(flowName))
        {
            _main.StatusText = "이미 존재하는 플로우 이름입니다.";
            return;
        }

        // 새 Flow 추가
        var defaultCmd = new TextCommand { Speaker = "시스템", TextContent = $"'{flowName}' 플로우 연출 시작" };
        _main.Scenario.ActiveScenario.UserFlows[flowName] = new ObservableCollection<FlowCommand> { defaultCmd };
        
        AvailableFlows.Add(flowName);
        NewFlowNameInput = "";
        SelectedFlowName = flowName;

        _main.StatusText = $"새 플로우 '{flowName}' 생성 완료.";
    }

    public void DeleteFlowCommand()
    {
        if (_main.Scenario.ActiveScenario == null || string.IsNullOrEmpty(SelectedFlowName)) return;

        if (AvailableFlows.Count <= 1)
        {
            _main.StatusText = "최소 한 개 이상의 플로우는 존재해야 합니다. 삭제할 수 없습니다.";
            return;
        }

        string targetName = SelectedFlowName;
        _main.Scenario.ActiveScenario.UserFlows.Remove(targetName);
        AvailableFlows.Remove(targetName);
        
        SelectedFlowName = AvailableFlows.FirstOrDefault();

        _main.StatusText = $"플로우 '{targetName}' 삭제 완료.";
    }

    // --- 플로우 커맨드 CRUD 로직 ---
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
            var action = new MoveCommandAction(ActiveUserFlow, target, index, index - 1, this);
            _main.UndoRedo.Execute(action);
        }
    }

    public void MoveCommandDown(FlowCommand target)
    {
        if (target == null) return;
        int index = ActiveUserFlow.IndexOf(target);
        if (index >= 0 && index < ActiveUserFlow.Count - 1)
        {
            var action = new MoveCommandAction(ActiveUserFlow, target, index, index + 1, this);
            _main.UndoRedo.Execute(action);
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

        var action = new ConvertCommandAction(ActiveUserFlow, oldCommand, newCommand, index, this);
        _main.UndoRedo.Execute(action);
    }

    // (6) 삭제
    public void RemoveCommand(FlowCommand target)
    {
        if (target == null) return;
        int index = ActiveUserFlow.IndexOf(target);
        if (index < 0) return;

        var action = new RemoveCommandAction(ActiveUserFlow, target, index, this);
        _main.UndoRedo.Execute(action);
    }

    private void InsertOrAdd(FlowCommand? target, FlowCommand newNode)
    {
        int index = (target == null) ? 0 : ActiveUserFlow.IndexOf(target) + 1;
        var action = new AddCommandAction(ActiveUserFlow, newNode, index, this);
        _main.UndoRedo.Execute(action);
    }

    private void InsertAtStart(FlowCommand newNode)
    {
        var action = new AddCommandAction(ActiveUserFlow, newNode, 0, this);
        _main.UndoRedo.Execute(action);
    }

    private void AppendToEnd(FlowCommand newNode)
    {
        var action = new AddCommandAction(ActiveUserFlow, newNode, ActiveUserFlow.Count, this);
        _main.UndoRedo.Execute(action);
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
}
