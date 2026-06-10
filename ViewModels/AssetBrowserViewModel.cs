using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Services;
using MakinaEditor.Core;

namespace MakinaEditor.ViewModels;

public class AssetBrowserViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<AssetNode> ProjectAssets { get; } = new();
    public Dictionary<string, string> AssetRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ObservableCollection<ResourceObject> ProjectResources { get; } = new();
    public ObservableCollection<string> RegisteredBgs { get; } = new();
    public ObservableCollection<string> RegisteredCharacters { get; } = new();

    public ObservableCollection<string> this[string? characterId]
    {
        get
        {
            if (string.IsNullOrEmpty(characterId)) return new ObservableCollection<string>();
            var character = ProjectResources.FirstOrDefault(x => x.Type == ResourceType.Character && x.Id.Equals(characterId, StringComparison.OrdinalIgnoreCase));
            if (character != null)
            {
                return new ObservableCollection<string>(character.Variations.Keys);
            }
            return new ObservableCollection<string>();
        }
    }

    public ResourceType[] ResourceTypes => Enum.GetValues<ResourceType>();

    private string _newResourceIdInput = "";
    public string NewResourceIdInput
    {
        get => _newResourceIdInput;
        set => this.RaiseAndSetIfChanged(ref _newResourceIdInput, value);
    }

    private string _newResourcePathInput = "";
    public string NewResourcePathInput
    {
        get => _newResourcePathInput;
        set => this.RaiseAndSetIfChanged(ref _newResourcePathInput, value);
    }

    private string _newPoseNameInput = "";
    public string NewPoseNameInput
    {
        get => _newPoseNameInput;
        set => this.RaiseAndSetIfChanged(ref _newPoseNameInput, value);
    }

    private string _newPosePathInput = "";
    public string NewPosePathInput
    {
        get => _newPosePathInput;
        set => this.RaiseAndSetIfChanged(ref _newPosePathInput, value);
    }

    public ObservableCollection<KeyValuePair<string, string>> SelectedResourcePoses { get; } = new();

    public MainWindowViewModel Main => _main;

    private KeyValuePair<string, string>? _selectedPoseItem;
    public KeyValuePair<string, string>? SelectedPoseItem
    {
        get => _selectedPoseItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPoseItem, value);
            if (value != null)
            {
                NewPoseNameInput = value.Value.Key;
                NewPosePathInput = value.Value.Value;
            }
        }
    }

    public bool IsCharacterSelected => SelectedResource != null && SelectedResource.Type == ResourceType.Character;

    private ResourceType _newResourceTypeInput = ResourceType.Background;
    public ResourceType NewResourceTypeInput
    {
        get => _newResourceTypeInput;
        set => this.RaiseAndSetIfChanged(ref _newResourceTypeInput, value);
    }

    private ResourceObject? _selectedResource;
    public ResourceObject? SelectedResource
    {
        get => _selectedResource;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedResource, value);
            this.RaisePropertyChanged(nameof(IsCharacterSelected));
            UpdateSelectedResourcePosesList();

            if (value != null)
            {
                NewResourceIdInput = value.Id;
                NewResourceTypeInput = value.Type;
                NewResourcePathInput = value.Type == ResourceType.Background ? value.FilePath : "";
            }
            else
            {
                NewResourceIdInput = "";
                NewResourcePathInput = "";
            }
        }
    }

    private AssetNode? _selectedAssetNode;
    public AssetNode? SelectedAssetNode
    {
        get => _selectedAssetNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedAssetNode, value);
            if (value != null && value.Type == AssetType.Image && !string.IsNullOrEmpty(_main.Project.CurrentProjectPath))
            {
                string relPath = Path.GetRelativePath(_main.Project.CurrentProjectPath, value.FullPath).Replace('\\', '/');
                string fileName = Path.GetFileNameWithoutExtension(value.FullPath);
                
                // 캐릭터 객체 선택 시 포즈 추가 자동 제안
                if (IsCharacterSelected)
                {
                    NewPosePathInput = relPath;
                    
                    var parts = fileName.Split('_');
                    if (parts.Length >= 3 && (parts[0].Equals("char", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("character", StringComparison.OrdinalIgnoreCase)))
                    {
                        NewPoseNameInput = string.Join('_', parts, 2, parts.Length - 2);
                    }
                    else if (parts.Length >= 2)
                    {
                        NewPoseNameInput = string.Join('_', parts, 1, parts.Length - 1);
                    }
                    else
                    {
                        NewPoseNameInput = fileName;
                    }
                }
                else
                {
                    // 일반 자원 등록 제안
                    NewResourcePathInput = relPath;
                    
                    if (relPath.Contains("bg", StringComparison.OrdinalIgnoreCase) || relPath.Contains("background", StringComparison.OrdinalIgnoreCase))
                    {
                        NewResourceTypeInput = ResourceType.Background;
                        NewResourceIdInput = fileName;
                    }
                    else
                    {
                        NewResourceTypeInput = ResourceType.Character;
                        
                        var parts = fileName.Split('_');
                        if (parts.Length >= 3 && (parts[0].Equals("char", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("character", StringComparison.OrdinalIgnoreCase)))
                        {
                            NewResourceIdInput = parts[1];
                        }
                        else if (parts.Length >= 2 && (parts[0].Equals("bg", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("background", StringComparison.OrdinalIgnoreCase)))
                        {
                            NewResourceTypeInput = ResourceType.Background;
                            NewResourceIdInput = fileName;
                        }
                        else if (parts.Length >= 2)
                        {
                            NewResourceIdInput = parts[0];
                        }
                        else
                        {
                            NewResourceIdInput = fileName;
                        }
                    }
                }
            }
        }
    }

    public AssetBrowserViewModel(MainWindowViewModel main)
    {
        _main = main;
    }

    public async Task RefreshAssets()
    {
        if (string.IsNullOrEmpty(_main.Project.CurrentProjectPath)) return;

        _main.StatusText = "에셋 디렉터리를 스캔하는 중...";

        ProjectAssets.Clear();
        var rootNode = new AssetNode(_main.Project.CurrentProjectPath) { Name = _main.Project.ProjectName };
        await AssetService.ScanDirectoryAsync(_main.Project.CurrentProjectPath, rootNode.Children);
        ProjectAssets.Add(rootNode);

        // 에셋 라벨링 레지스트리 갱신
        AssetRegistry.Clear();
        BuildAssetRegistry(rootNode);

        // 비트맵 캐시 로딩 및 프리뷰 상태 업데이트
        _main.Preview.ClearBitmapCache(); // 캐시 갱신을 위해 이전 비트맵 해제
        _main.Preview.UpdatePreviewState();

        _main.StatusText = $"에셋 동기화 및 라벨링 완료. (총 {AssetRegistry.Count}개 에셋 식별)";
    }

    private void BuildAssetRegistry(AssetNode node)
    {
        if (node.Type == AssetType.Image || node.Type == AssetType.Audio)
        {
            string assetId = Path.GetFileNameWithoutExtension(node.FullPath);
            if (!string.IsNullOrEmpty(assetId))
            {
                AssetRegistry[assetId] = node.FullPath;
            }
        }

        foreach (var child in node.Children)
        {
            BuildAssetRegistry(child);
        }
    }

    public void RegisterResourceCommand()
    {
        if (string.IsNullOrWhiteSpace(NewResourceIdInput))
        {
            _main.StatusText = "리소스 ID를 입력해야 합니다.";
            return;
        }

        string id = NewResourceIdInput.Trim();
        string path = NewResourcePathInput.Trim();

        if (NewResourceTypeInput == ResourceType.Background && string.IsNullOrWhiteSpace(path))
        {
            _main.StatusText = "배경 리소스는 파일 경로를 입력해야 합니다.";
            return;
        }

        // 중복 체크 및 업데이트
        var existing = ProjectResources.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Type = NewResourceTypeInput;
            if (NewResourceTypeInput == ResourceType.Background)
            {
                existing.FilePath = path;
                existing.Variations.Clear();
                _main.StatusText = $"리소스 '{id}' 배경 수정 완료.";
            }
            else
            {
                existing.FilePath = "";
                _main.StatusText = $"리소스 '{id}' 캐릭터 설정 유지 완료.";
            }
        }
        else
        {
            var res = new ResourceObject { Id = id, Type = NewResourceTypeInput };
            if (NewResourceTypeInput == ResourceType.Background)
            {
                res.FilePath = path;
            }
            ProjectResources.Add(res);
            _main.StatusText = $"리소스 '{id}' 등록 완료.";
        }

        UpdateRegisteredResourcesList();
        
        NewResourceIdInput = "";
        NewResourcePathInput = "";
        SelectedResource = null;
        
        _ = _main.Project.SaveProject();
    }

    public void DeleteResourceCommand()
    {
        if (SelectedResource == null) return;
        
        string id = SelectedResource.Id;
        ProjectResources.Remove(SelectedResource);
        SelectedResource = null;
        
        UpdateRegisteredResourcesList();
        UpdateSelectedResourcePosesList();
        
        NewResourceIdInput = "";
        NewResourcePathInput = "";
        
        _main.StatusText = $"리소스 '{id}' 제거 완료.";
        
        _ = _main.Project.SaveProject();
    }

    public void UpdateSelectedResourcePosesList()
    {
        SelectedResourcePoses.Clear();
        if (SelectedResource != null && SelectedResource.Type == ResourceType.Character)
        {
            foreach (var kvp in SelectedResource.Variations)
            {
                SelectedResourcePoses.Add(kvp);
            }
        }
    }

    public void AddPoseCommand()
    {
        if (SelectedResource == null || SelectedResource.Type != ResourceType.Character)
        {
            _main.StatusText = "포즈를 추가할 캐릭터를 선택해야 합니다.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPoseNameInput) || string.IsNullOrWhiteSpace(NewPosePathInput))
        {
            _main.StatusText = "포즈 이름과 이미지 상대 경로를 입력해야 합니다.";
            return;
        }

        string pose = NewPoseNameInput.Trim();
        string path = NewPosePathInput.Trim();

        SelectedResource.Variations[pose] = path;
        _main.StatusText = $"캐릭터 '{SelectedResource.Id}'에 포즈 '{pose}' 추가 완료.";

        UpdateSelectedResourcePosesList();
        
        NewPoseNameInput = "";
        NewPosePathInput = "";

        // 인덱서 변경 통지 및 자동 저장
        this.RaisePropertyChanged("Item[]");
        _ = _main.Project.SaveProject();
    }

    public void DeletePoseCommand()
    {
        if (SelectedResource == null || SelectedResource.Type != ResourceType.Character)
        {
            return;
        }

        string? poseToDelete = null;
        if (SelectedPoseItem != null)
        {
            poseToDelete = SelectedPoseItem.Value.Key;
        }
        else if (!string.IsNullOrWhiteSpace(NewPoseNameInput))
        {
            poseToDelete = NewPoseNameInput.Trim();
        }

        if (string.IsNullOrEmpty(poseToDelete))
        {
            _main.StatusText = "삭제할 포즈를 선택하거나 이름을 입력해야 합니다.";
            return;
        }

        if (SelectedResource.Variations.Remove(poseToDelete))
        {
            _main.StatusText = $"캐릭터 '{SelectedResource.Id}'에서 포즈 '{poseToDelete}' 삭제 완료.";
            UpdateSelectedResourcePosesList();
            SelectedPoseItem = null;
            NewPoseNameInput = "";
            NewPosePathInput = "";
            
            // 인덱서 변경 통지 및 자동 저장
            this.RaisePropertyChanged("Item[]");
            _ = _main.Project.SaveProject();
        }
        else
        {
            _main.StatusText = $"포즈 '{poseToDelete}'를 찾을 수 없습니다.";
        }
    }

    public void UpdateRegisteredResourcesList()
    {
        RegisteredBgs.Clear();
        RegisteredCharacters.Clear();
        foreach (var r in ProjectResources)
        {
            if (r.Type == ResourceType.Background) RegisteredBgs.Add(r.Id);
            else RegisteredCharacters.Add(r.Id);
        }
        // 인덱서 갱신을 알리기 위해 Item[] 속성 변경 통지
        this.RaisePropertyChanged("Item[]");
        _main.Flow.RefreshAllShowCharCommandsPoses();
    }

    public void AddOrUpdateResource(ResourceObject res)
    {
        var existing = ProjectResources.FirstOrDefault(x => x.Id.Equals(res.Id, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Type = res.Type;
            existing.FilePath = res.FilePath;
            existing.Variations = res.Variations;
            _main.StatusText = $"리소스 '{res.Id}' 수정 완료.";
        }
        else
        {
            ProjectResources.Add(res);
            _main.StatusText = $"리소스 '{res.Id}' 등록 완료.";
        }

        UpdateRegisteredResourcesList();
        _ = _main.Project.SaveProject();
    }

    // 전역 변수 관리 관련 기능 추가
    public ObservableCollection<VariableDefinition> Variables => _main.Project.ProjectVariables;
    public VariableType[] VariableTypes => Enum.GetValues<VariableType>();

    public void AddVariableCommand()
    {
        string newName = $"var_{Variables.Count + 1}";
        var newVar = new VariableDefinition 
        { 
            Name = newName, 
            Type = VariableType.Boolean, 
            DefaultValue = "false" 
        };
        Variables.Add(newVar);
        _main.StatusText = $"새 전역 변수 '{newName}' 추가 완료.";
        _ = _main.Project.SaveProject();
    }

    public void DeleteVariableCommand(VariableDefinition target)
    {
        if (target == null) return;
        string name = target.Name;
        Variables.Remove(target);
        _main.StatusText = $"전역 변수 '{name}' 삭제 완료.";
        _ = _main.Project.SaveProject();
    }
}
