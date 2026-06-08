using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MakinaEditor.ViewModels;

namespace MakinaEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async void MenuOpenProject_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "마키나 프로젝트 폴더 선택",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            await vm.OpenProjectFolder(folders[0].Path.LocalPath);
        }
    }

    public async void MenuCreateProject_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new NewProjectWindow();
            var result = await dialog.ShowDialog<bool>(this);

            if (result && DataContext is MainWindowViewModel vm)
            {
                await vm.CreateNewProject(dialog.ResultProjectPath, dialog.ResultProjectName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in MenuCreateProject_Click: {ex}");
        }
    }
}