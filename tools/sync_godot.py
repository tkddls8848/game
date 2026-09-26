#!/usr/bin/env python3
"""
Unity 프로젝트의 순수 C# 로직을 Godot 프로젝트가 **같이 컴파일**하도록 묶어 준다.

복사하지 않는다. 복사하면 두 벌이 갈라지고, 갈라진 순간 "같은 게임"이 아니게 된다.
대신 Godot csproj가 Unity 쪽 원본 파일을 직접 가리키는 목록(SharedLogic.props)을 만든다.

어느 파일이 공유 대상인지는 CLAUDE.md의 판정 기준을 그대로 쓴다 —
`using UnityEngine` / `using UnityEditor`가 없는 파일. 즉 §18-1을 지킨 파일만 공유된다.
누가 로직 파일에 UnityEngine을 끌어들이면 그 파일은 조용히 빠지는 대신 **Godot 빌드가
컴파일 오류로 터진다**. 원칙이 무너지는 것을 사람이 눈치채기 전에 기계가 먼저 안다.

데이터(JSON)와 폰트는 Godot이 res:// 밖을 못 읽으므로 복사한다. 이쪽은 생성물이라
git에서 빼고, 원본은 Unity 프로젝트 하나로 유지한다.

  python tools/sync_godot.py           # 생성
  python tools/sync_godot.py --check   # 최신인지만 확인 (CI용, 파일을 쓰지 않는다)
"""
from __future__ import annotations

import argparse
import filecmp
import re
import shutil
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
UNITY = REPO / "DetectivePrototype"
SCRIPTS = UNITY / "Assets/Scripts"
GODOT = REPO / "DetectiveGodot"

PROPS = GODOT / "SharedLogic.props"
DATA_SRC = UNITY / "Assets/Resources/GameData"
DATA_DST = GODOT / "data"
FONT_SRC = UNITY / "Assets/Resources/Fonts/NotoSerifKR-Regular.otf"
FONT_DST = GODOT / "fonts/NotoSerifKR-Regular.otf"

RESOURCES = UNITY / "Assets/Resources"
MEDIA_DST = GODOT / "media"

# art.json이 가리키는 그림·소리. Godot은 res:// 밖을 읽지 못하므로 사본이 필요하다.
# 경로를 art.json의 값(`Art/Floors/...`)과 **같은 모양**으로 유지한다 — 그래야 한쪽 코드가
# 매니페스트를 그대로 읽는다. glob은 얕게 쓴다(SFX/Additional의 50여 개는 아직 쓰지 않는다).
MEDIA = [
    ("Art/Floors", "*.jpg"),
    ("Art/Floors", "*.png"),
    ("Art/Portraits", "*.png"),
    ("Audio/BGM", "*.ogg"),
    ("Audio/Ambient", "*.ogg"),
    ("Audio/SFX", "*.ogg"),
]

UNITY_USING = re.compile(r"^\s*using\s+Unity(Engine|Editor)\b", re.MULTILINE)
# 순수 파일에 남아 있으면 Godot 빌드가 깨지는 것들. 미리 잡아 사람에게 알린다.
HAZARDS = {
    "UnityEngine 정규화 참조": re.compile(r"\bUnityEngine\s*\."),
    "Unity 전용 조건부 컴파일": re.compile(r"#if\s+UNITY"),
}


def pure_sources() -> list[Path]:
    """CLAUDE.md의 §18-1 판정과 같은 규칙. 정렬해서 props가 안정적으로 나오게 한다."""
    out = []
    for p in sorted(SCRIPTS.rglob("*.cs")):
        if not UNITY_USING.search(p.read_text(encoding="utf-8-sig")):
            out.append(p)
    return out


def scan_hazards(files: list[Path]) -> list[str]:
    notes = []
    for p in files:
        body = p.read_text(encoding="utf-8-sig")
        # 주석 줄은 빼고 본다 — 이 저장소는 주석에서 JsonUtility/Vector2를 자주 언급한다.
        code = "\n".join(l for l in body.splitlines() if not l.lstrip().startswith(("//", "///", "*")))
        for label, rx in HAZARDS.items():
            if rx.search(code):
                notes.append(f"{p.relative_to(SCRIPTS)}: {label}")
    return notes


def render_props(files: list[Path]) -> str:
    # 경로를 props 파일 자기 위치에 묶는다($(MSBuildThisFileDirectory)) — 그래야 Godot 게임과
    # 테스트 프로젝트가 서로 다른 폴더에서 같은 목록을 Import할 수 있다.
    anchor = "$(MSBuildThisFileDirectory)..\\DetectivePrototype\\Assets\\Scripts"
    lines = [
        "<!-- 생성 파일. 손으로 고치지 말고 `python tools/sync_godot.py`를 다시 돌린다. -->",
        "<!-- Unity 프로젝트의 순수 C# 로직을 Godot이 같이 컴파일한다 (복사가 아니라 참조). -->",
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


def sync_tree(src: Path, dst: Path, pattern: str, check: bool) -> tuple[int, list[str]]:
    """src의 pattern 파일을 dst로 맞춘다. check면 쓰지 않고 차이만 센다."""
    stale: list[str] = []
    count = 0
    wanted = sorted(src.rglob(pattern))
    for s in wanted:
        d = dst / s.relative_to(src)
        count += 1
        if d.exists() and filecmp.cmp(s, d, shallow=False):
            continue
        stale.append(str(d.relative_to(GODOT)))
        if not check:
            d.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(s, d)
    # 원본에서 사라진 것은 지운다 — 남아 있으면 Godot이 옛 데이터를 읽는다.
    if dst.exists():
        keep = {(dst / s.relative_to(src)).resolve() for s in wanted}
        for d in sorted(dst.rglob(pattern)):
            if d.resolve() not in keep:
                stale.append(f"{d.relative_to(GODOT)} (원본에 없음)")
                if not check:
                    d.unlink()
    return count, stale


def sync_media(check: bool) -> tuple[int, list[str]]:
    """art.json이 가리키는 그림·소리를 media/ 로 맞춘다. 폴더 구조는 그대로 둔다."""
    stale: list[str] = []
    count = 0
    for folder, pattern in MEDIA:
        src_dir = RESOURCES / folder
        if not src_dir.is_dir():
            stale.append(f"{folder} (원본 폴더 없음)")
            continue
        for s in sorted(src_dir.glob(pattern)):
            d = MEDIA_DST / folder / s.name
            count += 1
            if d.exists() and filecmp.cmp(s, d, shallow=False):
                continue
            stale.append(str(d.relative_to(GODOT)))
            if not check:
                d.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(s, d)
    return count, stale


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Unity 순수 로직 → Godot 공유")
    ap.add_argument("--check", action="store_true", help="쓰지 않고 최신인지만 확인한다")
    args = ap.parse_args(argv)

    if not SCRIPTS.is_dir():
        print(f"Unity 스크립트 폴더를 찾지 못했다: {SCRIPTS}", file=sys.stderr)
        return 2

    files = pure_sources()
    total = len(list(SCRIPTS.rglob("*.cs")))
    lines = sum(len(p.read_text(encoding="utf-8-sig").splitlines()) for p in files)
    print(f"공유 대상 {len(files)}/{total} 파일 · {lines:,}줄 (§18-1을 지킨 파일)")

    hazards = scan_hazards(files)
    if hazards:
        print("\n주의 — 순수 파일에 Unity 흔적이 남아 있다. Godot 빌드가 깨질 수 있다:")
        for h in hazards:
            print(f"  {h}")

    GODOT.mkdir(parents=True, exist_ok=True)
    props = render_props(files)
    props_stale = (not PROPS.exists()) or PROPS.read_text(encoding="utf-8") != props
    if props_stale and not args.check:
        PROPS.write_text(props, encoding="utf-8")

    n_json, json_stale = sync_tree(DATA_SRC, DATA_DST, "*.json", args.check)

    font_stale = not (FONT_DST.exists() and FONT_SRC.exists()
                      and filecmp.cmp(FONT_SRC, FONT_DST, shallow=False))
    if font_stale and not args.check:
        if not FONT_SRC.exists():
            print(f"\n한글 폰트를 찾지 못했다: {FONT_SRC}", file=sys.stderr)
            print("Godot 기본 폰트에는 한글 글리프가 없어서 글자가 전부 □로 나온다.", file=sys.stderr)
            return 2
        FONT_DST.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(FONT_SRC, FONT_DST)

    n_media, media_stale = sync_media(args.check)

    print(f"\nSharedLogic.props  {'갱신' if props_stale else '최신'}")
    print(f"data/ (JSON {n_json}개)   {'갱신 ' + str(len(json_stale)) + '건' if json_stale else '최신'}")
    print(f"media/ (그림·소리 {n_media}개)  {'갱신 ' + str(len(media_stale)) + '건' if media_stale else '최신'}")
    print(f"fonts/NotoSerifKR   {'갱신' if font_stale else '최신'}")

    if args.check and (props_stale or json_stale or media_stale or font_stale):
        print("\n--check 실패: `python tools/sync_godot.py`를 돌려야 한다.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
