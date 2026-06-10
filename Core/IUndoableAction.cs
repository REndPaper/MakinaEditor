namespace MakinaEditor.Core;

public interface IUndoableAction
{
    string Description { get; }
    void Execute();
    void Undo();
}
