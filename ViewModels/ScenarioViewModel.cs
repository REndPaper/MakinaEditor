using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ReactiveUI;
using MakinaEditor.Models;
using MakinaEditor.Core;

namespace MakinaEditor.ViewModels;

public class ScenarioViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<string> Scenarios { get; } = new();

    private string? _selectedScenarioName;
    public string? SelectedScenarioName
    {
        get => _selectedScenarioName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedScenarioName, value);
            if (!string.IsNullOrEmpty(value))
            {
                _ = LoadScenario(value);
            }
        }
    }

    private string _newScenarioNameInput = "";
    public string NewScenarioNameInput
    {
        get => _newScenarioNameInput;
        set => this.RaiseAndSetIfChanged(ref _newScenarioNameInput, value);
    }

    private MakinaScenario _activeScenario = new("New Project");
    public MakinaScenario ActiveScenario
    {
        get => _activeScenario;
        set => this.RaiseAndSetIfChanged(ref _activeScenario, value);
    }

    public ScenarioViewModel(MainWindowViewModel main)
    {
        _main = main;
    }

    public async Task LoadScenario(string scenarioName)
    {
        if (string.IsNullOrEmpty(_main.Project.CurrentProjectPath)) return;

        string scenarioDir = Path.Combine(_main.Project.CurrentProjectPath, "Scenarios", scenarioName);
        string scenarioFilePath = Path.Combine(scenarioDir, $"{scenarioName}.json");
        bool migrated = false;

        // 하위 호환 마이그레이션: 레거시 단일 JSON 파일이 존재하면 폴더 구조로 전환
        if (!File.Exists(scenarioFilePath))
        {
            string legacyFilePath = Path.Combine(_main.Project.CurrentProjectPath, "Scenarios", $"{scenarioName}.json");
            if (File.Exists(legacyFilePath))
            {
                try
                {
                    var legacyJson = await File.ReadAllTextAsync(legacyFilePath);
                    using (var doc = JsonDocument.Parse(legacyJson))
                    {
                        var root = doc.RootElement;
                        Directory.CreateDirectory(scenarioDir);
                        Directory.CreateDirectory(Path.Combine(scenarioDir, "Flows"));

                        // 1. 시나리오 메타만 기록
                        await File.WriteAllTextAsync(scenarioFilePath, legacyJson);

                        // 2. Flows 하위 요소들을 개별 파일로 분사
                        if (root.TryGetProperty("UserFlows", out var userFlowsProp) && userFlowsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in userFlowsProp.EnumerateObject())
                            {
                                string flowId = prop.Name;
                                string flowJson = prop.Value.GetRawText();
                                string flowFile = Path.Combine(scenarioDir, "Flows", $"{flowId}.json");
                                await File.WriteAllTextAsync(flowFile, flowJson);
                            }
                        }
                    }

                    // 레거시 단일 파일 제거
                    File.Delete(legacyFilePath);
                    migrated = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Migration failed for {scenarioName}: {ex.Message}");
                }
            }
            else
            {
                return; // 파일이 없으므로 불러오기 중단
            }
        }

        try
        {
            // 1. 시나리오 메타데이터 로드
            var json = await File.ReadAllTextAsync(scenarioFilePath);
            var scenario = JsonSerializer.Deserialize<MakinaScenario>(json);
            if (scenario != null)
            {
                ActiveScenario = scenario;
                ActiveScenario.ScenarioName = scenarioName;

                // 2. 개별 Flow 리스트 수집
                ActiveScenario.UserFlows.Clear();
                string flowsDir = Path.Combine(scenarioDir, "Flows");
                if (Directory.Exists(flowsDir))
                {
                    var flowFiles = Directory.GetFiles(flowsDir, "*.json");
                    foreach (var file in flowFiles)
                    {
                        try
                        {
                            string flowId = Path.GetFileNameWithoutExtension(file);
                            var flowJson = await File.ReadAllTextAsync(file);
                            var flowCommands = JsonSerializer.Deserialize<ObservableCollection<FlowCommand>>(flowJson);
                            if (flowCommands != null)
                            {
                                ActiveScenario.UserFlows[flowId] = flowCommands;
                            }
                        }
                        catch (Exception flowEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to load flow {file}: {flowEx.Message}");
                        }
                    }
                }

                // 3. Available Flows 캐시 갱신
                _main.Flow.AvailableFlows.Clear();
                foreach (var flowKey in ActiveScenario.UserFlows.Keys)
                {
                    _main.Flow.AvailableFlows.Add(flowKey);
                }

                // 4. 만약 Flow가 비어있다면 flow_default 자동 추가
                if (_main.Flow.AvailableFlows.Count == 0)
                {
                    var defaultCmd = new TextCommand { Speaker = "시스템", TextContent = "새 플로우가 생성되었습니다." };
                    var defaultFlow = new ObservableCollection<FlowCommand> { defaultCmd };
                    ActiveScenario.UserFlows["flow_default"] = defaultFlow;
                    _main.Flow.AvailableFlows.Add("flow_default");
                }

                // 5. Flow 포커싱 전환
                _main.Flow.ClearSelectedFlowNameSilently(); // SelectedFlowName Setter 우회 초기화용 헬퍼 호출
                _main.Flow.SelectedFlowName = _main.Flow.AvailableFlows.Contains("flow_default") ? "flow_default" : _main.Flow.AvailableFlows.FirstOrDefault();

                _main.StatusText = migrated 
                    ? $"시나리오 '{scenarioName}' 마이그레이션 완료 및 로드됨."
                    : $"시나리오 '{scenarioName}' 로드됨.";
            }
        }
        catch (Exception ex)
        {
            _main.StatusText = $"시나리오 로드 중 오류 발생: {ex.Message}";
        }
    }

    public async Task CreateScenarioCommand()
    {
        if (string.IsNullOrEmpty(_main.Project.CurrentProjectPath)) return;
        if (string.IsNullOrWhiteSpace(NewScenarioNameInput))
        {
            _main.StatusText = "시나리오 이름을 입력해야 합니다.";
            return;
        }

        string scenarioName = NewScenarioNameInput.Trim();
        if (Scenarios.Contains(scenarioName))
        {
            _main.StatusText = "이미 존재하는 시나리오 이름입니다.";
            return;
        }

        try
        {
            string scenarioFolder = Path.Combine(_main.Project.CurrentProjectPath, "Scenarios");
            string scenarioDir = Path.Combine(scenarioFolder, scenarioName);
            Directory.CreateDirectory(scenarioDir);
            Directory.CreateDirectory(Path.Combine(scenarioDir, "Flows"));

            // 1. 새 시나리오 메타데이터 뼈대 생성
            var newScenario = new MakinaScenario(scenarioName);
            string scenarioFilePath = Path.Combine(scenarioDir, $"{scenarioName}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(scenarioFilePath, JsonSerializer.Serialize(newScenario, options));

            // 2. 기본 플로우 파일 생성
            var startText = new TextCommand { Speaker = "시스템", TextContent = $"{scenarioName} 시나리오가 성공적으로 시작되었습니다." };
            var defaultFlow = new ObservableCollection<FlowCommand> { startText };
            string flowFilePath = Path.Combine(scenarioDir, "Flows", "flow_default.json");
            await File.WriteAllTextAsync(flowFilePath, JsonSerializer.Serialize(defaultFlow, options));

            // 목록에 추가 후 포커싱
            Scenarios.Add(scenarioName);
            NewScenarioNameInput = "";
            SelectedScenarioName = scenarioName;

            _main.StatusText = $"새 시나리오 '{scenarioName}' 생성 완료.";
        }
        catch (Exception ex)
        {
            _main.StatusText = $"시나리오 생성 실패: {ex.Message}";
        }
    }

    public async Task DeleteScenarioCommand()
    {
        if (string.IsNullOrEmpty(_main.Project.CurrentProjectPath) || string.IsNullOrEmpty(SelectedScenarioName)) return;

        if (Scenarios.Count <= 1)
        {
            _main.StatusText = "최소 한 개 이상의 시나리오는 존재해야 합니다. 삭제할 수 없습니다.";
            return;
        }

        try
        {
            string targetName = SelectedScenarioName;
            string scenarioDir = Path.Combine(_main.Project.CurrentProjectPath, "Scenarios", targetName);

            if (Directory.Exists(scenarioDir))
            {
                Directory.Delete(scenarioDir, true);
            }

            Scenarios.Remove(targetName);
            SelectedScenarioName = Scenarios.FirstOrDefault();

            _main.StatusText = $"시나리오 '{targetName}' 삭제 완료.";
        }
        catch (Exception ex)
        {
            _main.StatusText = $"시나리오 삭제 실패: {ex.Message}";
        }
    }
}
