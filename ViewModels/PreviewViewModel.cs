using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Core;

namespace MakinaEditor.ViewModels;

public class PreviewViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly DispatcherTimer _previewTimer;
    private readonly Dictionary<string, Bitmap> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);

    // --- 프리뷰 상태 속성들 ---
    private string? _previewBg;
    public string? PreviewBg { get => _previewBg; set => this.RaiseAndSetIfChanged(ref _previewBg, value); }

    private Bitmap? _previewBgImage;
    public Bitmap? PreviewBgImage { get => _previewBgImage; set => this.RaiseAndSetIfChanged(ref _previewBgImage, value); }

    private string _previewSpeaker = "";
    public string PreviewSpeaker { get => _previewSpeaker; set => this.RaiseAndSetIfChanged(ref _previewSpeaker, value); }

    private string _previewText = "";
    public string PreviewText { get => _previewText; set => this.RaiseAndSetIfChanged(ref _previewText, value); }

    private string? _previewLeftChar;
    public string? PreviewLeftChar { get => _previewLeftChar; set => this.RaiseAndSetIfChanged(ref _previewLeftChar, value); }

    private Bitmap? _previewLeftCharImage;
    public Bitmap? PreviewLeftCharImage { get => _previewLeftCharImage; set => this.RaiseAndSetIfChanged(ref _previewLeftCharImage, value); }

    private string? _previewCenterChar;
    public string? PreviewCenterChar { get => _previewCenterChar; set => this.RaiseAndSetIfChanged(ref _previewCenterChar, value); }

    private Bitmap? _previewCenterCharImage;
    public Bitmap? PreviewCenterCharImage { get => _previewCenterCharImage; set => this.RaiseAndSetIfChanged(ref _previewCenterCharImage, value); }

    private string? _previewRightChar;
    public string? PreviewRightChar { get => _previewRightChar; set => this.RaiseAndSetIfChanged(ref _previewRightChar, value); }

    private Bitmap? _previewRightCharImage;
    public Bitmap? PreviewRightCharImage { get => _previewRightCharImage; set => this.RaiseAndSetIfChanged(ref _previewRightCharImage, value); }

    private string? _previewBgm;
    public string? PreviewBgm { get => _previewBgm; set => this.RaiseAndSetIfChanged(ref _previewBgm, value); }

    private string? _previewShader;
    public string? PreviewShader { get => _previewShader; set => this.RaiseAndSetIfChanged(ref _previewShader, value); }

    public bool IsPreviewPlaying => _previewTimer.IsEnabled;

    public PreviewViewModel(MainWindowViewModel main)
    {
        _main = main;
        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _previewTimer.Tick += PreviewTimer_Tick;
    }

    public void StopTimer()
    {
        _previewTimer.Stop();
        this.RaisePropertyChanged(nameof(IsPreviewPlaying));
    }

    public void ClearBitmapCache()
    {
        foreach (var bmp in _bitmapCache.Values)
        {
            bmp.Dispose();
        }
        _bitmapCache.Clear();
        
        PreviewBgImage = null;
        PreviewLeftCharImage = null;
        PreviewCenterCharImage = null;
        PreviewRightCharImage = null;
    }

    private Bitmap? GetCachedBitmap(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        if (_bitmapCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            var bmp = new Bitmap(path);
            _bitmapCache[path] = bmp;
            return bmp;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load bitmap {path}: {ex.Message}");
            return null;
        }
    }

    private Bitmap? LoadAssetBitmap(string? assetId)
    {
        if (string.IsNullOrEmpty(assetId)) return null;
        if (string.IsNullOrEmpty(_main.Project.CurrentProjectPath)) return null;

        // 1. 등록된 리소스 객체(ResourceObject)에서 검색
        var res = _main.Assets.ProjectResources.FirstOrDefault(x => x.Id.Equals(assetId, StringComparison.OrdinalIgnoreCase));
        string? targetPath = null;
        if (res != null)
        {
            if (res.Type == ResourceType.Background)
            {
                targetPath = Path.Combine(_main.Project.CurrentProjectPath, res.FilePath);
            }
            else
            {
                var defaultPath = res.Variations.TryGetValue("default", out var dp) ? dp : res.Variations.Values.FirstOrDefault();
                if (!string.IsNullOrEmpty(defaultPath))
                {
                    targetPath = Path.Combine(_main.Project.CurrentProjectPath, defaultPath);
                }
            }
        }
        
        // 2. 만약 등록된 리소스가 없으면, 기존의 파일명 라벨(AssetRegistry)에서 검색
        if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
        {
            if (_main.Assets.AssetRegistry.TryGetValue(assetId, out var path))
            {
                targetPath = path;
            }
        }

        return GetCachedBitmap(targetPath);
    }

    private Bitmap? FindCharacterBitmap(string? charId, string? pose)
    {
        if (string.IsNullOrEmpty(charId) || charId == "Hide" || pose == "Hide") return null;
        if (string.IsNullOrEmpty(_main.Project.CurrentProjectPath)) return null;

        // 1. 등록된 리소스에서 캐릭터 탐색
        var res = _main.Assets.ProjectResources.FirstOrDefault(x => x.Type == ResourceType.Character && x.Id.Equals(charId, StringComparison.OrdinalIgnoreCase));
        if (res != null)
        {
            string? relPath = null;
            if (!string.IsNullOrEmpty(pose) && res.Variations.TryGetValue(pose, out var path))
            {
                relPath = path;
            }
            else if (res.Variations.TryGetValue("default", out var defPath))
            {
                relPath = defPath;
            }
            else if (res.Variations.Count > 0)
            {
                relPath = res.Variations.Values.First();
            }

            if (!string.IsNullOrEmpty(relPath))
            {
                string fullPath = Path.Combine(_main.Project.CurrentProjectPath, relPath);
                var bmp = GetCachedBitmap(fullPath);
                if (bmp != null) return bmp;
            }
        }

        // 2. 하위 호환 폴백 로직
        var fallbackBmp = LoadAssetBitmap($"char_{charId}_{pose}");
        if (fallbackBmp != null) return fallbackBmp;

        fallbackBmp = LoadAssetBitmap($"{charId}_{pose}");
        if (fallbackBmp != null) return fallbackBmp;

        fallbackBmp = LoadAssetBitmap($"char_{charId}_default");
        if (fallbackBmp != null) return fallbackBmp;

        fallbackBmp = LoadAssetBitmap($"{charId}_default");
        if (fallbackBmp != null) return fallbackBmp;

        fallbackBmp = LoadAssetBitmap($"char_{charId}");
        if (fallbackBmp != null) return fallbackBmp;

        return LoadAssetBitmap($"{charId}");
    }

    public void UpdatePreviewState()
    {
        // 프리뷰 모델 상태 초기화
        PreviewBg = null;
        PreviewBgImage = null;
        PreviewSpeaker = "";
        PreviewText = "";
        PreviewLeftChar = null;
        PreviewLeftCharImage = null;
        PreviewCenterChar = null;
        PreviewCenterCharImage = null;
        PreviewRightChar = null;
        PreviewRightCharImage = null;
        PreviewBgm = null;
        PreviewShader = null;

        var activeFlow = _main.Flow.ActiveUserFlow;
        if (activeFlow.Count == 0) return;

        var selectedCmd = _main.Flow.SelectedCommand;
        int targetIndex = selectedCmd != null ? activeFlow.IndexOf(selectedCmd) : activeFlow.Count - 1;
        if (targetIndex < 0) targetIndex = activeFlow.Count - 1;

        for (int i = 0; i <= targetIndex; i++)
        {
            var cmd = activeFlow[i];
            if (cmd is BgCommand bg)
            {
                PreviewBg = bg.AssetId;
                PreviewBgImage = LoadAssetBitmap(bg.AssetId);
                if (PreviewBgImage == null && !string.IsNullOrEmpty(bg.AssetId))
                {
                    PreviewBgImage = LoadAssetBitmap($"bg_{bg.AssetId}");
                }
            }
            else if (cmd is ShowCharCommand ch)
            {
                string charDisplay = string.IsNullOrEmpty(ch.Pose) ? (ch.CharacterId ?? "") : $"{ch.CharacterId} ({ch.Pose})";
                var charBmp = FindCharacterBitmap(ch.CharacterId, ch.Pose);

                if (ch.Position == "Left")
                {
                    bool isHide = ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId);
                    PreviewLeftChar = isHide ? null : charDisplay;
                    PreviewLeftCharImage = isHide ? null : charBmp;
                }
                else if (ch.Position == "Center")
                {
                    bool isHide = ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId);
                    PreviewCenterChar = isHide ? null : charDisplay;
                    PreviewCenterCharImage = isHide ? null : charBmp;
                }
                else if (ch.Position == "Right")
                {
                    bool isHide = ch.CharacterId == "Hide" || ch.Pose == "Hide" || string.IsNullOrEmpty(ch.CharacterId);
                    PreviewRightChar = isHide ? null : charDisplay;
                    PreviewRightCharImage = isHide ? null : charBmp;
                }
                else if (ch.Position == "Hide" || ch.CharacterId == "Hide" || ch.Pose == "Hide")
                {
                    if (PreviewLeftChar != null && PreviewLeftChar.StartsWith(ch.CharacterId ?? "---"))
                    {
                        PreviewLeftChar = null;
                        PreviewLeftCharImage = null;
                    }
                    if (PreviewCenterChar != null && PreviewCenterChar.StartsWith(ch.CharacterId ?? "---"))
                    {
                        PreviewCenterChar = null;
                        PreviewCenterCharImage = null;
                    }
                    if (PreviewRightChar != null && PreviewRightChar.StartsWith(ch.CharacterId ?? "---"))
                    {
                        PreviewRightChar = null;
                        PreviewRightCharImage = null;
                    }
                }
            }
            else if (cmd is PlayBgmCommand bgm)
            {
                PreviewBgm = bgm.AssetId;
            }
            else if (cmd is ShaderCommand sh)
            {
                PreviewShader = $"{sh.ShaderId} (강도: {sh.Intensity})";
            }
            else if (cmd is TextCommand txt)
            {
                PreviewSpeaker = txt.Speaker ?? "";
                PreviewText = txt.TextContent ?? "";
            }
        }
    }

    public void SelectPrevCommand()
    {
        var activeFlow = _main.Flow.ActiveUserFlow;
        if (activeFlow.Count == 0) return;
        var selectedCmd = _main.Flow.SelectedCommand;

        if (selectedCmd == null)
        {
            _main.Flow.SelectedCommand = activeFlow.LastOrDefault();
            return;
        }
        int index = activeFlow.IndexOf(selectedCmd);
        if (index > 0)
        {
            _main.Flow.SelectedCommand = activeFlow[index - 1];
        }
    }

    public void SelectNextCommand()
    {
        var activeFlow = _main.Flow.ActiveUserFlow;
        if (activeFlow.Count == 0) return;
        var selectedCmd = _main.Flow.SelectedCommand;

        if (selectedCmd == null)
        {
            _main.Flow.SelectedCommand = activeFlow.FirstOrDefault();
            return;
        }
        int index = activeFlow.IndexOf(selectedCmd);
        if (index < activeFlow.Count - 1)
        {
            _main.Flow.SelectedCommand = activeFlow[index + 1];
        }
    }

    public void TogglePlayPreview()
    {
        var activeFlow = _main.Flow.ActiveUserFlow;
        if (activeFlow.Count == 0) return;

        if (_previewTimer.IsEnabled)
        {
            _previewTimer.Stop();
            _main.StatusText = "프리뷰 자동 재생 일시 정지.";
        }
        else
        {
            var selectedCmd = _main.Flow.SelectedCommand;
            if (selectedCmd == null || activeFlow.IndexOf(selectedCmd) >= activeFlow.Count - 1)
            {
                _main.Flow.SelectedCommand = activeFlow.FirstOrDefault();
            }
            _previewTimer.Start();
            _main.StatusText = "프리뷰 자동 재생 중...";
        }
        this.RaisePropertyChanged(nameof(IsPreviewPlaying));
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        var activeFlow = _main.Flow.ActiveUserFlow;
        var selectedCmd = _main.Flow.SelectedCommand;

        if (selectedCmd == null)
        {
            _main.Flow.SelectedCommand = activeFlow.FirstOrDefault();
            return;
        }

        int currentIndex = activeFlow.IndexOf(selectedCmd);
        if (currentIndex < activeFlow.Count - 1)
        {
            _main.Flow.SelectedCommand = activeFlow[currentIndex + 1];
        }
        else
        {
            _previewTimer.Stop();
            this.RaisePropertyChanged(nameof(IsPreviewPlaying));
            _main.StatusText = "프리뷰 자동 재생 완료.";
        }
    }
}
