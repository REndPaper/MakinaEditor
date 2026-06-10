using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace MakinaEditor.Models;

public enum VariableType
{
    String,
    Number,
    Boolean
}

public class VariableDefinition : ReactiveObject
{
    private string _name = "var_name";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private VariableType _type = VariableType.Boolean;
    public VariableType Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    private string _defaultValue = "false";
    public string DefaultValue
    {
        get => _defaultValue;
        set => this.RaiseAndSetIfChanged(ref _defaultValue, value);
    }
}

public class ProjectMetadata
{
    public string Name { get; set; } = "New Project";
    public string Version { get; set; } = "1.0.0";
    public List<ResourceObject> Resources { get; set; } = new();
    public ObservableCollection<VariableDefinition> Variables { get; set; } = new();
}