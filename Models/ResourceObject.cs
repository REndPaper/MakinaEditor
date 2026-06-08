using System.Collections.Generic;
using System.Linq;

namespace MakinaEditor.Models;

public enum ResourceType 
{ 
    Background, 
    Character 
}

public class ResourceObject
{
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public Dictionary<string, string> Variations { get; set; } = new();
    public ResourceType Type { get; set; }

    // UI 바인딩용 도우미
    public string Icon => Type == ResourceType.Background ? "🖼️" : "👤";

    public string DisplayName 
    {
        get
        {
            if (Type == ResourceType.Background)
            {
                return $"{Id} ({FilePath})";
            }
            else
            {
                var variationsStr = string.Join(", ", Variations.Keys);
                return $"{Id} [{variationsStr}]";
            }
        }
    }
}

public class PoseEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public PoseEntry() { }
    public PoseEntry(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
