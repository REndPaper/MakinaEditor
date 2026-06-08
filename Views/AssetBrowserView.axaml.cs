using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MakinaEditor.Models;
using MakinaEditor.ViewModels;
using System.Linq;

namespace MakinaEditor.Views;

public partial class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        InitializeComponent();
    }

    public async void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        // 1. 현재 UserControl이 속한 최상위 레벨(Window)을 찾습니다.
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 2. OS 표준 폴더 선택기 호출
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "마키나 프로젝트 폴더 선택",
            AllowMultiple = false
        });

        // 3. 선택된 폴더가 있다면 뷰모델의 로직 실행
        if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            // 폴더 경로는 LocalPath로 가져옵니다.
            await vm.OpenProjectFolder(folders[0].Path.LocalPath);
        }
    }

    public async void AddResource_Click(object? sender, RoutedEventArgs e)
    {
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow == null || !(DataContext is MainWindowViewModel vm)) return;

        var dialog = new ResourceEditWindow(vm.CurrentProjectPath ?? "");
        var result = await dialog.ShowDialog<ResourceObject>(parentWindow);

        if (result != null)
        {
            vm.AddOrUpdateResource(result);
        }
    }

    public void DeleteResource_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.DeleteResourceCommand();
        }
    }

    public async void ResourceList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow == null || !(DataContext is MainWindowViewModel vm)) return;

        if (ResourceListBox.SelectedItem is ResourceObject selectedRes)
        {
            var clone = new ResourceObject
            {
                Id = selectedRes.Id,
                Type = selectedRes.Type,
                FilePath = selectedRes.FilePath,
                Variations = new System.Collections.Generic.Dictionary<string, string>(selectedRes.Variations)
            };

            var dialog = new ResourceEditWindow(vm.CurrentProjectPath ?? "", clone);
            var result = await dialog.ShowDialog<ResourceObject>(parentWindow);

            if (result != null)
            {
                vm.AddOrUpdateResource(result);
            }
        }
    }
}