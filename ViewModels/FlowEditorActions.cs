using System;
using System.Collections.ObjectModel;
using System.Linq;
using MakinaEditor.Core;
using MakinaEditor.Models;

namespace MakinaEditor.ViewModels;

// 1. 카드 삽입 액션
public class AddCommandAction : IUndoableAction
{
    private readonly ObservableCollection<FlowCommand> _list;
    private readonly FlowCommand _newNode;
    private readonly int _index;
    private readonly FlowEditorViewModel _flowVm;

    public string Description => $"연출 명령어 추가: {_newNode.Opcode}";

    public AddCommandAction(ObservableCollection<FlowCommand> list, FlowCommand newNode, int index, FlowEditorViewModel flowVm)
    {
        _list = list;
        _newNode = newNode;
        _index = index;
        _flowVm = flowVm;
    }

    public void Execute()
    {
        _list.Insert(_index, _newNode);
        _flowVm.SelectedCommand = _newNode;
    }

    public void Undo()
    {
        _list.Remove(_newNode);
        _flowVm.SelectedCommand = (_index > 0 && _list.Count > 0) ? _list[Math.Max(0, _index - 1)] : _list.FirstOrDefault();
    }
}

// 2. 카드 삭제 액션
public class RemoveCommandAction : IUndoableAction
{
    private readonly ObservableCollection<FlowCommand> _list;
    private readonly FlowCommand _targetNode;
    private readonly int _originalIndex;
    private readonly FlowEditorViewModel _flowVm;

    public string Description => $"연출 명령어 삭제: {_targetNode.Opcode}";

    public RemoveCommandAction(ObservableCollection<FlowCommand> list, FlowCommand targetNode, int originalIndex, FlowEditorViewModel flowVm)
    {
        _list = list;
        _targetNode = targetNode;
        _originalIndex = originalIndex;
        _flowVm = flowVm;
    }

    public void Execute()
    {
        _list.Remove(_targetNode);
        if (_flowVm.SelectedCommand == _targetNode)
        {
            _flowVm.SelectedCommand = (_list.Count > 0) ? _list[Math.Max(0, _originalIndex - 1)] : null;
        }
    }

    public void Undo()
    {
        _list.Insert(_originalIndex, _targetNode);
        _flowVm.SelectedCommand = _targetNode;
    }
}

// 3. 카드 상/하 이동 액션
public class MoveCommandAction : IUndoableAction
{
    private readonly ObservableCollection<FlowCommand> _list;
    private readonly FlowCommand _targetNode;
    private readonly int _fromIndex;
    private readonly int _toIndex;
    private readonly FlowEditorViewModel _flowVm;

    public string Description => $"연출 명령어 이동: {_targetNode.Opcode} ({_fromIndex} -> {_toIndex})";

    public MoveCommandAction(ObservableCollection<FlowCommand> list, FlowCommand targetNode, int fromIndex, int toIndex, FlowEditorViewModel flowVm)
    {
        _list = list;
        _targetNode = targetNode;
        _fromIndex = fromIndex;
        _toIndex = toIndex;
        _flowVm = flowVm;
    }

    public void Execute()
    {
        _list.Move(_fromIndex, _toIndex);
        _flowVm.SelectedCommand = _targetNode;
    }

    public void Undo()
    {
        _list.Move(_toIndex, _fromIndex);
        _flowVm.SelectedCommand = _targetNode;
    }
}

// 4. 카드 유형 즉석 변환 액션
public class ConvertCommandAction : IUndoableAction
{
    private readonly ObservableCollection<FlowCommand> _list;
    private readonly FlowCommand _oldNode;
    private readonly FlowCommand _newNode;
    private readonly int _index;
    private readonly FlowEditorViewModel _flowVm;

    public string Description => $"연출 명령어 변환: {_oldNode.Opcode} -> {_newNode.Opcode}";

    public ConvertCommandAction(ObservableCollection<FlowCommand> list, FlowCommand oldNode, FlowCommand newNode, int index, FlowEditorViewModel flowVm)
    {
        _list = list;
        _oldNode = oldNode;
        _newNode = newNode;
        _index = index;
        _flowVm = flowVm;
    }

    public void Execute()
    {
        _list[_index] = _newNode;
        _flowVm.SelectedCommand = _newNode;
    }

    public void Undo()
    {
        _list[_index] = _oldNode;
        _flowVm.SelectedCommand = _oldNode;
    }
}
