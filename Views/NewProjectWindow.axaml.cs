using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MakinaEditor.Views;

public partial class NewProjectWindow : Window
{
    public string ResultProjectName { get; private set; } = string.Empty;
    public string ResultProjectPath { get; private set; } = string.Empty;

    public NewProjectWindow()
    {
        InitializeComponent();
        
        // 기본 저장 위치 설정 (내 문서)
        string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var locationTextBox = this.FindControl<TextBox>("LocationTextBox");
        if (locationTextBox != null)
        {
            locationTextBox.Text = defaultPath;
        }
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var storageProvider = this.StorageProvider;
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "프로젝트가 저장될 상위 디렉터리 선택",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var locationTextBox = this.FindControl<TextBox>("LocationTextBox");
            if (locationTextBox != null)
            {
                locationTextBox.Text = folders[0].Path.LocalPath;
            }
        }
    }

    private void Create_Click(object? sender, RoutedEventArgs e)
    {
        var nameTextBox = this.FindControl<TextBox>("ProjectNameTextBox");
        var locationTextBox = this.FindControl<TextBox>("LocationTextBox");

        string name = nameTextBox?.Text?.Trim() ?? string.Empty;
        string location = locationTextBox?.Text?.Trim() ?? string.Empty;

        // 폴더 이름에 사용될 수 없는 문자 검증
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(invalidChars) >= 0)
        {
            return;
        }

        if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
        {
            return;
        }

        string fullPath = Path.Combine(location, name);
        if (Directory.Exists(fullPath))
        {
            return;
        }

        ResultProjectName = name;
        ResultProjectPath = location;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
