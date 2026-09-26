#!/usr/bin/env python3
"""
엔진 없이 돌리는 테스트가 컴파일할 파일 목록을 만든다.

`SharedLogicTests`는 Unity를 참조하지 않고 순수 C# 로직만 컴파일해 `dotnet test`로 돌린다.
Unity batchmode는 라이선스가 필요하고 프로젝트를 잠그고 분 단위로 걸리는데, 이쪽은 0.3초다.
그 차이가 이 파일이 존재하는 이유다.

**어느 파일이 순수인지는 CLAUDE.md의 판정 기준을 그대로 쓴다** —
`using UnityEngine` / `using UnityEditor`가 없는 파일.

여기에 자기 강제력이 있다. 누가 로직 파일에 UnityEngine을 끌어들이면 그 파일은 목록에서
빠지고 **테스트 빌드가 컴파일 오류로 터진다.** §18-1이 문서상의 약속이 아니라 게이트가 된다.

복사하지 않는다. 생성된 props가 Unity 프로젝트의 원본 `.cs`를 직접 가리킨다 —
두 벌이 되면 갈라지고, 갈라지면 테스트가 검증하는 것이 실제 게임 코드가 아니게 된다.

  python tools/sync_shared_logic.py           # 생성
  python tools/sync_shared_logic.py --check    # 최신인지만 확인 (CI용, 파일을 쓰지 않는다)
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
UNITY = REPO / "DetectivePrototype"
SCRIPTS = UNITY / "Assets/Scripts"
PROPS = REPO / "SharedLogicTests/SharedLogic.props"

UNITY_USING = re.compile(r"^\s*using\s+Unity(Engine|Editor)\b", re.MULTILINE)

# 순수 파일에 남아 있으면 테스트 빌드가 깨지는 것들. 미리 잡아 사람에게 알린다.
HAZARDS = {
    "UnityEngine 정규화 참조": re.compile(r"\bUnityEngine\s*\."),
    "Unity 전용 조건부 컴파일": re.compile(r"#if\s+UNITY"),
}


def pure_sources() -> list[Path]:
    """CLAUDE.md §18-1 판정과 같은 규칙. 정렬해서 props가 안정적으로 나오게 한다."""
    return [p for p in sorted(SCRIPTS.rglob("*.cs"))
            if not UNITY_USING.search(p.read_text(encoding="utf-8-sig"))]


def scan_hazards(files: list[Path]) -> list[str]:
    notes = []
    for p in files:
        body = p.read_text(encoding="utf-8-sig")
        # 주석 줄은 뺀다 — 이 저장소는 주석에서 JsonUtility·Vector2를 자주 언급한다.
        code = "\n".join(l for l in body.splitlines()
                         if not l.lstrip().startswith(("//", "///", "*")))
        for label, rx in HAZARDS.items():
            if rx.search(code):
                notes.append(f"{p.relative_to(SCRIPTS)}: {label}")
    return notes


def render(files: list[Path]) -> str:
    # 경로를 props 파일 자기 위치에 묶는다. 다른 폴더에서 Import해도 해석된다.
    anchor = "$(MSBuildThisFileDirectory)..\\DetectivePrototype\\Assets\\Scripts"
    lines = [
        "<!-- 생성 파일. 손으로 고치지 말고 `python tools/sync_shared_logic.py`를 다시 돌린다. -->",
        "<!-- Unity 프로젝트의 순수 C# 로직을 테스트가 그대로 컴파일한다 (복사가 아니라 참조). -->",
        "<Project>",
        "  <ItemGroup>",
    ]
    for p in files:
        rel = p.relative_to(SCRIPTS)
        src = anchor + "\\" + str(rel).replace("/", "\\")
        link = ("Shared/" + rel.as_posix()).replace("/", "\\")
        lines.append(f'    <Compile Include="{src}" Link="{link}" />')
    lines += ["  </ItemGroup>", "</Project>", ""]
    return "\n".join(lines)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="순수 로직 목록 생성")
    ap.add_argument("--check", action="store_true", help="쓰지 않고 최신인지만 확인한다")
    args = ap.parse_args(argv)

    if not SCRIPTS.is_dir():
        print(f"Unity 스크립트 폴더를 찾지 못했다: {SCRIPTS}", file=sys.stderr)
        return 2

    files = pure_sources()
    total = len(list(SCRIPTS.rglob("*.cs")))
    lines = sum(len(p.read_text(encoding="utf-8-sig").splitlines()) for p in files)
    print(f"순수 로직 {len(files)}/{total} 파일 · {lines:,}줄 (CLAUDE.md §18-1 판정)")

    hazards = scan_hazards(files)
    if hazards:
        print("\n주의 — 순수 파일에 Unity 흔적이 남아 있다. 테스트 빌드가 깨질 수 있다:")
        for h in hazards:
            print(f"  {h}")

    rendered = render(files)
    stale = (not PROPS.exists()) or PROPS.read_text(encoding="utf-8") != rendered

    if args.check:
        print(f"\nSharedLogic.props  {'낡음' if stale else '최신'}")
        if stale:
            print("--check 실패: `python tools/sync_shared_logic.py`를 돌려야 한다.", file=sys.stderr)
            return 1
        return 0

    PROPS.parent.mkdir(parents=True, exist_ok=True)
    PROPS.write_text(rendered, encoding="utf-8")
    print(f"\n{'갱신' if stale else '변화 없음'}: {PROPS.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
