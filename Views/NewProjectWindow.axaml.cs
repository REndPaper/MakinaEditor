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
        
        // 기본 저장 위치 설정 (안전하게 BaseDirectory 사용)
        try
        {
            string defaultPath = AppDomain.CurrentDomain.BaseDirectory;
            var locationTextBox = this.FindControl<TextBox>("LocationTextBox");
            if (locationTextBox != null)
            {
                locationTextBox.Text = defaultPath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in constructor: {ex}");
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
        var errorTextBlock = this.FindControl<TextBlock>("ErrorTextBlock");

        if (errorTextBlock != null)
        {
            errorTextBlock.Text = string.Empty;
        }

        string name = nameTextBox?.Text?.Trim() ?? string.Empty;
        string location = locationTextBox?.Text?.Trim() ?? string.Empty;

        // 폴더 이름에 사용될 수 없는 문자 검증
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(invalidChars) >= 0)
        {
            if (errorTextBlock != null)
            {
                errorTextBlock.Text = "⚠️ 올바르지 않은 프로젝트 이름입니다. (특수문자 제외)";
            }
            return;
        }

        if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
        {
            if (errorTextBlock != null)
            {
                errorTextBlock.Text = "⚠️ 유효하지 않은 저장 경로입니다.";
            }
            return;
        }

        string fullPath = Path.Combine(location, name);
        if (Directory.Exists(fullPath))
        {
            if (errorTextBlock != null)
            {
                errorTextBlock.Text = "⚠️ 지정된 경로에 동일한 이름의 폴더가 이미 존재합니다.";
            }
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
