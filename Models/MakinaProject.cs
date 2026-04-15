using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MakinaEditor.Models;

// 화면 설계(UI)에서 만들어질 껍데기 레이아웃 데이터
public class UILayoutTemplate
{
    public required string TemplateId { get; set; } // 예: "DialogBox_Style_A", "MainMenu"
    // (여기에 위치 X/Y, 크기, 이미지 경로 등의 데이터가 들어감)
}

public class MakinaProject
{
    public string ProjectName { get; set; }

    // 1️⃣ 화면 설계 에디터가 뱉어내는 UI 템플릿들 (Global)
    public Dictionary<string, UILayoutTemplate> ScreenTemplates { get; }

    // 2️⃣ 시나리오 흐름 제어 에디터가 뱉어내는 커널 로직들 (Macro)
    public Dictionary<string, ObservableCollection<ScenarioCommand>> KernelRoutines { get; }

    // 3️⃣ 플로우 작성 에디터가 뱉어내는 렌더링 로직들 (Micro)
    public Dictionary<string, ObservableCollection<FlowCommand>> UserFlows { get; }

    public MakinaProject(string name)
    {
        ProjectName = name;
        ScreenTemplates = new Dictionary<string, UILayoutTemplate>();
        KernelRoutines = new Dictionary<string, ObservableCollection<ScenarioCommand>>();
        UserFlows = new Dictionary<string, ObservableCollection<FlowCommand>>();
    }
}