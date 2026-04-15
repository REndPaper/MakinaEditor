using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MakinaEditor.ViewModels;

namespace MakinaEditor.Views;

public partial class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        InitializeComponent();
    }

    // 🎯 이 메서드가 없어서 에러가 났던 겁니다!
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
}