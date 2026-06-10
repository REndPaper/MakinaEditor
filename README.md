<p align="center">
  <h1 align="center">🎬 MakinaEditor</h1>
  <p align="center">
    <strong>크로스 플랫폼 비주얼 노벨 저작 도구</strong><br/>
    Avalonia UI 기반 · MVVM 아키텍처 · .NET 10
  </p>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10"/>
  <img src="https://img.shields.io/badge/Avalonia-12.0.1-8B5CF6?logo=avalonia&logoColor=white" alt="Avalonia"/>
  <img src="https://img.shields.io/badge/ReactiveUI-12.0.1-61DAFB" alt="ReactiveUI"/>
  <img src="https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-22C55E" alt="Platform"/>
  <img src="https://img.shields.io/badge/License-Proprietary-EF4444" alt="License"/>
</p>

---

## 📋 Overview

**MakinaEditor**는 비주얼 노벨 게임의 시나리오, 연출, 분기 구조를 하나의 통합 환경에서 설계하기 위한 데스크톱 에디터 애플리케이션입니다.

코어 컨셉은 비주얼 노벨의 실행 흐름을 **운영체제 커널과 유사한 2-Layer 아키텍처**(커널 루틴 / 유저 플로우)로 추상화하는 것입니다.

### 핵심 특징
- **시나리오 노드 그래프** — 시나리오 간 흐름을 노드 기반 캔버스로 시각화
- **플로우 타임라인** — 대사, 배경, 캐릭터, BGM 등의 연출을 타임라인 UI로 편집
- **실시간 프리뷰** — 편집 중인 플로우를 즉시 게임 화면처럼 미리보기
- **에셋 브라우저** — 리소스(배경, 캐릭터, 사운드)를 트리 구조로 관리
- **전역 변수 시스템** — 호감도, 플래그 등 게임 상태 변수를 선언적으로 관리
- **Undo/Redo** — Command Pattern 기반의 전역 실행 취소/재실행

---

## 🏛️ Architecture

```
MakinaEditor/
├── Core/              # Enums, 인터페이스 (IUndoableAction)
├── Models/            # 도메인 모델 (Scenario, FlowCommand, ProjectMetadata 등)
├── ViewModels/        # MVVM ViewModels (5개 서브 ViewModel + Mediator)
├── Views/             # Avalonia AXAML UI
├── Services/          # AssetService, UndoRedoService
└── Assets/            # 임베디드 리소스
```

### 2-Layer Opcode 시스템

| Layer | 역할 | Opcodes | 편집 UI |
|:---:|---|---|---|
| **커널 (Scenario)** | 씬 전환, 분기 평가, 흐름 제어 | `LOAD_SCENE`, `UNLOAD_SCENE`, `EVAL_BRANCH`, `JUMP_FLOW`, `SAVE_STATE` | 노드 그래프 |
| **유저 (Flow)** | 화면 연출, 대사, 사운드 | `PRINT_TEXT`, `SET_BG`, `SHOW_CHAR`, `APPLY_SHADER`, `PLAY_BGM`, `WAIT_INPUT` | 플로우 타임라인 |

### ViewModel 구조

```
MainWindowViewModel (Mediator)
├── ProjectViewModel         — 프로젝트/시나리오 CRUD, JSON 직렬화
├── ScenarioViewModel        — 시나리오 디렉토리 탐색, 활성 시나리오 관리
├── ScenarioGraphViewModel   — 노드 그래프 캔버스 (드래그, 엣지 빌더)
├── FlowEditorViewModel      — 플로우 타임라인 편집
├── PreviewViewModel         — 실시간 프리뷰 렌더링
└── AssetBrowserViewModel    — 리소스 브라우저, 전역 변수 관리
```

### 프로젝트 데이터 모델

```
ProjectMetadata
├── Name / Version
├── Resources[]        — 배경, 캐릭터, 사운드 리소스 정의
└── Variables[]        — 전역 변수 (String, Number, Boolean)

MakinaScenario
├── ScenarioName
├── KernelRoutines     — 시나리오 레벨 매크로 명령어
├── UserFlows          — 플로우 레벨 연출 명령어
└── GraphNodes[]       — 노드 그래프 시각화 데이터
```

---

## 🖥️ Editor Panels

### 1. 시나리오 노드 그래프 에디터

노드 기반 캔버스로 시나리오 흐름을 시각적으로 설계합니다.

| 노드 타입 | 색상 | 용도 |
|---|:---:|---|
| **Start** | 🟢 `#2ECC71` | 시나리오 진입점 (고정, 삭제 불가) |
| **End** | 🔴 `#E74C3C` | 시나리오 종료점 (고정, 삭제 불가) |
| **Flow** | 🔵 `#3498DB` | 플로우 블록 바인딩 |
| **Link** | 🟠 `#E67E22` | 다른 시나리오로의 이동 |
| **Conditional** | 🟣 `#9B59B6` | 조건부 분기 (True/False 경로) |

- **드래그 & 드롭**: 노드를 자유롭게 배치
- **엣지 연결**: 노드 간 흐름 경로를 시각적으로 연결
- **회상/실제 플레이 경로 구분**: End 노드 이후의 Post-Scenario 노드 지원

### 2. 플로우 타임라인 에디터

시간 순서대로 연출 명령어를 배열하는 타임라인 인터페이스입니다.

- 대사(`PRINT_TEXT`), 배경(`SET_BG`), 캐릭터(`SHOW_CHAR`), BGM(`PLAY_BGM`)
- 셰이더 효과(`APPLY_SHADER`), 유저 입력 대기(`WAIT_INPUT`)
- 선택지 분기: `WAIT_INPUT` 내부에 `ChoiceOption` 리스트로 분기 처리
- 노드 삽입, 순서 변경, 타입 변환 지원

### 3. 실시간 프리뷰

플로우 타임라인의 명령어를 실시간으로 렌더링하여 게임 화면을 미리 보여줍니다.

- 배경/캐릭터 이미지 실시간 렌더링 (비트맵 캐시)
- 대사 텍스트 오버레이
- 플레이 포인터로 현재 실행 위치 표시

### 4. 에셋 브라우저

프로젝트에서 사용하는 리소스를 통합 관리합니다.

- **리소스 관리**: 배경, 캐릭터(포즈 바리에이션 포함), 사운드
- **전역 변수**: `String`, `Number`, `Boolean` 타입의 게임 상태 변수 선언
- **리소스 편집 팝업**: 더블클릭으로 리소스 속성 수정

---

## 🔧 Tech Stack

| Component | Technology |
|---|---|
| **Framework** | .NET 10.0 |
| **UI Toolkit** | [Avalonia](https://avaloniaui.net/) 12.0.1 |
| **MVVM** | [ReactiveUI](https://www.reactiveui.net/) 12.0.1 |
| **Typography** | Avalonia.Fonts.Inter |
| **Serialization** | System.Text.Json (Polymorphic) |
| **Design System** | Fluent Theme (Dark Mode) |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- (Linux) X11 또는 Wayland 디스플레이 서버

### Build & Run

```bash
# Clone
git clone https://github.com/REndPaper/MakinaEditor.git
cd MakinaEditor

# Restore & Run
dotnet restore
dotnet run
```

### Project Structure

```
MakinaEditor.sln
└── MakinaEditor.csproj
    ├── Core/Enums.cs              — ScenarioOpcode, FlowOpcode, EditorMode
    ├── Core/IUndoableAction.cs    — Undo/Redo 인터페이스
    ├── Models/
    │   ├── MakinaProject.cs       — 최상위 프로젝트 모델
    │   ├── ProjectMetadata.cs     — 메타데이터 + 전역 변수
    │   ├── Scenario.cs            — 시나리오 + 노드 그래프
    │   ├── Commands.cs            — FlowCommand 다형성 계층
    │   ├── ResourceObject.cs      — 에셋 리소스 정의
    │   └── AssetNode.cs           — 에셋 트리 노드
    ├── Services/
    │   ├── AssetService.cs        — 파일 시스템 에셋 관리
    │   └── UndoRedoService.cs     — Command Pattern Undo/Redo
    ├── ViewModels/
    │   ├── MainWindowViewModel.cs — Mediator (서브 VM 조율)
    │   ├── ProjectViewModel.cs    — 프로젝트 CRUD
    │   ├── ScenarioViewModel.cs   — 시나리오 디렉토리
    │   ├── ScenarioGraphViewModel.cs — 노드 그래프
    │   ├── FlowEditorViewModel.cs — 플로우 타임라인
    │   ├── PreviewViewModel.cs    — 실시간 프리뷰
    │   └── AssetBrowserViewModel.cs — 에셋 브라우저
    └── Views/
        ├── MainWindow.axaml       — 메인 레이아웃
        ├── FlowEditorView.axaml   — 플로우 타임라인 UI
        ├── ScenarioGraphView.axaml — 노드 그래프 UI
        └── ...
```

---

## 📊 Development Status

| Module | Status | Description |
|---|:---:|---|
| 코어 모델/아키텍처 | ✅ Complete | 2-Layer Opcode, 다형성 직렬화, MVVM 분리 |
| 프로젝트 관리 | ✅ Complete | 생성, 저장, 로드, 최근 프로젝트 |
| 에셋 브라우저 | ✅ Complete | 리소스 CRUD, 전역 변수, 트리 뷰 |
| 플로우 타임라인 | ✅ Complete | 6종 FlowCommand, 선택지 분기, Undo/Redo |
| 실시간 프리뷰 | ✅ Complete | 비트맵 캐시, 대사/배경/캐릭터 렌더링 |
| 시나리오 노드 그래프 | ✅ Complete | 5종 노드, 드래그, 엣지 연결, 회상/실제 경로 |
| 화면 구성 에디터 (UI Design) | ⬜ Planned | 대화창, 메뉴 등 UI 레이아웃 WYSIWYG |
| 빌드/내보내기 | ⬜ Planned | C++ 런타임용 바이너리 패킹 |

---

## 🗺️ Roadmap

### Phase 1 — 코어 안정화 (Current)
- [x] 2-Layer Opcode 시스템
- [x] JSON 다형성 직렬화 (`System.Text.Json`)
- [x] 전역 변수/플래그 시스템
- [x] Undo/Redo (Command Pattern)
- [ ] 프로젝트 단위 Validation
- [ ] 에러 핸들링 및 로깅 강화

### Phase 2 — 화면 구성 에디터
- [ ] WYSIWYG 캔버스 기반 UI 레이아웃 편집
- [ ] 대화창, 선택지 UI, 메뉴 화면 템플릿
- [ ] 드래그 & 리사이즈 가능한 UI 요소 배치
- [ ] 테마/스킨 시스템

### Phase 3 — 시나리오 분기 에디터 고도화
- [ ] 다중 시나리오 간 전역 노드 그래프 뷰
- [ ] 조건부 분기 Expression 에디터 (GUI)
- [ ] 시나리오 검증 (데드 엔드 탐지, 순환 참조 검출)
- [ ] 노드 그룹화 및 서브 그래프

### Phase 4 — 플로우 에디터 고도화
- [ ] 타임라인 줌/스크롤 내비게이션
- [ ] 커스텀 명령어 플러그인 시스템
- [ ] 복수 플로우 동시 편집 (탭 기반)
- [ ] 플로우 템플릿 및 프리셋

### Phase 5 — 빌드 & 런타임
- [ ] C++ 런타임 엔진 연동
- [ ] 프로젝트 빌드/내보내기 파이프라인
- [ ] 에셋 패킹 및 압축
- [ ] 멀티 플랫폼 배포 스크립트

---

## 🤝 Contributing

현재 비공개 개발 단계입니다. 기여 가이드라인은 추후 공개될 예정입니다.

---

## 📄 License

이 프로젝트는 독점 라이선스 하에 있습니다. 무단 복제 및 배포를 금합니다.
