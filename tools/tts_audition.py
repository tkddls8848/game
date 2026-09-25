#!/usr/bin/env python3
"""
비교 청취용 샘플 생성기 — 어느 공급자의 어느 목소리로 갈지 귀로 정하기 위한 도구.

이 게임의 퍼즐은 목소리에 이름을 붙이는 것이라, "다섯이 서로 다른 사람으로 들리는가"가
기능 요구사항이다. 사양표의 "한국어 41종"은 그것을 보증하지 않는다. 들어 봐야 안다.

하는 일은 둘뿐이다.
  1) --list-voices : 계정에서 실제로 쓸 수 있는 한국어 음성을 공급자에게 물어본다.
     음성 이름을 코드에 박아 두지 않는 이유가 이것이다 — 목록은 계정과 시점에 따라 다르고,
     틀린 이름을 넣으면 실행 시 알 수 없는 오류가 난다.
  2) (기본) 대본에서 고른 청취용 대사를 후보 음성마다 생성한다.

대사는 배역마다 3줄을 회차 앞·중·뒤에서 하나씩 고른다. 추임새는 빼고 중간 길이만 쓴다 —
너무 짧으면 음색을 판단할 수 없고, 너무 길면 듣는 데 지친다.

결과는 Assets 밖(AssetDownloads/audition/)에 떨어뜨린다. 청취용 파일이 Unity에 임포트돼
빌드에 섞이면 안 된다.

  python tools/tts_audition.py --provider azure --list-voices
  python tools/tts_audition.py --provider azure --voices ko-KR-Haena:MAI-Voice-2,ko-KR-Junho:MAI-Voice-2
  python tools/tts_audition.py --provider google --voices <이름1>,<이름2> --dry-run

키는 환경변수에서만 읽는다(tts_generate.py와 같다). 코드나 문서에 키를 적지 않는다.
"""
from __future__ import annotations

import argparse
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from tts_generate import (  # noqa: E402  (경로를 먼저 세워야 한다)
    PROVIDERS,
    _force_utf8_console,
    _require_env,
    load_script,
)

REPO = Path(__file__).resolve().parent.parent
DEFAULT_SCRIPT = REPO / "DetectivePrototype/Assets/Resources/GameData/cases/case_02/script.json"
DEFAULT_OUT = REPO / "AssetDownloads/audition"

LINES_PER_VOICE = 3
MIN_CHARS, MAX_CHARS = 18, 55


def _get(url: str, headers: dict[str, str]) -> bytes:
    req = urllib.request.Request(url, headers=headers, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.read()
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")[:400]
        raise SystemExit(f"음성 목록을 받지 못했다 (HTTP {e.code}): {body}")


def list_voices(provider_name: str, creds: dict) -> list[dict]:
    """계정에서 쓸 수 있는 한국어 음성. 이름을 추측하지 않고 공급자에게 묻는다."""
    if provider_name == "azure":
        region = creds["AZURE_SPEECH_REGION"]
        url = f"https://{region}.tts.speech.microsoft.com/cognitiveservices/voices/list"
        raw = _get(url, {"Ocp-Apim-Subscription-Key": creds["AZURE_SPEECH_KEY"]})
        out = []
        for v in json.loads(raw):
            if not v.get("Locale", "").startswith("ko-KR"):
                continue
            out.append({
                "name": v.get("ShortName", ""),
                "gender": v.get("Gender", ""),
                "styles": v.get("StyleList") or [],
            })
        return out

    if provider_name == "google":
        key = creds["GOOGLE_TTS_API_KEY"]
        url = f"https://texttospeech.googleapis.com/v1/voices?languageCode=ko-KR&key={key}"
        raw = _get(url, {})
        out = []
        for v in json.loads(raw).get("voices", []):
            out.append({
                "name": v.get("name", ""),
                "gender": v.get("ssmlGender", ""),
                "styles": [],
            })
        return out

    raise SystemExit(f"{provider_name} 는 음성 목록 조회를 넣지 않았다. --voices 로 직접 지정한다.")


def pick_lines(data: dict) -> dict[str, list[dict]]:
    """배역마다 회차 앞·중·뒤에서 한 줄씩. 대본이 바뀌어도 같은 규칙으로 따라간다."""
    by: dict[str, list[dict]] = {}
    for u in data["utterances"]:
        by.setdefault(u["voiceId"], []).append(u)

    picked: dict[str, list[dict]] = {}
    for voice_id, group in sorted(by.items()):
        group.sort(key=lambda u: u["startMs"])
        good = [u for u in group if MIN_CHARS <= len(u["text"]) <= MAX_CHARS] or group
        n = max(1, len(good) // LINES_PER_VOICE)
        chunks = [good[i * n:(i + 1) * n] for i in range(LINES_PER_VOICE)]
        picked[voice_id] = [c[len(c) // 2] for c in chunks if c]
    return picked


def main(argv=None) -> int:
    _force_utf8_console()
    ap = argparse.ArgumentParser(description="비교 청취용 샘플 생성")
    ap.add_argument("--provider", choices=sorted(PROVIDERS), default="azure")
    ap.add_argument("--script", type=Path, default=DEFAULT_SCRIPT)
    ap.add_argument("--out-dir", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--list-voices", action="store_true",
                    help="계정에서 쓸 수 있는 한국어 음성을 보여 주고 끝낸다")
    ap.add_argument("--voices", help="쉼표로 구분한 음성 이름. 이것들로 같은 대사를 각각 생성한다")
    ap.add_argument("--dry-run", action="store_true", help="호출 계획만 보여 준다")
    args = ap.parse_args(argv)

    provider = PROVIDERS[args.provider]
    need_key = not args.dry_run or args.list_voices
    creds = _require_env(provider.env) if need_key else {}

    if args.list_voices:
        voices = list_voices(args.provider, creds)
        print(f"{args.provider} 한국어 음성 {len(voices)}종\n")
        for v in sorted(voices, key=lambda x: (x["gender"], x["name"])):
            styles = f"  감정 {len(v['styles'])}종: {', '.join(v['styles'])}" if v["styles"] else ""
            print(f"  {v['gender']:<7} {v['name']}{styles}")
        print("\n이 중에서 고른 뒤 --voices 에 쉼표로 넘긴다.")
        return 0

    if not args.voices:
        ap.error("--voices 가 필요하다. 먼저 --list-voices 로 목록을 본다.")

    data = load_script(args.script)
    picked = pick_lines(data)
    names = [v.strip() for v in args.voices.split(",") if v.strip()]

    total_chars = sum(len(u["text"]) for us in picked.values() for u in us) * len(names)
    calls = sum(len(us) for us in picked.values()) * len(names)
    cost = total_chars / 1_000_000 * provider.price_per_million if provider.price_per_million else 0.0

    print(f"공급자 {args.provider} · 후보 음성 {len(names)}종")
    print(f"대사 {sum(len(us) for us in picked.values())}줄 × {len(names)}종 = 호출 {calls}건 / {total_chars:,}자")
    print(f"예상 비용 ${cost:.4f}" if cost else "예상 비용: 크레딧제 또는 무료 한도")
    print()
    for voice_id, us in picked.items():
        for u in us:
            print(f"  {voice_id} {u['id']:<6} {u['room'].replace('room_',''):<9} {len(u['text']):>2}자")
    print()

    if args.dry_run:
        print("--dry-run 이라 호출하지 않았다.")
        return 0

    out_root = args.out_dir / args.provider
    made = 0
    for name in names:
        safe = name.replace(":", "_").replace("/", "_")
        folder = out_root / safe
        folder.mkdir(parents=True, exist_ok=True)
        for voice_id, us in picked.items():
            for u in us:
                path = folder / f"{voice_id}_{u['id']}.{provider.ext}"
                if path.exists():
                    continue
                audio = provider.synth(creds, u["text"], name, {})
                path.write_bytes(audio)
                made += 1
                print(f"  ... {path.relative_to(REPO)}")

    index = out_root / "듣는_순서.txt"
    lines = [
        "비교 청취 안내",
        "",
        "같은 대사를 후보 음성마다 만들어 두었다. 폴더를 오가며 같은 파일 이름끼리 비교한다.",
        "",
        "보는 것이 아니라 듣고 정한다. 확인할 것은 셋이다.",
        "  1. 다섯 배역으로 골랐을 때 서로 다른 사람으로 들리는가 (이 게임의 퍼즐이 그것이다)",
        "  2. 벽 너머로 뭉갰을 때도 구분이 남는가 (옆 방 소리는 웅얼거림으로 나간다)",
        "  3. 연기 톤이 평탄하지 않은가 (거짓말·추궁·당황이 드러나야 단서가 된다)",
        "",
        "후보 음성:",
    ]
    lines += [f"  {n}" for n in names]
    lines += ["", "배역과 대사:"]
    for voice_id, us in picked.items():
        lines.append(f"  {voice_id}")
        for u in us:
            lines.append(f"    {voice_id}_{u['id']}.{provider.ext}  ({u['room'].replace('room_','')})  {u['text']}")
    index.write_text("\n".join(lines), encoding="utf-8")

    print(f"\n파일 {made}개 생성. 안내: {index.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
