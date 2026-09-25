# CLAUDE.md — 추리 시뮬레이션 프로토타입

전체 개발 계획은 `DEVELOPMENT_PLAN.md` 참고. 이 파일은 **작업할 때 매번 필요한 사실**만 담는다.

---

## 환경

| 항목 | 값 |
|---|---|
| Unity 에디터 | `C:\Program Files\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe` |
| Unity Hub | `C:\Program Files\Unity Hub\Unity Hub.exe` |
| 프로젝트 루트 | `C:\Users\tkddl\orca\projects\game\DetectivePrototype` |
| 저장소 루트 | `C:\Users\tkddl\orca\projects\game` |
| 입력 | 레거시 `Input` 클래스 (Input System 패키지 미사용) |
| Mono 컴파일러 | `...\6000.0.81f1\Editor\Data\MonoBleedingEdge\bin\mcs.bat` |

> **라이선스 필수.** 라이선스가 없으면 batchmode 실행이 return code 198(`No valid Unity Editor license found`)로 거부된다.
> 활성화: Unity Hub 로그인(우측 상단 계정 → Sign in) → Personal 라이선스 자동 발급.
> 라이선스 없이도 **순수 C# 로직은 번들 Mono(`mcs.bat`)로 컴파일·검증 가능**하다 (DEVELOPMENT_PLAN.md §3.2-d).

---

## 검증 명령 (PowerShell)

에이전트는 Phase 완료를 주장하기 전에 아래를 **실제로 실행**한다.

### ⚠️ 반드시 `Start-Process -Wait`로 실행할 것

`Unity.exe`는 **GUI 서브시스템 실행 파일**이다. PowerShell에서 `& $UNITY ...`로 호출하면
**프로세스 종료를 기다리지 않고 즉시 반환**한다. 그 결과:

* `$LASTEXITCODE`가 비어 있어 성공/실패를 판정할 수 없다
* 백그라운드 태스크가 "완료"로 표시되면서 아직 살아 있는 Unity가 정리돼 로그가 중간에 끊긴다
* 다음 batchmode 호출이 `another Unity instance is running`으로 실패한다

`-logFile -`(stdout)도 이 조합에서는 신뢰할 수 없다. **로그는 파일로 받아서 읽는다.**

```powershell
$UNITY = "C:\Program Files\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe"
$PROJ  = "C:\Users\tkddl\orca\projects\game\DetectivePrototype"

function Invoke-Unity([string[]]$UnityArgs, [string]$Log) {
    $a = $UnityArgs + @('-logFile', $Log)
    $p = Start-Process -FilePath $UNITY -ArgumentList $a -Wait -PassThru -NoNewWindow
    return $p.ExitCode
}
```

**컴파일 검증**
```powershell
Invoke-Unity @('-batchmode','-quit','-nographics','-projectPath',$PROJ) "$env:TEMP\unity_compile.log"
Select-String "error CS" "$env:TEMP\unity_compile.log"
```
`error CS` 매치가 없으면 통과.

**순수 로직만 검증 (Unity 라이선스/설치 없이)**

`Assets/Scripts` 아래에서 `using UnityEngine`/`using UnityEditor`가 없는 파일은 전부 번들 Mono로 바로 컴파일된다.
(시간·스케줄·목격·대화 조립·타임라인 복원·채점·추리 가능성 검증이 모두 여기에 속한다.)

```powershell
$pure = Get-ChildItem Assets\Scripts -Recurse -Filter *.cs |
  Where-Object { -not (Select-String -Path $_.FullName -Pattern 'using UnityEngine|using UnityEditor' -Quiet) } |
  ForEach-Object FullName
& "...\MonoBleedingEdge\bin\mcs.bat" -target:library -out:$env:TEMP\pure.dll $pure
```

리눅스에서는 `mono-mcs`/`nunit-console`로 같은 파일 묶음을 컴파일하고, `GameDataLoader`를 쓰지 않는 테스트 파일
(`Assets/Tests/EditMode` 중 `GameDataTests.cs`를 뺀 나머지)을 NUnit으로 돌릴 수 있다.

여기에 UnityEngine 참조가 끼어들어 컴파일이 깨지면 §18-1 원칙이 무너진 것이다. 로직을 다시 분리한다.

**EditMode 테스트**
```powershell
Invoke-Unity @('-batchmode','-nographics','-projectPath',$PROJ,
               '-runTests','-testPlatform','EditMode',
               '-testResults',"$PROJ\TestResults.xml") "$env:TEMP\unity_tests.log"
```
종료 코드 0 = 전부 통과. 상세는 `TestResults.xml`의 `passed`/`failed` 속성.

**씬 재생성**
```powershell
Invoke-Unity @('-batchmode','-quit','-nographics','-projectPath',$PROJ,
               '-executeMethod','DetectiveEditor.SceneBuilder.RebuildMainScene') "$env:TEMP\unity_scene.log"
```

씬 빌더는 rooms.json 무결성 검사에 실패하면 **예외를 던지고 씬을 만들지 않는다** → batchmode 종료 코드가 0이 아니게 된다.
에디터 GUI에서는 `Tools/Detective/Rebuild Main Scene`, 데이터만 검사하려면 `Tools/Detective/Validate Game Data`.

---

## ⚠️ 규칙: 에디터가 열려 있으면 batchmode 금지

Unity 에디터가 이 프로젝트를 열어 둔 상태에서 batchmode를 실행하면 **프로젝트 락 때문에 실패**한다.

batchmode 실행 전 확인:
```powershell
Get-Process Unity -ErrorAction SilentlyContinue
```
프로세스가 있으면 → 사람에게 에디터를 닫아 달라고 요청하고, 그동안 코드 작성만 한다.

---

## 어셈블리 구조

asmdef 3개로 나뉘어 있다. **이 경계를 깨지 말 것.**

```
Assets/Scripts/     DetectivePrototype.asmdef              (런타임, 네임스페이스 Detective.*)
Assets/Editor/      DetectivePrototype.Editor.asmdef       (에디터 전용, → DetectivePrototype 참조)
Assets/Tests/EditMode/ DetectivePrototype.Tests.EditMode.asmdef (테스트, → DetectivePrototype 참조)
```

주의: asmdef가 있는 어셈블리는 **기본 어셈블리(Assembly-CSharp)를 참조할 수 없다.**
런타임 코드는 반드시 `Assets/Scripts/` 아래에 두어야 테스트에서 참조된다.

---

## 설계 원칙 (요약, 전문은 DEVELOPMENT_PLAN.md §18)

1. **로직에서 UnityEngine 의존을 뺀다.** 시간·스케줄·채점·목격 판정은 순수 C#
   → GUI 없이 EditMode 테스트로 검증 가능. 이게 이 프로젝트의 핵심 제약이다
2. **손으로만 만들 수 있는 에셋을 만들지 않는다.** 씬/프리팹이 필요하면 그것을 생성하는
   에디터 스크립트를 먼저 쓴다 (`Assets/Editor/DetectiveEditor/SceneBuilder.cs`)
3. **사건 데이터는 JSON.** ScriptableObject(.asset)는 GUID YAML이라 텍스트로 안전하게 못 쓴다
4. Phase 하나씩. 각 커밋은 컴파일이 통과하는 상태
5. 시각은 **정수 밀리초** (18:00 = `GameTime.StartMs` = 0, 19:00 = `GameTime.EndMs` = 3,600,000). **부동소수·문자열 시각 비교 금지**.
   틱(10분 칸 0~6)은 **표시용 파생값**이다 — 수사 노트 표·타임라인 눈금·고발 화면 시각 선택지만 칸을 쓴다.
   * 시각을 받는 API는 ms: `GameTime.ToLabel/TryParse/Clamp/IsValid`, `NpcSchedule.RoomAt(npc, ms)`, `TimelineBoard.PlacementAt/RecordsAt`
   * 칸을 받는 API는 이름에 `Tick`이 붙는다: `TickOf(ms)`, `TickLabel`, `ClampTick`, `TryParseTick`, `RecordsInTick`
   * 틱으로 적힌 데이터는 **경계 함수 `GameTime.TickToMs`로만** ms가 된다(스키마의 `RevealMs`·`RelatedMs`·`ClaimMs`·`SightingMs`·`CaseAnswer.TimeMs`).
     `-1`(시각 없음)은 `GameTime.NoTime`으로 옮겨진다. 데이터 형식 자체의 ms 전환은 후속 Phase(DEVELOPMENT_PLAN_UNHEARD.md)

---

## 데이터 위치

```
Assets/Resources/GameData/
    rooms.json
    npcs/      npc_a.json ...
    evidence/  evidence.json
    dialogue/  dialogue_npc_a.json ...
    cases/     case_01.json
```

로딩: `Resources.Load<TextAsset>("GameData/...")` + `JsonUtility`.
`JsonUtility`는 Dictionary/다형성 불가 → **배열 + 문자열 ID 참조로 평평하게** 설계한다.

### 맵 (`rooms.json`)

* 좌표는 월드 유닛, `(x, y)`는 **좌하단 모서리**. 방은 축 정렬 사각형이다
* 벽 두께 `RoomLayout.WallThickness = 0.4`. **문(`doors`)의 짧은 변은 이보다 두꺼워야** 벽을 관통해 구멍이 뚫린다
* 벽은 손으로 배치하지 않는다. `RoomLayout.BuildAllWallSegments()`가 방 테두리에서 문을 빼고 같은 직선 위 조각을 병합해 만든다
* 방 배치: 아래줄 `로비 | 식당 | 창고`, 가운데 가로 `복도`, 윗줄 `서재(room_victim) | 손님 방(room_suspect)`. 모든 방은 복도를 통해서만 연결된다
* JSON을 고친 뒤에는 **반드시 씬을 다시 만든다** (아래 `RebuildMainScene`). 씬은 rooms.json·evidence.json·npcs의 파생물이다

### 인물 (`npcs/*.json`)

* 시각 필드는 아직 **틱(10분 칸) 단위**로 적는다. 코드는 ms로 묻고, ms가 속한 칸(`GameTime.TickOf`)의 값을 읽는다(18:35 → 18:30 칸)
* `schedule[7]` = 틱별 **실제** 방. 용의자는 빈 칸 불가, 피해자는 사망 이후 `""`
* `claims[7]` = 본인이 **주장**하는 방. 빈 문자열이면 그 틱은 사실대로 말한다. 거짓말은 여기에만 적는다
* 목격 대사는 스케줄에서 자동 생성된다(같은 틱 + 같은 방). **거짓말하는 틱의 목격은 털어놓지 않는다**

### 단서 (`evidence/evidence.json`) · 대사 (`dialogue/*.json`) · 사건 (`cases/case_01.json`)

* 단서의 `revealNpc/revealTick/revealRoom` = 물증이 확정하는 행적. 시각이 없는 int 필드는 **-1을 명시**한다(JsonUtility는 빠진 int를 0 = 18:00으로 채운다).
  `*Tick` 필드는 코드에서 직접 읽지 말고 스키마의 `*Ms` 속성(경계 함수)으로 읽는다. 예외는 원본 값을 검사하는 `GameDataValidator`
* 대사 한 줄의 역할: `revealsClaims`(알리바이, 인물당 정확히 1개), `requiresEvidence`(조건부), `claimTick/Room`(말 바꾸기), `sightingTick/Target`(자동 목격 대사 덮어쓰기)
* 사건 파일은 정답 6항목(범인·동기·시각·장소·수법·결정적 증거)과 선택지·문구를 담는다
* 데이터를 고치면 `GameDataValidator`(참조 무결성)와 `CaseSolvabilityChecker`(범행 시각에 범인만 목격·물증 알리바이가 없고, 증언↔목격·물증 모순이 2건 이상)가 씬 빌더와 EditMode 테스트에서 돈다

### case_01 정답 (스포일러, 데이터 수정 시 참고)

범인 클라라(npc_a) / 금전(횡령 발각) / 18:30 / 서재 / 독살 / 립스틱 자국 와인잔.
클라라의 "계속 식당" 증언은 18:10 창고(마르코 목격)·18:20 복도(줄리안 목격)·18:40 복도(헬렌 목격)와 모순되고,
18:30 알리바이는 본인 증언뿐이다. 나머지 셋은 18:30에 목격(줄리안↔헬렌) 또는 물증(전화 기록부 → 마르코)으로 알리바이가 선다.
줄리안의 18:10 거짓말(유언장 다툼)은 곁가지 단서다.

---

## 연출 에셋 (`art.json`)

연출 방향은 **"사건 파일"**: 어두운 저택 평면도 위에 바랜 종이 패널, 검은/붉은 잉크 글씨, 보드게임 말 같은 인물 토큰, 명조체(Noto Serif KR).

* `Assets/Resources/GameData/art.json`이 방 바닥·인물 초상화·단서 이미지·소리의 **Resources 경로**를 정한다
* **자리는 지금 전부 채워져 있다** — 바닥 질감 6장(ambientCG), 초상화 5장·단서 그림 7장(Wikimedia Commons, 세피아·비네팅으로 톤 맞춤),
  BGM 3곡(Musopen 쇼팽)·환경음·효과음 3종. 전부 CC0/퍼블릭 도메인이고 출처는 `CREDITS.md`
* **파일이 없으면 코드가 만든 대체물**을 쓴다: 바닥은 `ProceduralTextures`(wood/marble/carpet/stone/rug), 초상화는 세피아 실루엣 + 이름 첫 글자,
  종소리·종이·조사음은 `ProceduralAudio` 합성음. BGM·환경음은 파일이 없으면 침묵
* 씬 빌더는 바닥·토큰 질감을 `Assets/Art/Generated/*.png`로 저장해 씬에 끼운다(런타임 생성 Sprite는 씬에 남지 않는다). UI 종이·비네팅은 실행 시 만든다
* 에셋을 넣으면 `CREDITS.md`에 출처·라이선스를 한 줄 적는다. 포함된 폰트는 OFL 1.1. 어디서 받을지는 `ASSET_SOURCES.md`
* `ArtManifestValidator`가 art.json의 방·인물·단서 참조를 검사한다(씬 빌더·EditMode 테스트)
* 임포트 설정은 `Assets/Editor/DetectiveEditor/ArtAssetImporter.cs`(AssetPostprocessor)가 맞춘다 — 손으로 맞추지 말 것.
  바닥은 **Wrap=Repeat · Mesh=Full Rect · PPU=질감 크기(512)** 여야 Tiled로 깔리고 tileSize가 무늬 크기가 된다.
  `.meta`가 없는 첫 임포트 때만 손대므로 그 뒤 사람이 고친 값은 남는다. 되돌리려면 `Tools/Detective/Reimport Art Assets`
* 그림·소리·폰트의 `.meta`는 Unity가 처음 임포트할 때 만든다 → 그 뒤 **커밋해서 GUID를 고정**한다

---

## 조작

| 키 | 동작 |
|---|---|
| WASD / 화살표 | 이동 |
| E | 조사 · 대화 (대화 중에는 다음 줄) |
| T | 타임라인 관찰 모드 (←→ 시각, 1~7 바로 가기, Space 자동 재생). 에디터에서만 F9 = 실제 스케줄 보기 |
| Tab (관찰 모드 안) | 청취(소나) 화면 ↔ 타임라인. 청취: WASD 귀 옮기기, Space 재생·정지, ←→ 5초, R 처음부터, [ ] 배속 |
| N | 수사 노트 (1~3 / ←→ 탭, ↑↓ 선택, PgUp/PgDn·Q/E 내용 스크롤) |
| F | 고발 (↑↓ 항목, ←→ 선택, Enter 확정) → 결과 화면에서 R = 재시작 |

---

## 현재 진행 상황

- [x] **Phase 0 — 환경 정비 완료** (컴파일 `error CS` 0건, EditMode 2/2 passed)
- [x] **Phase 1 — 맵/플레이어 이동/상호작용 (코드 작성 완료, 사람 Play 확인 대기)**
      rooms.json, RoomLayout(벽/문 생성), SceneBuilder, PlayerController, PlayerInteraction, HudUI
      순수 로직 테스트 18개는 Unity 없이 mcs+mono로 통과 확인. **batchmode 컴파일/씬 생성은 아직 미실행**
- [x] **Phase 2~6 — 코드·데이터 작성 완료 (사람 Play 확인 대기)**
      Phase 2 시간·NPC 스케줄·타임라인 관찰 모드 / Phase 3 조사·단서·수사 노트 / Phase 4 대화·증언·목격·타임라인 복원 /
      Phase 5 case_01·추리 가능성 검증·시작 화면 / Phase 6 고발·채점·결과
      리눅스 컨테이너에서 확인한 것: Unity 참조 DLL(UnityEngine 2021.3 모듈·uGUI 2020.3·UnityEditor 2021.1)로
      런타임·에디터·테스트 어셈블리 컴파일 통과, 순수 로직 테스트 80개 mono+NUnit 통과, 실제 JSON으로 참조 무결성·추리 가능성 검사 통과.
      **Unity 6 batchmode 컴파일 / EditMode 테스트 / 씬 재생성 / Play 확인은 아직 미실행**
- [x] **Phase U-0 — 시간 축을 정수 밀리초로 교체 (DEVELOPMENT_PLAN_UNHEARD.md)** — Unity 6 검증 완료
      `GameTime`이 ms 기반, 틱은 표시용 파생값. 데이터 스키마(틱 필드)는 그대로 두고 `GameTime.TickToMs` 경계 함수로 읽는다.
      batchmode 컴파일 `error CS`·`warning CS` 0건, EditMode 123/123, 씬 재생성 성공, 빌드·Play 확인까지 끝났다.
- [x] **엿듣기 수직 슬라이스 — 구조가 성립함을 확인**
      `Assets/Scripts/Eavesdrop/` (순수 C#) + `cases/slice/script_slice.json` (90초·방2·목소리2).
      핵심 발화 둘이 32~37초에 겹쳐 한 번에 다 들을 수 없다 → 되돌려 듣는 것 외에 선택지가 없다.
      `SliceSolvability`가 "필요한 사실이 어딘가에서는 들린다"와 "한 방에 고정되면 전부는 못 듣는다"를 기계 판정한다.
- [x] **Phase U-1 — 대본 검증기·연속 재생 장치·로더** (EditMode 142/142)
      `ScriptValidator`(참조 무결성) · `PlaybackTransport`(재생·정지·탐색·되감기·배속) · `EavesdropScriptLoader`(Resources).
      배속은 백분율 정수 + 나머지 누적이라 드리프트가 없다. 회차 끝에서 자동으로 되돌지 않는다(되감기는 플레이어의 선택).
      가청 판정은 문이 아니라 **벽 맞닿음** 기준이다 — 문만 보면 이 저택은 복도 중심 별 모양이 되어 복도가 아닌 두 방이 서로 무음이었다.
- [x] **Phase U-2 — 청취점과 가청 판정 + 청취(소나) 화면**
      탐색 중: `PlayerRoomTracker`(청취점 = 탐정이 선 방) → `EavesdropController`(회차 재생·`ListeningSession`) → `EavesdropUI`(종이 버블).
      관찰 모드(T)에서 **Tab** → `SonarView`: 저택은 어둠에 가라앉고 소리만 파문으로 보인다(docs/art-concepts 10번 시안).
      귀(WASD)가 선 방은 호박색, 벽 너머는 회색. 같은 방 = 글자, 벽 너머 = `SonarText.Garble`로 전부 가린 웅얼거림, 그 밖 = 무음.
      탐색 중 돌아가는 같은 `ListeningSession`을 쓰므로 여기서 들은 것은 거기서도 들은 것이다. 닫으면 청취점을 탐정의 방으로 되돌린다.
      파문·회전선은 재생 위치(ms)에 묶여 정지하면 같이 멈춘다. 링·정사각형은 `ProceduralTextures`가 만들고 씬은 다시 만들 필요가 없다
      (`TimelineController`가 실행 시 `SonarView`를 단다).
      **Unity 6 batchmode 검증 완료** — 컴파일 `error CS` 0 · `warning CS` 0, EditMode 157/157 통과. Play 확인만 남았다.
- [ ] **출시 판정 기준은 `DEVELOPMENT_PLAN_RELEASE.md`** — 남은 Phase(U-3~U-6)와 계획에 없던 출시 항목(저장·설정·일시정지 등),
      그리고 **에이전트가 끝낼 수 없는 것**(한국어 성우 음성, 스토어 작업)을 갈라 적었다. "완성도"의 종료 조건은 거기 있다.
