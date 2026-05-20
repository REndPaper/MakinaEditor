using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using MakinaEditor.Models;
using Avalonia.Threading;

namespace MakinaEditor.Services;

public static class AssetService
{
    /// <summary>
    /// 백그라운드 스레드에서 안전하게 디렉터리를 스캔하여 트리 노드를 생성합니다.
    /// </summary>
    public static List<AssetNode> ScanDirectoryNonBlocking(string path)
    {
        var list = new List<AssetNode>();
        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists) return list;

            // 1. 디렉터리 탐색
            foreach (var dir in dirInfo.GetDirectories())
            {
                if (dir.Name.StartsWith(".") || dir.Name == "bin" || dir.Name == "obj" || (dir.Attributes & FileAttributes.Hidden) != 0) 
                    continue;

                try
                {
                    var node = new AssetNode(dir.FullName);
                    var subChildren = ScanDirectoryNonBlocking(dir.FullName);
                    foreach (var child in subChildren)
                    {
                        node.Children.Add(child);
                    }
                    list.Add(node);
                }
                catch (UnauthorizedAccessException) { /* 특정 하위 폴더 접근 제한 시 무시 */ }
            }

            // 2. 파일 탐색
            foreach (var file in dirInfo.GetFiles())
            {
                if (file.Name.StartsWith(".") || (file.Attributes & FileAttributes.Hidden) != 0)
                    continue;

                var node = new AssetNode(file.FullName);
                list.Add(node);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 폴더 접근 권한이 없는 경우 무시
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Asset scan error on path {path}: {ex.Message}");
        }

        return list;
    }

    /// <summary>
    /// 지정된 경로의 에셋을 비동기적으로 스캔하여 컬렉션에 채웁니다.
    /// </summary>
    public static async Task ScanDirectoryAsync(string path, ObservableCollection<AssetNode> collection)
    {
        // 백그라운드 스레드에서 트리 노드 구조 구축
        var nodes = await Task.Run(() => ScanDirectoryNonBlocking(path));

        // UI 스레드에서 컬렉션 갱신
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            collection.Clear();
            foreach (var node in nodes)
            {
                collection.Add(node);
            }
        });
    }
}