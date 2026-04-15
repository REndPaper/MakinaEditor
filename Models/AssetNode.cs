namespace MakinaEditor.Models;

public enum AssetType { Folder, Image, Audio, Script, Unknown }

public class AssetNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public AssetType Type { get; set; }
    
    public System.Collections.ObjectModel.ObservableCollection<AssetNode> Children { get; } = new();

    // 🎯 XAML 바인딩을 위한 '꿀' 속성 추가
    public bool IsFolder => Type == AssetType.Folder;
    public bool IsNotFolder => !IsFolder; // 파일 아이콘용

    public AssetNode(string path)
    {
        FullPath = path;
        Name = System.IO.Path.GetFileName(path);
        
        if (System.IO.Directory.Exists(path))
        {
            Type = AssetType.Folder;
        }
        else
        {
            string ext = System.IO.Path.GetExtension(path).ToLower();
            Type = ext switch
            {
                ".png" or ".jpg" or ".webp" => AssetType.Image,
                ".wav" or ".mp3" or ".ogg" => AssetType.Audio,
                ".json" or ".makina" => AssetType.Script,
                _ => AssetType.Unknown
            };
        }
    }
}