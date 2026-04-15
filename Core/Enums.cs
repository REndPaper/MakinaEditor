namespace MakinaEditor.Core;

public enum EditorMode { UI_Design, Scenario_Graph, Flow_Timeline }
public enum ScenarioOpcode 
{
    LOAD_SCENE,    // 새로운 라벨의 에셋들을 메모리에 프리로드 (Disk I/O 발생)
    UNLOAD_SCENE,  // 안 쓰는 메모리 해제
    EVAL_BRANCH,   // 전역 변수(호감도 등)를 평가해서 다음 Flow를 결정
    JUMP_FLOW,     // 다른 Flow 블록으로 컨텍스트 전환
    SAVE_STATE     // 세이브 파일용 스냅샷 생성
}

public enum FlowOpcode 
{
    PRINT_TEXT,    // 대사 출력
    SET_BG,        // 배경 변경
    SHOW_CHAR,     // 스탠딩 CG 출력
    APPLY_SHADER,  // 셰이더 연산 (GPU 직결)
    PLAY_BGM,      // 사운드 재생
    WAIT_INPUT     // 유저의 클릭 대기 (이때만 루프가 일시 정지됨)
}