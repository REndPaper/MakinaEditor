using System;
using System.IO;
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

        if (folders.Count > 0 && DataContext is ProjectViewModel vm)
        {
            await vm.OpenProjectFolder(folders[0].Path.LocalPath);
        }
    }

    public async void CreateProject_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var parentWindow = topLevel as Window;
            if (parentWindow == null) return;

            var dialog = new NewProjectWindow();
            var result = await dialog.ShowDialog<bool>(parentWindow);

            if (result && DataContext is ProjectViewModel vm)
            {
                await vm.CreateNewProject(dialog.ResultProjectPath, dialog.ResultProjectName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in CreateProject_Click: {ex}");
        }
    }
}