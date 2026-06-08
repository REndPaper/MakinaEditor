using System.Collections.Generic;

namespace MakinaEditor.Models;

public class ProjectMetadata
{
    public string Name { get; set; } = "New Project";
    public string Version { get; set; } = "1.0.0";
    public List<ResourceObject> Resources { get; set; } = new();
}