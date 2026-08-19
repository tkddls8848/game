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
5. 시간은 **정수 틱 0~6** (18:00~19:00, 10분 단위). 문자열 시각 비교 금지

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

---

## 현재 진행 상황

- [x] **Phase 0 — 환경 정비 완료** (컴파일 `error CS` 0건, EditMode 2/2 passed)
- [ ] Phase 1 — 맵/플레이어 이동/상호작용
- [ ] Phase 2 — 시간 · NPC 스케줄
- [ ] Phase 3 — 조사 · 단서
- [ ] Phase 4 — 대화 · 증언 · 목격
- [ ] Phase 5 — 사건 데이터
- [ ] Phase 6 — 고발 · 결과
