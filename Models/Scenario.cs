using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace MakinaEditor.Models;

public class MakinaScenario
{
    public string ScenarioName { get; set; } // 예: "ep_01_intro"

    // 🌐 [커널 메모리 영역] 노드 그래프 UI와 바인딩 됨
    // Key: 매크로 블록 ID (예: "scene_01_logic")
    // Value: 흐름 제어 OP코드 배열
    public Dictionary<string, ObservableCollection<ScenarioCommand>> KernelRoutines { get; }

    // 🎬 [유저 메모리 영역] 더블클릭 시 타임라인 UI와 바인딩 됨
    // Key: 연출 플로우 ID (예: "flow_01_dialogue")
    // Value: 화면 연출 OP코드 배열
    [JsonIgnore]
    public Dictionary<string, ObservableCollection<FlowCommand>> UserFlows { get; }

    public MakinaScenario()
    {
        ScenarioName = "Untitled";
        KernelRoutines = new Dictionary<string, ObservableCollection<ScenarioCommand>>();
        UserFlows = new Dictionary<string, ObservableCollection<FlowCommand>>();
    }

    public MakinaScenario(string name) : this()
    {
        ScenarioName = name;
    }
}