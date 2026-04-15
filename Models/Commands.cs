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
public abstract class FlowCommand 
{
    public Core.FlowOpcode Opcode { get; protected set; }
}

// 1. 대사 출력 명령어
public class TextCommand : FlowCommand 
{
    public string? Speaker { get; set; }
    public string? TextContent { get; set; }

    public TextCommand() { Opcode = Core.FlowOpcode.PRINT_TEXT; }
}

// 2. 배경 설정 명령어
public class BgCommand : FlowCommand 
{
    public string? AssetId { get; set; }

    public BgCommand() { Opcode = Core.FlowOpcode.SET_BG; }
}

// 3. 셰이더 적용 명령어
public class ShaderCommand : FlowCommand 
{
    public string? ShaderId { get; set; }
    public float Intensity { get; set; }

    public ShaderCommand() { Opcode = Core.FlowOpcode.APPLY_SHADER; }
}