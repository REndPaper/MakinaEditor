using System.Collections.ObjectModel;
using System.IO;
using MakinaEditor.Models;
using Avalonia.Threading;

namespace MakinaEditor.Services;

public static class AssetService
{
    public static void ScanDirectory(string path, ObservableCollection<AssetNode> collection)
    {
        var dirInfo = new DirectoryInfo(path);

        foreach (var dir in dirInfo.GetDirectories())
        {
            if (dir.Name.StartsWith(".") || dir.Name == "bin" || dir.Name == "obj") continue;
            var node = new AssetNode(dir.FullName);
            Dispatcher.UIThread.Post(() => collection.Add(node));
            ScanDirectory(dir.FullName, node.Children);
        }

        foreach (var file in dirInfo.GetFiles())
        {
            var node = new AssetNode(file.FullName);
            Dispatcher.UIThread.Post(() => collection.Add(node));
        }
    }
}