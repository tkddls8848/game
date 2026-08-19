# 1인 추리 시뮬레이션 게임 — 개발 계획 (Claude Code 환경판)

> 원본 Codex 전달용 계획을 **Claude Code(CLI 에이전트) 환경**에서 실제로 굴러가도록 재작성한 문서다.
> 핵심 차이: Claude Code는 터미널 에이전트라 **Unity 에디터 GUI를 직접 조작할 수 없다.**
> 따라서 "에디터에서 드래그해서 만든다"에 의존하는 모든 절차를 **코드/텍스트로 재현 가능한 형태**로 바꿨다.

---

## 0. 이 문서가 원본과 다른 점 (요약)

| 항목 | 원본(Codex 전제) | 이 문서(Claude Code 전제) |
|---|---|---|
| 씬 구성 | 에디터에서 수동 배치 | **에디터 스크립트로 씬을 생성**(`Tools/Detective/Rebuild Main Scene`) |
| 데이터 포맷 | ScriptableObject(.asset) | **JSON 우선**(에이전트가 직접 읽고 쓰기 쉬움), SO는 선택적 래퍼 |
| 프리팹 | 에디터에서 제작 | 코드로 생성(`PrefabUtility`) 또는 씬 빌더가 런타임 생성 |
| 검증 | 사람이 Play 눌러 확인 | **batchmode 컴파일 + EditMode 테스트**를 에이전트가 먼저 돌리고, 사람은 Play 확인만 |
| 진행 단위 | Phase 설명 | Phase마다 **완료 조건(DoD) + 검증 명령 + 사람 확인 항목** 명시 |
| 역할 | Codex가 전부 | **에이전트/사람 역할 분담표** 명시 |

---

## 1. 프로젝트 목표

소규모 2D 추리 시뮬레이션 게임의 프로토타입을 개발한다.
플레이어는 제한된 공간을 돌아다니며 NPC의 증언, 행동 기록, 현장 단서를 조사하고 사건의 범인을 추리한다.

첫 번째 목표는 완성된 상용 게임이 아니라 **약 15~30분 분량의 플레이 가능한 프로토타입**이다.

---

## 2. 개발 환경

* 엔진: Unity 6 (6000.x LTS 계열)
* 템플릿: **2D (Built-In Render Pipeline)** 권장 — 의존성 최소화. URP 2D 템플릿을 써도 스프라이트만 쓰므로 코드 변경 없음
* 언어: C#
* 형태: 2D Top-Down
* 플랫폼: PC (Windows)
* 입력: 키보드 + 마우스 — **레거시 `Input` 클래스 사용**(Input System 패키지 도입은 프로토타입 범위 밖)
* 그래픽: 단색 스프라이트/사각형(코드로 생성). 외부 유료 에셋 의존 없음
* 버전 관리: Git (이미 초기화됨)

### 2.1 사전 준비 (사람이 1회 수행)

에이전트는 Unity를 설치하거나 Unity Hub GUI를 조작할 수 없다. 아래는 사람이 먼저 해야 한다.

1. Unity Hub 설치 → **Unity 6 (6000.x)** 에디터 설치
2. Hub에서 `2D` 템플릿으로 프로젝트 생성. 위치는 이 저장소 안:
   `C:\Users\tkddl\orca\projects\game\DetectivePrototype`
3. 설치된 에디터 실행 파일 경로를 확인해서 에이전트에게 알려준다:

```powershell
Get-ChildItem "C:\Program Files\Unity\Hub\Editor" -Directory | Select-Object Name
# 예: C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe
```

> 현재 이 머신의 기본 경로에는 Unity가 설치되어 있지 않다. 위 단계가 끝나야 Phase 1을 시작할 수 있다.
> 단, **Unity 없이도 C# 로직 작성과 순수 로직 단위 테스트는 선행 가능**하다(§3.2-d 참고).

---

## 3. Claude Code 작업 방식

### 3.1 역할 분담

| 작업 | 담당 |
|---|---|
| Unity 설치, 프로젝트 생성, 에디터 실행 | 사람 |
| C# 스크립트 작성/수정 | 에이전트 |
| 사건·NPC·단서 JSON 데이터 작성 | 에이전트 |
| 씬/프리팹 생성 (에디터 스크립트 경유) | 에이전트 |
| batchmode 컴파일 검증, EditMode 테스트 실행 | 에이전트 |
| Play 모드에서 조작감·동선 확인 | 사람 |
| 버그 리포트(스크린샷/로그 전달) | 사람 |
| 버그 수정 | 에이전트 |

### 3.2 에이전트가 GUI 없이 검증하는 방법

**(a) 컴파일 검증 — batchmode**

```powershell
& "C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "C:\Users\tkddl\orca\projects\game\DetectivePrototype" `
  -logFile -
```

로그에 `error CS` 문자열이 없으면 컴파일 통과.

**(b) 로직 테스트 — Unity Test Framework (EditMode)**

```powershell
& "C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\tkddl\orca\projects\game\DetectivePrototype" `
  -runTests -testPlatform EditMode `
  -testResults "TestResults.xml" -logFile -
```

**(c) 씬 재생성 — executeMethod**

```powershell
& "...\Unity.exe" -batchmode -quit -nographics `
  -projectPath "..." `
  -executeMethod DetectiveEditor.SceneBuilder.RebuildMainScene -logFile -
```

> ⚠️ **주의:** Unity 에디터가 해당 프로젝트를 열어 둔 상태에서는 batchmode 실행이 프로젝트 락 때문에 실패한다.
> 에이전트가 위 명령을 돌리기 전에는 사람이 에디터를 닫아야 한다. 반대로 사람이 Play 테스트 중이면 에이전트는 코드 작성만 한다.

**(d) 순수 C# 로직은 Unity 없이도 검증 가능**

`TimeManager`의 시간 진행, `NPCSchedule`의 시각→장소 조회, `CaseManager`의 정답 채점, 목격 판정은 `UnityEngine` 의존이 없도록 설계한다.
→ Unity 미설치 상태에서도 `dotnet` 콘솔 프로젝트나 EditMode 테스트로 바로 검증할 수 있다. **이것이 이 설계의 핵심 원칙(§18-1)이다.**

### 3.3 권한 설정 (선택)

Unity CLI 호출 시 매번 승인 프롬프트가 뜨는 걸 줄이려면 `.claude/settings.json`에 허용 규칙을 추가한다.

```json
{
  "permissions": {
    "allow": ["Bash(*Unity.exe*)", "PowerShell(*Unity.exe*)"]
  }
}
```

### 3.4 CLAUDE.md

프로젝트 루트에 `CLAUDE.md`를 두고 다음을 기록한다(Phase 0에서 에이전트가 생성):
Unity 실행 경로, 검증 명령 3종, 폴더 규칙, 데이터 JSON 스키마 위치, "에디터 열려 있으면 batchmode 금지" 규칙.

---

## 4. 핵심 게임 루프

1. 맵을 탐색한다
2. NPC와 대화한다
3. 물건과 장소를 조사한다
4. 증언과 단서를 획득한다
5. 수사 노트에서 정보를 확인한다
6. NPC의 증언과 실제 사건 타임라인의 모순을 찾는다
7. 범인과 사건 내용을 추리한다
8. 최종 고발 화면에서 답을 제출한다
9. 정답 여부에 따라 결과를 표시한다

---

## 5. 프로토타입 범위 — 맵

하나의 작은 건물 내부. 공간 6개:

`로비 / 식당 / 복도 / 피해자 방 / 용의자 방 / 창고`

* 맵은 **사각형 Room 6개 + 통로**로 구성한다
* 방 데이터(이름, 좌표, 크기)는 `rooms.json`으로 분리하고, 씬 빌더가 이를 읽어 콜라이더/바닥 스프라이트를 생성한다
* 수동 타일맵 편집 없음 → 에이전트가 JSON만 고치면 맵이 바뀐다

---

## 6. 등장인물

* 피해자 1명, 용의자 NPC 4명, 플레이어 1명

NPC 데이터 필드: 이름 / 설명 / 피해자와의 관계 / 시작 위치 / 시간별 행동 일정 / 알고 있는 정보 / 거짓 증언 / 목격 정보 / 대화 데이터

---

## 7. 시간 시스템

18:00 ~ 19:00, **10분 단위 7틱**.

```text
18:00  18:10  18:20  18:30  18:40  18:50  19:00
```

* 내부 표현은 **정수 틱 인덱스(0~6)** 로 하고, 표시용 문자열은 변환 함수로 만든다 → 비교·채점이 단순해진다
* 게임 시간에 따라 NPC가 지정된 위치로 이동한다
* 시간 배속/일시정지는 초기 버전에 없다
* **사건 시간대는 "이미 지나간 기록"이다.** 플레이어는 실시간으로 사건을 목격하는 게 아니라, 조사·증언으로 이 7틱의 진실을 복원한다
  → 즉 TimeManager는 "현재 시각"이 아니라 **타임라인 조회 API**로 먼저 구현한다. NPC 실시간 이동 연출은 그 위에 얹는다

NPC 스케줄 예:

```text
NPC_A
18:00 식당 / 18:10 식당 / 18:20 복도 / 18:30 피해자 방 / 18:40 복도 / 18:50 로비 / 19:00 로비
```

---

## 8. NPC 이동 시스템

* 시간표에 지정된 방으로 이동
* 목적지 도착 후 대기
* 같은 시각 + 같은 방 = 목격 가능
* 경로 탐색은 방 중심점 직선 이동으로 충분(NavMesh/A* 없음)
* 행동 트리·범용 AI 없음

---

## 9. 사건 구조

첫 번째 사건은 고정 사건. **코드가 아니라 `Assets/Resources/GameData/cases/case_01.json`에 둔다.**

```text
범인: NPC_A
범행 시각: 18:30
장소: 피해자 방
동기: 금전 문제
수법: 독살
결정적 증거: 와인잔
```

향후 `case_02.json`을 추가하는 것만으로 새 사건이 붙도록 설계한다.

---

## 10. 데이터 레이아웃 (JSON 우선)

**왜 ScriptableObject가 아니라 JSON인가:**
`.asset` 파일은 GUID/fileID가 얽힌 YAML이라 에이전트가 텍스트로 안전하게 생성·수정하기 어렵다. JSON은 에이전트가 직접 작성·diff·검증할 수 있고, 사람이 읽기도 쉽다.
→ 프로토타입은 JSON. 나중에 에디터 편집 편의가 필요해지면 JSON을 SO로 임포트하는 툴을 추가한다.

```text
Assets/Resources/GameData/
    rooms.json
    npcs/          npc_a.json  npc_b.json  npc_c.json  npc_d.json
    evidence/      evidence.json
    dialogue/      dialogue_npc_a.json ...
    cases/         case_01.json
```

로딩은 `Resources.Load<TextAsset>("GameData/...")` + `JsonUtility`.
`JsonUtility`는 딕셔너리/다형성을 못 다루므로 **모든 스키마를 배열 + 문자열 ID 참조로 평평하게** 설계한다.

### 단서 스키마

```json
{
  "id": "evidence_wineglass",
  "name": "깨진 와인잔",
  "description": "피해자의 방에서 발견된 와인잔. 바닥에 붉은 침전물이 남아 있다.",
  "foundRoom": "room_victim",
  "relatedNpc": "npc_a",
  "relatedTick": 3
}
```

`획득 여부`는 데이터가 아니라 **런타임 상태**다. `EvidenceManager`가 `HashSet<string>`으로 보관한다(데이터 파일은 읽기 전용 유지).

---

## 11. 증언 시스템

NPC에게 말을 걸면 증언을 얻는다.

```text
NPC_A: "18시 15분 이후에는 계속 식당에 있었습니다."
```

실제 기록:

```text
18:20 복도
18:30 피해자 방
```

**게임은 모순을 자동으로 알려주지 않는다.** 플레이어가 수사 노트를 보고 직접 판단한다.
(내부적으로 모순 판정 함수는 만들되, 데이터 검증/테스트용으로만 쓰고 UI에 노출하지 않는다.)

---

## 12. 목격 시스템

```text
같은 시각 + 같은 방 = 목격 가능
```

시야 판정 없음. 목격 결과는 해당 NPC의 대화 데이터에 조건부 대사로 추가된다.

```text
NPC_B: "18시 20분쯤 A가 피해자 방 쪽으로 가는 것을 봤습니다."
```

목격 대사는 손으로 다 쓰지 않고, **스케줄에서 자동 생성 + 필요한 것만 수동 오버라이드**한다.

---

## 13. 조사 시스템

조사 가능한 오브젝트 근처에서 `E`를 누르면 조사한다.

필요 기능: 조사 대상 하이라이트 / 상호작용 / 설명 UI / 단서 획득 / 조사 완료 여부 저장

---

## 14. 수사 노트 UI

획득한 정보만 표시한다. 탭 3개:

* **인물** — 이름, 관계, 발견된 정보, 증언
* **증거** — 이름, 설명, 발견 위치
* **타임라인** — 사건 관련 시각, 목격 정보, 발견된 행동 기록

자동 연결·자동 추론 없음. UI는 uGUI(TextMeshPro) 기본 컴포넌트만 사용하고, **레이아웃도 에디터 스크립트로 생성**한다.

---

## 15. 최종 추리 시스템

고발 화면에서 6개 항목 선택:

```text
범인 / 동기 / 범행 시간 / 범행 장소 / 범행 방법 / 결정적 증거
```

제출 시 `case_01.json`의 정답과 비교. 채점 함수는 **순수 함수**로 만들어 EditMode 테스트로 검증한다.

---

## 16. 결과 화면

```text
CASE SOLVED   /   CASE FAILED
```

내부적으로는 `6개 중 몇 개 정답`을 계산해 두고, 표시만 2종으로 한다 → 부분 정답/다중 엔딩 확장 시 데이터 구조를 안 바꿔도 된다.

---

## 17. 프로젝트 구조

```text
DetectivePrototype/
  Assets/
    Scripts/
      Core/            GameManager.cs  TimeManager.cs  GameTime.cs
      Player/          PlayerController.cs  PlayerInteraction.cs
      NPC/             NPCController.cs  NPCSchedule.cs  NPCData.cs
      Investigation/   Evidence.cs  EvidenceManager.cs  InvestigationManager.cs
      Dialogue/        DialogueManager.cs  DialogueData.cs
      Case/            CaseData.cs  CaseManager.cs
      UI/              InvestigationNotebookUI.cs  DialogueUI.cs  AccusationUI.cs
      Data/            GameDataLoader.cs        # JSON 로딩 단일 진입점
    Editor/
      DetectiveEditor/ SceneBuilder.cs          # 씬을 코드로 생성 (Claude 환경 핵심)
                       DataValidator.cs         # JSON 참조 무결성 검사
    Tests/
      EditMode/        TimeTests.cs  ScheduleTests.cs  CaseGradingTests.cs
                       DetectivePrototype.Tests.asmdef
    Resources/GameData/  (§10 참고)
    Scenes/            Main.unity                # SceneBuilder가 생성
  DEVELOPMENT_PLAN.md
  CLAUDE.md
  .gitignore
```

### 17.1 필수 세팅

* `.gitignore`: Unity 공식 템플릿 사용 (`Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `*.csproj`, `*.sln`)
* `Packages/manifest.json`에 `com.unity.test-framework` 포함 확인
* `Tests/EditMode`에 asmdef 생성 (참조: `UnityEngine.TestRunner`, `UnityEditor.TestRunner`)

---

## 18. 개발 원칙 (Claude Code판)

1. **UnityEngine 의존을 로직에서 분리한다.** 시간·스케줄·채점·목격 판정은 순수 C# → GUI 없이 검증 가능
2. 한 번에 전체 시스템을 구현하지 않는다. Phase 단위로 진행한다
3. 각 단계가 **실제 실행되는 상태**를 유지한다 (컴파일 에러 0)
4. 새 기능 전에 기존 기능 동작을 먼저 확인한다
5. 시스템 간 의존성 최소화 (매니저끼리 직접 참조 대신 이벤트/인터페이스)
6. 사건 데이터는 코드와 분리한다 (JSON)
7. 불필요하게 복잡한 디자인 패턴 금지
8. 완성도보다 플레이 가능성 우선
9. **에디터 수동 작업을 요구하는 설계를 피한다.** 손으로 배치해야만 되는 구조는 에이전트가 재현·수정할 수 없다
10. 커밋은 Phase 단위로. 각 커밋은 컴파일이 통과하는 상태여야 한다

---

## 19. 프로토타입에서 제외할 기능

온라인 / 멀티플레이 / 생성형 AI NPC 대화 / 절차적 사건 생성 / 오픈월드 / 복잡한 인벤토리 / 전투 / 성장·스킬 / 세이브 슬롯 / 음성 / 고급 애니메이션 / 행동 트리 / Input System 패키지 / Addressables / 외부 트윈 라이브러리

---

## 20. 구현 순서 (Phase별 완료 조건 포함)

### Phase 0 — 환경 정비

* Unity 프로젝트 생성(사람), `.gitignore`, `CLAUDE.md`, 폴더 스켈레톤, Test asmdef
* **DoD:** batchmode 컴파일 통과 + 빈 EditMode 테스트 1개가 green
* **사람 확인:** 에디터에서 프로젝트가 에러 없이 열린다

### Phase 1 — 기본 플레이

* `rooms.json` + `SceneBuilder`(방/카메라/플레이어 생성)
* `PlayerController`(WASD, Rigidbody2D), 카메라 추적
* `PlayerInteraction`(`E`, 범위 내 `IInteractable` 탐색)
* **DoD:** `-executeMethod ...RebuildMainScene`로 Main.unity가 재생성되고 컴파일 통과
* **사람 확인:** Play → WASD 이동, 벽 충돌, 상호작용 프롬프트 표시

### Phase 2 — 시간 · NPC

* `GameTime`(틱↔"18:30" 변환, 순수 C#)
* `NPCSchedule`(틱→방 조회, 순수 C#), `npc_*.json`
* `NPCController`(스케줄대로 방 중심으로 이동)
* **DoD:** `TimeTests`, `ScheduleTests` green. NPC 4명이 씬 빌더로 생성됨
* **사람 확인:** 시간이 흐르면 NPC가 지정된 방으로 이동한다

### Phase 3 — 조사 · 단서

* `evidence.json`, `Evidence`(IInteractable), `EvidenceManager`(획득 집합)
* 수사 노트 UI — 증거 탭
* **DoD:** `DataValidator`가 evidence의 room/npc 참조 무결성 통과
* **사람 확인:** `E`로 조사 → 설명 표시 → 노트에 등록 → 재조사 시 중복 등록 안 됨

### Phase 4 — 대화 · 증언 · 목격

* `dialogue_*.json`, `DialogueManager`, `DialogueUI`
* 스케줄 기반 목격 정보 자동 생성 + 조건부 대사
* 노트 — 인물 탭, 타임라인 탭
* **DoD:** 목격 판정 테스트 green (같은 틱·같은 방)
* **사람 확인:** NPC 대화 → 증언 획득 → 노트 타임라인에 반영

### Phase 5 — 사건 데이터

* `case_01.json`, `CaseData`, `CaseManager`
* 증언 ↔ 실제 스케줄 모순이 최소 2건 존재하도록 데이터 튜닝
* **DoD:** "단서와 증언만으로 범인을 유일하게 특정할 수 있는가"를 확인하는 검증 스크립트 통과
* **사람 확인:** 실제로 단서만 보고 범인을 좁힐 수 있는지 플레이

### Phase 6 — 고발 · 결과

* `AccusationUI`(6개 드롭다운), 채점 순수 함수, 결과 화면
* **DoD:** `CaseGradingTests` — 전정답/부분정답/전오답 케이스 green
* **사람 확인:** 처음부터 끝까지 15~30분 플레이 완주

---

## 21. 첫 번째 구현 목표 (Phase 1의 최소 슬라이스)

```text
1. 2D 방 하나 생성          (SceneBuilder가 코드로 생성)
2. 플레이어 WASD 이동
3. NPC 하나 배치
4. NPC에게 접근
5. E 키 입력
6. 간단한 대화창 표시
7. 조사 가능한 물건 하나 생성
8. E 키로 조사
9. 단서 하나 획득
10. 수사 노트(N 키)에서 획득한 단서 확인
```

이 10개가 동작하는 것을 사람이 Play로 확인한 뒤에 시간 시스템과 스케줄로 넘어간다.

---

## 22. 에이전트 작업 지침

각 작업 단계마다 다음을 지킨다.

1. **Phase 하나씩.** 전체 코드를 한 번에 쏟아내지 않는다
2. 생성/수정한 파일과 이유를 설명한다
3. 중요한 설계 결정을 한두 줄로 남긴다
4. 그 Phase의 **DoD 검증 명령을 실제로 실행**하고 결과를 보고한다 (통과했다고 추정하지 않는다)
5. 사람이 Unity에서 확인해야 할 항목을 **체크리스트로 제시**한다
6. 에러가 나면 원인을 먼저 특정하고 고친다. 로그 전문을 요구하기 전에 batchmode 로그를 직접 읽는다
7. 기존 기능을 깨뜨리지 않도록 변경 범위를 최소화한다
8. **에디터가 열려 있는지 먼저 확인**한다. 열려 있으면 batchmode 대신 코드 작성만 하고 사람에게 닫아 달라고 요청한다
9. 손으로만 만들 수 있는 에셋(프리팹·씬 배치)이 필요해지면, 그것을 만드는 **에디터 스크립트를 먼저 작성**한다

최종 목표는 작은 사건 하나를 처음부터 끝까지 플레이할 수 있는 추리 시뮬레이션 프로토타입이다.
