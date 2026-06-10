using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Core;

namespace MakinaEditor.ViewModels;

public class GraphEdge : ReactiveObject
{
    private string _sourceId = "";
    public string SourceId { get => _sourceId; set => this.RaiseAndSetIfChanged(ref _sourceId, value); }

    private string _targetId = "";
    public string TargetId { get => _targetId; set => this.RaiseAndSetIfChanged(ref _targetId, value); }

    private double _startX;
    public double StartX 
    { 
        get => _startX; 
        set 
        {
            this.RaiseAndSetIfChanged(ref _startX, value);
            this.RaisePropertyChanged(nameof(StartPoint));
        }
    }

    private double _startY;
    public double StartY 
    { 
        get => _startY; 
        set 
        {
            this.RaiseAndSetIfChanged(ref _startY, value);
            this.RaisePropertyChanged(nameof(StartPoint));
        }
    }

    private double _endX;
    public double EndX 
    { 
        get => _endX; 
        set 
        {
            this.RaiseAndSetIfChanged(ref _endX, value);
            this.RaisePropertyChanged(nameof(EndPoint));
        }
    }

    private double _endY;
    public double EndY 
    { 
        get => _endY; 
        set 
        {
            this.RaiseAndSetIfChanged(ref _endY, value);
            this.RaisePropertyChanged(nameof(EndPoint));
        }
    }

    public Avalonia.Point StartPoint => new Avalonia.Point(StartX, StartY);
    public Avalonia.Point EndPoint => new Avalonia.Point(EndX, EndY);

    private bool _isAlternative;
    public bool IsAlternative 
    { 
        get => _isAlternative; 
        set 
        {
            this.RaiseAndSetIfChanged(ref _isAlternative, value);
            this.RaisePropertyChanged(nameof(StrokeColor));
            this.RaisePropertyChanged(nameof(StrokeDash));
        }
    }

    private bool _isPlayOnly;
    public bool IsPlayOnly 
    { 
        get => _isPlayOnly; 
        set 
        {
            this.RaiseAndSetIfChanged(ref _isPlayOnly, value);
            this.RaisePropertyChanged(nameof(StrokeColor));
            this.RaisePropertyChanged(nameof(StrokeDash));
        }
    }

    public string StrokeColor => IsAlternative ? "#E74C3C" : (IsPlayOnly ? "#E67E22" : "#9B59B6");
    public string StrokeDash => IsAlternative ? "2,2" : (IsPlayOnly ? "4,4" : "1,0");
}

public class ScenarioGraphViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<GraphNodeInfo> Nodes => _main.Scenario.ActiveScenario.GraphNodes;
    public ObservableCollection<GraphEdge> Edges { get; } = new();

    public ObservableCollection<string> AvailableScenarios => _main.Scenario.Scenarios;
    public ObservableCollection<string> AvailableFlows => _main.Flow.AvailableFlows;

    public ScenarioGraphViewModel(MainWindowViewModel main)
    {
        _main = main;
        
        // ActiveScenario 변경 감지
        _main.Scenario.WhenAnyValue(x => x.ActiveScenario)
            .Subscribe(_ => OnActiveScenarioChanged());
    }

    private void OnActiveScenarioChanged()
    {
        EnsureStartEndNodes();
        RebuildEdges();
        
        // 컬렉션 변경 및 노드 속성 변경 감시 등록
        Nodes.CollectionChanged -= OnNodesCollectionChanged;
        Nodes.CollectionChanged += OnNodesCollectionChanged;

        foreach (var node in Nodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            node.PropertyChanged += OnNodePropertyChanged;
        }
    }

    private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (GraphNodeInfo node in e.NewItems)
            {
                node.PropertyChanged += OnNodePropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (GraphNodeInfo node in e.OldItems)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
            }
        }
        RebuildEdges();
        _ = _main.Project.SaveProject();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphNodeInfo.X) || 
            e.PropertyName == nameof(GraphNodeInfo.Y) || 
            e.PropertyName == nameof(GraphNodeInfo.TargetNodeId) || 
            e.PropertyName == nameof(GraphNodeInfo.AlternativeTargetNodeId))
        {
            RebuildEdges();
        }
        
        if (e.PropertyName == nameof(GraphNodeInfo.TargetNodeId) || 
            e.PropertyName == nameof(GraphNodeInfo.AlternativeTargetNodeId) ||
            e.PropertyName == nameof(GraphNodeInfo.Title) ||
            e.PropertyName == nameof(GraphNodeInfo.BindingId) ||
            e.PropertyName == nameof(GraphNodeInfo.Condition))
        {
            _ = _main.Project.SaveProject();
        }
    }

    public void SaveNodePositions()
    {
        _ = _main.Project.SaveProject();
    }

    public void EnsureStartEndNodes()
    {
        if (_main.Scenario.ActiveScenario == null) return;

        var start = Nodes.FirstOrDefault(x => x.Type == NodeType.Start);
        if (start == null)
        {
            Nodes.Add(new GraphNodeInfo
            {
                Id = "start_node",
                Type = NodeType.Start,
                Title = "시나리오 시작",
                X = 50,
                Y = 200
            });
        }

        var end = Nodes.FirstOrDefault(x => x.Type == NodeType.End);
        if (end == null)
        {
            Nodes.Add(new GraphNodeInfo
            {
                Id = "end_node",
                Type = NodeType.End,
                Title = "시나리오 끝",
                X = 650,
                Y = 200
            });
        }
    }

    public void RebuildEdges()
    {
        Edges.Clear();
        if (_main.Scenario.ActiveScenario == null) return;

        foreach (var node in Nodes)
        {
            bool isPlayOnly = (node.Type == NodeType.End || node.Type == NodeType.Link || node.Type == NodeType.Conditional);

            if (!string.IsNullOrEmpty(node.TargetNodeId))
            {
                var target = Nodes.FirstOrDefault(x => x.Id == node.TargetNodeId);
                if (target != null)
                {
                    Edges.Add(new GraphEdge
                    {
                        SourceId = node.Id,
                        TargetId = target.Id,
                        StartX = node.X + 90,
                        StartY = node.Y + 40,
                        EndX = target.X + 90,
                        EndY = target.Y,
                        IsAlternative = false,
                        IsPlayOnly = isPlayOnly
                    });
                }
            }

            if (!string.IsNullOrEmpty(node.AlternativeTargetNodeId))
            {
                var target = Nodes.FirstOrDefault(x => x.Id == node.AlternativeTargetNodeId);
                if (target != null)
                {
                    Edges.Add(new GraphEdge
                    {
                        SourceId = node.Id,
                        TargetId = target.Id,
                        StartX = node.X + 90,
                        StartY = node.Y + 40,
                        EndX = target.X + 90,
                        EndY = target.Y,
                        IsAlternative = true,
                        IsPlayOnly = isPlayOnly
                    });
                }
            }
        }
    }

    public void AddFlowNode(string flowName)
    {
        if (string.IsNullOrWhiteSpace(flowName)) return;
        string fName = flowName.Trim();

        if (!_main.Flow.AvailableFlows.Contains(fName))
        {
            var defaultCmd = new TextCommand { Speaker = "시스템", TextContent = $"'{fName}' 플로우 시작" };
            _main.Scenario.ActiveScenario.UserFlows[fName] = new ObservableCollection<FlowCommand> { defaultCmd };
            _main.Flow.AvailableFlows.Add(fName);
        }

        var node = new GraphNodeInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = NodeType.Flow,
            Title = fName,
            BindingId = fName,
            X = 300,
            Y = 200
        };
        Nodes.Add(node);
        _main.StatusText = $"플로우 노드 '{fName}' 생성 완료.";
    }

    public void AddLinkNode(string targetScenarioName)
    {
        if (string.IsNullOrWhiteSpace(targetScenarioName)) return;

        var node = new GraphNodeInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = NodeType.Link,
            Title = $"시나리오 점프: {targetScenarioName}",
            BindingId = targetScenarioName,
            X = 850,
            Y = 200
        };
        Nodes.Add(node);
        _main.StatusText = $"시나리오 점프 노드 '{targetScenarioName}' 생성 완료.";
    }

    public void AddConditionalNode(string condition)
    {
        string cond = string.IsNullOrWhiteSpace(condition) ? "flag == true" : condition.Trim();

        var node = new GraphNodeInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = NodeType.Conditional,
            Title = "조건식 분기",
            Condition = cond,
            X = 850,
            Y = 350
        };
        Nodes.Add(node);
        _main.StatusText = $"조건부 분기 노드 생성 완료.";
    }

    public void DeleteNode(GraphNodeInfo target)
    {
        if (target == null) return;
        if (target.Type == NodeType.Start || target.Type == NodeType.End)
        {
            _main.StatusText = "시작 노드와 끝 노드는 삭제할 수 없습니다.";
            return;
        }

        foreach (var node in Nodes)
        {
            if (node.TargetNodeId == target.Id) node.TargetNodeId = null;
            if (node.AlternativeTargetNodeId == target.Id) node.AlternativeTargetNodeId = null;
        }

        Nodes.Remove(target);
        _main.StatusText = $"노드 '{target.Title}' 삭제 완료.";
    }

    public void NavigateToFlow(string? flowId)
    {
        if (string.IsNullOrEmpty(flowId)) return;
        if (_main.Flow.AvailableFlows.Contains(flowId))
        {
            _main.Flow.SelectedFlowName = flowId;
            _main.CurrentMode = EditorMode.Flow_Timeline;
            _main.StatusText = $"플로우 '{flowId}' 타임라인 편집으로 전환.";
        }
    }
}
