using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using ReactiveUI;
using MakinaEditor.ViewModels;

namespace MakinaEditor.Models;

public enum NodeType
{
    Start,
    End,
    Flow,
    Link,
    Conditional
}

public class GraphNodeInfo : ReactiveObject
{
    private string _id = "";
    public string Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    private NodeType _type = NodeType.Flow;
    public NodeType Type
    {
        get => _type;
        set
        {
            this.RaiseAndSetIfChanged(ref _type, value);
            this.RaisePropertyChanged(nameof(IsDeleteVisible));
            this.RaisePropertyChanged(nameof(IsStartNode));
            this.RaisePropertyChanged(nameof(IsEndNode));
            this.RaisePropertyChanged(nameof(IsFlowNode));
            this.RaisePropertyChanged(nameof(IsLinkNode));
            this.RaisePropertyChanged(nameof(IsConditionalNode));
            this.RaisePropertyChanged(nameof(NodeBackground));
            this.RaisePropertyChanged(nameof(NodeBorderBrush));
        }
    }

    private string _title = "New Node";
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    private double _x;
    public double X
    {
        get => _x;
        set => this.RaiseAndSetIfChanged(ref _x, value);
    }

    private double _y;
    public double Y
    {
        get => _y;
        set => this.RaiseAndSetIfChanged(ref _y, value);
    }

    private string? _bindingId;
    public string? BindingId
    {
        get => _bindingId;
        set => this.RaiseAndSetIfChanged(ref _bindingId, value);
    }

    private string? _condition;
    public string? Condition
    {
        get => _condition;
        set => this.RaiseAndSetIfChanged(ref _condition, value);
    }

    private string? _targetNodeId;
    public string? TargetNodeId
    {
        get => _targetNodeId;
        set 
        {
            this.RaiseAndSetIfChanged(ref _targetNodeId, value);
            this.RaisePropertyChanged(nameof(TargetNode));
        }
    }

    private string? _alternativeTargetNodeId;
    public string? AlternativeTargetNodeId
    {
        get => _alternativeTargetNodeId;
        set 
        {
            this.RaiseAndSetIfChanged(ref _alternativeTargetNodeId, value);
            this.RaisePropertyChanged(nameof(AlternativeTargetNode));
        }
    }

    // XAML 조건부 렌더링용 헬퍼 플래그
    public bool IsDeleteVisible => Type != NodeType.Start && Type != NodeType.End;
    public bool IsStartNode => Type == NodeType.Start;
    public bool IsEndNode => Type == NodeType.End;
    public bool IsFlowNode => Type == NodeType.Flow;
    public bool IsLinkNode => Type == NodeType.Link;
    public bool IsConditionalNode => Type == NodeType.Conditional;

    // 비주얼 색상 동적 바인딩용 속성
    public string NodeBackground => Type switch
    {
        NodeType.Start => "#1E2F26",
        NodeType.End => "#2F1E1E",
        NodeType.Flow => "#202A35",
        NodeType.Link => "#2F251E",
        NodeType.Conditional => "#2F1E2E",
        _ => "#202A35"
    };

    public string NodeBorderBrush => Type switch
    {
        NodeType.Start => "#2ECC71",
        NodeType.End => "#E74C3C",
        NodeType.Flow => "#3498DB",
        NodeType.Link => "#E67E22",
        NodeType.Conditional => "#9B59B6",
        _ => "#3498DB"
    };

    // ComboBox SelectedItem 연동용 프로퍼티
    [JsonIgnore]
    public GraphNodeInfo? TargetNode
    {
        get
        {
            var nodes = MainWindowViewModel.Instance?.ScenarioGraph?.Nodes;
            return nodes?.FirstOrDefault(x => x.Id == TargetNodeId);
        }
        set
        {
            TargetNodeId = value?.Id;
            this.RaisePropertyChanged(nameof(TargetNode));
        }
    }

    [JsonIgnore]
    public GraphNodeInfo? AlternativeTargetNode
    {
        get
        {
            var nodes = MainWindowViewModel.Instance?.ScenarioGraph?.Nodes;
            return nodes?.FirstOrDefault(x => x.Id == AlternativeTargetNodeId);
        }
        set
        {
            AlternativeTargetNodeId = value?.Id;
            this.RaisePropertyChanged(nameof(AlternativeTargetNode));
        }
    }
}

public class MakinaScenario
{
    public string ScenarioName { get; set; } // 예: "ep_01_intro"

    // 🌐 [커널 메모리 영역] 노드 그래프 UI와 바인딩 됨
    // Key: 매크로 블록 ID (예: "scene_01_logic")
    // Value: 흐름 제어 OP코드 배열
    public Dictionary<string, ObservableCollection<ScenarioCommand>> KernelRoutines { get; }

    // 🎬 [유저 메모리 영역] 더블클릭 시 타임라인 UI와 바인딩 됨
    // Key: 연출 플로우 ID (예: "flow_01_dialogue")
    // Value: 화면 연출 OP코드 배열
    [JsonIgnore]
    public Dictionary<string, ObservableCollection<FlowCommand>> UserFlows { get; }

    // 🗺️ 노드 그래프 시각화 정보
    public ObservableCollection<GraphNodeInfo> GraphNodes { get; set; }

    public MakinaScenario()
    {
        ScenarioName = "Untitled";
        KernelRoutines = new Dictionary<string, ObservableCollection<ScenarioCommand>>();
        UserFlows = new Dictionary<string, ObservableCollection<FlowCommand>>();
        GraphNodes = new ObservableCollection<GraphNodeInfo>();
    }

    public MakinaScenario(string name) : this()
    {
        ScenarioName = name;
    }
}