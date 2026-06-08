using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MakinaEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace MakinaEditor.Views;

public partial class ResourceEditWindow : Window
{
    private readonly ResourceObject? _editingResource;
    private readonly string _projectPath;
    private readonly bool _isEditMode;

    // 포즈 데이터 바인딩용
    private readonly ObservableCollection<PoseEntry> _poseList = new();

    public ResourceObject? ResultResource { get; private set; }

    public ResourceEditWindow()
    {
        InitializeComponent();
        _projectPath = "";
    }

    public ResourceEditWindow(string projectPath, ResourceObject? editingResource = null)
    {
        InitializeComponent();
        _projectPath = projectPath;
        _editingResource = editingResource;
        _isEditMode = editingResource != null;

        // ComboBox 설정
        ResourceTypeComboBox.ItemsSource = Enum.GetValues<ResourceType>();
        ResourceTypeComboBox.SelectedItem = ResourceType.Background;

        // 포즈 리스트 박스 소스 바인딩
        PosesListBox.ItemsSource = _poseList;

        // 타입 변경에 따른 입력 판넬 가시성 제어
        ResourceTypeComboBox.SelectionChanged += (s, e) => UpdatePanelVisibilities();

        if (_isEditMode && _editingResource != null)
        {
            TitleTextBlock.Text = $"리소스 객체 수정 ({_editingResource.Id})";
            ResourceIdTextBox.Text = _editingResource.Id;
            ResourceIdTextBox.IsEnabled = false; // 수정 시 ID는 변경 불가
            ResourceTypeComboBox.SelectedItem = _editingResource.Type;
            ResourceTypeComboBox.IsEnabled = false; // 타입 수정 불가

            if (_editingResource.Type == ResourceType.Background)
            {
                FilePathTextBox.Text = _editingResource.FilePath;
            }
            else
            {
                foreach (var kvp in _editingResource.Variations)
                {
                    _poseList.Add(new PoseEntry(kvp.Key, kvp.Value));
                }
            }
        }
        else
        {
            TitleTextBlock.Text = "새 리소스 객체 추가";
        }

        UpdatePanelVisibilities();
    }

    private void UpdatePanelVisibilities()
    {
        var type = (ResourceType)(ResourceTypeComboBox.SelectedItem ?? ResourceType.Background);
        BackgroundPathPanel.IsVisible = (type == ResourceType.Background);
        CharacterPosePanel.IsVisible = (type == ResourceType.Character);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void SelectFile_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "배경 이미지 선택",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
        {
            string fullPath = files[0].Path.LocalPath;
            string relPath = Path.GetRelativePath(_projectPath, fullPath).Replace('\\', '/');
            FilePathTextBox.Text = relPath;

            // 추가 모드이고 ID가 비어있다면 파일명 제안
            if (!_isEditMode && string.IsNullOrWhiteSpace(ResourceIdTextBox.Text))
            {
                ResourceIdTextBox.Text = Path.GetFileNameWithoutExtension(fullPath);
            }
        }
    }

    private async void SelectPoseFile_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "포즈 이미지 선택",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
        {
            string fullPath = files[0].Path.LocalPath;
            string relPath = Path.GetRelativePath(_projectPath, fullPath).Replace('\\', '/');
            PosePathTextBox.Text = relPath;

            // 포즈 이름 자동 제안
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            var parts = fileName.Split('_');
            if (parts.Length >= 3 && (parts[0].Equals("char", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("character", StringComparison.OrdinalIgnoreCase)))
            {
                PoseNameTextBox.Text = string.Join('_', parts, 2, parts.Length - 2);
            }
            else if (parts.Length >= 2)
            {
                PoseNameTextBox.Text = string.Join('_', parts, 1, parts.Length - 1);
            }
            else
            {
                PoseNameTextBox.Text = fileName;
            }
        }
    }

    private void AddPose_Click(object? sender, RoutedEventArgs e)
    {
        string poseName = PoseNameTextBox.Text?.Trim() ?? "";
        string posePath = PosePathTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(poseName) || string.IsNullOrEmpty(posePath))
        {
            return;
        }

        // 중복 포즈 체크 및 업데이트
        var existing = _poseList.FirstOrDefault(x => x.Key.Equals(poseName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _poseList.Remove(existing);
        }

        _poseList.Add(new PoseEntry(poseName, posePath));
        PoseNameTextBox.Text = "";
        PosePathTextBox.Text = "";
    }

    private void DeletePose_Click(object? sender, RoutedEventArgs e)
    {
        if (PosesListBox.SelectedItem is PoseEntry selectedEntry)
        {
            _poseList.Remove(selectedEntry);
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        string id = ResourceIdTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        var type = (ResourceType)(ResourceTypeComboBox.SelectedItem ?? ResourceType.Background);
        var res = _editingResource ?? new ResourceObject();
        res.Id = id;
        res.Type = type;

        if (type == ResourceType.Background)
        {
            string path = FilePathTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(path)) return;
            res.FilePath = path;
            res.Variations.Clear();
        }
        else
        {
            res.FilePath = "";
            res.Variations.Clear();
            foreach (var item in _poseList)
            {
                res.Variations[item.Key] = item.Value;
            }
        }

        ResultResource = res;
        Close(ResultResource);
    }
}
