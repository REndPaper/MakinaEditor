using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using ReactiveUI;
using MakinaEditor.ViewModels;

namespace MakinaEditor.Models;

// 노드 그래프에서 생성될 '매크로' 명령어
public class ScenarioCommand 
{
    public Core.ScenarioOpcode Opcode { get; set; }
    
    // 파라미터 (JUMP할 타겟 Flow의 ID나 EVAL할 조건식)
    public string? TargetId { get; set; } 
    public string? ConditionExpr { get; set; } 
}

// 부모 클래스 (C++ 런타임이 읽을 OP코드 타입만 가짐)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextCommand), typeDiscriminator: "text")]
[JsonDerivedType(typeof(BgCommand), typeDiscriminator: "bg")]
[JsonDerivedType(typeof(ShowCharCommand), typeDiscriminator: "char")]
[JsonDerivedType(typeof(PlayBgmCommand), typeDiscriminator: "bgm")]
[JsonDerivedType(typeof(ShaderCommand), typeDiscriminator: "shader")]
public abstract class FlowCommand : ReactiveObject
{
    public Core.FlowOpcode Opcode { get; protected set; }
}

// 1. 대사 출력 명령어
public class TextCommand : FlowCommand 
{
    private string? _speaker;
    public string? Speaker 
    {
        get => _speaker;
        set => this.RaiseAndSetIfChanged(ref _speaker, value);
    }
    
    private string? _textContent;
    public string? TextContent 
    {
        get => _textContent;
        set => this.RaiseAndSetIfChanged(ref _textContent, value);
    }

    public TextCommand() { Opcode = Core.FlowOpcode.PRINT_TEXT; }
}

// 2. 배경 설정 명령어
public class BgCommand : FlowCommand 
{
    private string? _assetId;
    public string? AssetId 
    {
        get => _assetId;
        set => this.RaiseAndSetIfChanged(ref _assetId, value);
    }

    public BgCommand() { Opcode = Core.FlowOpcode.SET_BG; }
}

// 3. 캐릭터 스탠딩 CG 출력 명령어
public class ShowCharCommand : FlowCommand
{
    private string? _characterId;
    public string? CharacterId 
    {
        get => _characterId;
        set 
        {
            this.RaiseAndSetIfChanged(ref _characterId, value);
            UpdateAvailablePoses();
        }
    }
    
    private string? _pose;
    public string? Pose 
    {
        get => _pose;
        set => this.RaiseAndSetIfChanged(ref _pose, value);
    }
    
    private string? _position;
    public string? Position 
    {
        get => _position;
        set => this.RaiseAndSetIfChanged(ref _position, value);
    }

    [JsonIgnore]
    public ObservableCollection<string> AvailablePoses { get; } = new();

    public void UpdateAvailablePoses()
    {
        AvailablePoses.Clear();
        if (string.IsNullOrEmpty(CharacterId)) return;

        var vm = MainWindowViewModel.Instance;
        if (vm != null)
        {
            var character = vm.ProjectResources.FirstOrDefault(x => x.Type == ResourceType.Character && x.Id.Equals(CharacterId, StringComparison.OrdinalIgnoreCase));
            if (character != null)
            {
                foreach (var poseKey in character.Variations.Keys)
                {
                    AvailablePoses.Add(poseKey);
                }
            }
        }
    }

    public ShowCharCommand() 
    { 
        Opcode = Core.FlowOpcode.SHOW_CHAR; 
        UpdateAvailablePoses();
    }
}

// 4. BGM 재생 명령어
public class PlayBgmCommand : FlowCommand
{
    private string? _assetId;
    public string? AssetId 
    {
        get => _assetId;
        set => this.RaiseAndSetIfChanged(ref _assetId, value);
    }
    
    private float _volume = 1.0f;
    public float Volume 
    {
        get => _volume;
        set => this.RaiseAndSetIfChanged(ref _volume, value);
    }

    public PlayBgmCommand() { Opcode = Core.FlowOpcode.PLAY_BGM; }
}

// 5. 셰이더 적용 명령어
public class ShaderCommand : FlowCommand 
{
    private string? _shaderId;
    public string? ShaderId 
    {
        get => _shaderId;
        set => this.RaiseAndSetIfChanged(ref _shaderId, value);
    }
    
    private float _intensity;
    public float Intensity 
    {
        get => _intensity;
        set => this.RaiseAndSetIfChanged(ref _intensity, value);
    }

    public ShaderCommand() { Opcode = Core.FlowOpcode.APPLY_SHADER; }
}