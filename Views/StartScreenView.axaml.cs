using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MakinaEditor.ViewModels;

namespace MakinaEditor.Views;

public partial class StartScreenView : UserControl
{
    public StartScreenView()
    {
        InitializeComponent();
    }

    // 📂 폴더 열기 버튼 핸들러
    public async void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "마키나 프로젝트 폴더 선택",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            await vm.OpenProjectFolder(folders[0].Path.LocalPath);
        }
    }

    public async void CreateProject_Click(object? sender, RoutedEventArgs e)
{
    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel == null) return;

    // 프로젝트를 생성할 '부모 폴더' 선택
    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
        Title = "새 프로젝트를 생성할 위치 선택",
        AllowMultiple = false
    });

    if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
    {
        // 💡 실제 생성 로직은 뷰모델에 맡깁니다.
        await vm.CreateNewProject(folders[0].Path.LocalPath);
    }
}
}