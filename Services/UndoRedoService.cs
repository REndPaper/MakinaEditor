using System;
using System.Collections.Generic;
using ReactiveUI;
using MakinaEditor.Core;

namespace MakinaEditor.Services;

public class UndoRedoService : ReactiveObject
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Execute(IUndoableAction action)
    {
        action.Execute();
        _undoStack.Push(action);
        _redoStack.Clear();
        
        UpdateProperties();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);

        UpdateProperties();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);

        UpdateProperties();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();

        UpdateProperties();
    }

    private void UpdateProperties()
    {
        this.RaisePropertyChanged(nameof(CanUndo));
        this.RaisePropertyChanged(nameof(CanRedo));
    }
}
