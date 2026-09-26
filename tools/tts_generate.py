#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
script.json의 발화를 상업 TTS로 합성해 파일로 굽는다. (초안)

    python tools/tts_generate.py --provider azure --dry-run

설계 원칙
---------
1. **공급자 의존은 함수 하나로 격리한다.** 각 공급자는 `Provider` 하나이고,
   네트워크를 만지는 코드는 그 안의 `synth` 함수 **하나뿐**이다.
   공급자를 바꾸려면 `PROVIDERS`에 항목을 하나 더 쓰면 된다. 나머지 코드는 손대지 않는다.
2. **API 키는 환경변수에서만 읽는다.** 코드·문서·로그·리포트 어디에도 키를 적지 않는다.
   오류 메시지에 URL을 실을 때는 `_redact()`를 통과시킨다.
3. **기본은 dry-run이 아니라 실호출이지만, 키가 없으면 실행 자체를 거부한다.**
   `--dry-run`은 무엇을 호출할지만 출력하고 네트워크를 만지지 않으며 파일도 쓰지 않는다.

필요한 환경변수 (공급자별)
--------------------------
    azure       AZURE_SPEECH_KEY, AZURE_SPEECH_REGION      (예: koreacentral)
    google      GOOGLE_TTS_API_KEY
    elevenlabs  ELEVENLABS_API_KEY

출력
----
    DetectivePrototype/Assets/Resources/Audio/Voice/case_02/<utteranceId>.ogg

Unity에서는 확장자를 뺀 Resources 경로로 읽는다 → `Audio/Voice/case_02/u001`.
`Utterance.clip`(Assets/Scripts/Eavesdrop/UtteranceSchema.cs)에 넣을 값이 그것이다.
`--clip-map`을 주면 id → clip 경로 대응표를 JSON으로 따로 뽑아 준다
(이 스크립트는 script.json을 **수정하지 않는다**).

파일을 새로 넣은 뒤에는 Unity가 `.meta`를 만든다 → 그 뒤 커밋해서 GUID를 고정할 것.

표준 라이브러리만 쓴다. 외부 SDK 설치가 필요 없다.
"""

from __future__ import annotations

import argparse
import base64
import collections
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

# 저장소 루트 = 이 파일의 부모의 부모
REPO_ROOT = Path(__file__).resolve().parent.parent

DEFAULT_SCRIPT = REPO_ROOT / "DetectivePrototype/Assets/Resources/GameData/cases/case_02/script.json"
DEFAULT_OUT_DIR = REPO_ROOT / "DetectivePrototype/Assets/Resources/Audio/Voice/case_02"

# 시청(audition)용 출력. Assets 밖이다 — Unity가 임포트하면 안 되는 버리는 파일들이다.
AUDITION_OUT_DIR = REPO_ROOT / "tts_audition"

# 배역별 시청 대사. 대본에서 고른 실제 발화 id이고, 본문은 실행 시 script.json에서 읽는다
# (여기 텍스트를 복사해 두면 대본이 바뀔 때 어긋난다).
#   v1 클라라 — 담담한 설명 밑에 뭔가 깔린 줄
#   v2 마르코 — 의문문 + 불평. 억양이 드러난다
#   v3 줄리안 — 말줄임표로 시작하는 비꼼. 휴지 처리를 본다
#   v4 헬렌   — 평서 + 의문이 한 줄에. 침착한 톤
#   v5 에드먼드 — 숫자·말줄임표·자문자답. 노년 톤과 휴지를 한꺼번에 본다
AUDITION_IDS = {
    "v1": "u004",
    "v2": "u003",
    "v3": "u054",
    "v4": "u043",
    "v5": "u067",
}

# Resources.Load에 쓰는 경로의 접두사 (Assets/Resources/ 아래 기준, 확장자 없음)
RESOURCES_PREFIX = "Audio/Voice/case_02"

HTTP_TIMEOUT_SEC = 60


# --------------------------------------------------------------------------
# 비밀값 취급
# --------------------------------------------------------------------------

def _redact(s: str) -> str:
    """URL/메시지에서 키처럼 보이는 것을 가린다. 로그로 나가는 모든 문자열은 여기를 통과한다."""
    s = re.sub(r"([?&](?:key|api_key|apikey|token)=)[^&\s]+", r"\1<redacted>", s, flags=re.I)
    s = re.sub(r"(sk_|xi-api-key:\s*)[A-Za-z0-9_\-]{8,}", r"\1<redacted>", s)
    return s


def _require_env(names: list[str]) -> dict[str, str]:
    """환경변수를 읽는다. 값은 절대 출력하지 않는다."""
    out = {}
    missing = []
    for n in names:
        v = os.environ.get(n, "").strip()
        if not v:
            missing.append(n)
        else:
            out[n] = v
    if missing:
        raise SystemExit(
            "환경변수가 없다: " + ", ".join(missing) + "\n"
            "키는 셸에서 넣는다. 파일이나 커밋에 절대 적지 마라.\n"
            "  PowerShell:  $env:AZURE_SPEECH_KEY = '...'\n"
            "  bash:        export AZURE_SPEECH_KEY='...'"
        )
    return out


def _post(url: str, data: bytes, headers: dict[str, str]) -> bytes:
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=HTTP_TIMEOUT_SEC) as resp:
            return resp.read()
    except urllib.error.HTTPError as e:
        body = e.read()[:400].decode("utf-8", "replace")
        raise RuntimeError(f"HTTP {e.code} — {_redact(url)}\n{_redact(body)}") from None
    except urllib.error.URLError as e:
        raise RuntimeError(f"연결 실패 — {_redact(url)}: {e.reason}") from None


def _xml_escape(s: str) -> str:
    return (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
             .replace('"', "&quot;").replace("'", "&apos;"))


# --------------------------------------------------------------------------
# 공급자 — 여기가 전부다. 교체하려면 이 아래에 하나 더 쓴다.
# --------------------------------------------------------------------------

class Provider:
    """공급자 하나. 네트워크를 만지는 것은 self.synth 뿐이다."""

    def __init__(self, name, env, ext, price_per_million, max_chars,
                 default_voices, synth, billed_chars, candidates=(), note=""):
        self.name = name
        self.env = env                      # 필요한 환경변수 이름들
        self.ext = ext                      # 출력 확장자
        self.price_per_million = price_per_million   # USD / 1M자 (0 = 크레딧제)
        self.max_chars = max_chars          # 1회 호출 최대 문자 수
        self.default_voices = default_voices  # voiceId -> 공급자 음성 이름
        self.synth = synth                  # (creds, text, voice, style) -> bytes  ← 유일한 호출 지점
        self.billed_chars = billed_chars    # (text, voice, style) -> int
        self.candidates = list(candidates)  # 시청해 볼 만한 ko 음성 전체 (--list-voices)
        self.note = note


# ---- Azure AI Speech ------------------------------------------------------

def _azure_ssml(text: str, voice: str, style: dict) -> str:
    inner = _xml_escape(text)
    rate = style.get("rate")
    pitch = style.get("pitch")
    if rate or pitch:
        attrs = ""
        if rate:
            attrs += f' rate="{rate}"'
        if pitch:
            attrs += f' pitch="{pitch}"'
        inner = f"<prosody{attrs}>{inner}</prosody>"
    st = style.get("style")
    if st:
        deg = style.get("styledegree")
        deg_attr = f' styledegree="{deg}"' if deg else ""
        inner = f'<mstts:express-as style="{st}"{deg_attr}>{inner}</mstts:express-as>'
    return (
        '<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" '
        'xmlns:mstts="https://www.w3.org/2001/mstts" xml:lang="ko-KR">'
        f'<voice name="{voice}">{inner}</voice></speak>'
    )


def _azure_synth(creds: dict, text: str, voice: str, style: dict) -> bytes:
    region = creds["AZURE_SPEECH_REGION"]
    url = f"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1"
    ssml = _azure_ssml(text, voice, style)
    headers = {
        "Ocp-Apim-Subscription-Key": creds["AZURE_SPEECH_KEY"],
        "Content-Type": "application/ssml+xml",
        # Unity가 바로 읽는 OGG/Opus
        "X-Microsoft-OutputFormat": "ogg-24khz-16bit-mono-opus",
        "User-Agent": "detective-prototype-tts",
    }
    return _post(url, ssml.encode("utf-8"), headers)


def _azure_billed(text: str, voice: str, style: dict) -> int:
    """<speak>·<voice>를 뺀 나머지가 과금 대상이다(마크업 포함). 추정값."""
    ssml = _azure_ssml(text, voice, style)
    body = ssml.split(">", 1)[1]                      # <speak ...> 제거
    body = body.rsplit("</speak>", 1)[0]
    body = re.sub(r"</?voice[^>]*>", "", body)        # <voice>/</voice> 제거
    return len(body)


AZURE = Provider(
    name="azure",
    env=["AZURE_SPEECH_KEY", "AZURE_SPEECH_REGION"],
    ext=".ogg",
    price_per_million=15.0,          # S1 Neural. Neural HD는 22.0
    max_chars=5000,
    default_voices={
        # docs/TTS_OPTIONS.md §8 의 배역 배정안. 실제로 들어 보고 고칠 것.
        "v1": "ko-KR-Haena:MAI-Voice-2",   # 클라라 — 여, 감정 스타일 13종
        "v2": "ko-KR-Junho:MAI-Voice-2",   # 마르코 — 남, 감정 스타일 11종
        "v3": "ko-KR-HyunsuNeural",        # 줄리안 — 젊은 남자
        "v4": "ko-KR-SunHiNeural",         # 헬렌 — 여, 침착
        "v5": "ko-KR-InJoonNeural",        # 에드먼드 — 남, 노년 (rate/pitch로 낮춘다)
    },
    synth=_azure_synth,
    billed_chars=_azure_billed,
    candidates=[
        # 감정 스타일이 있는 HD 음성 (추리물 연기 톤에 유일하게 쓸 만하다)
        "ko-KR-Haena:MAI-Voice-2", "ko-KR-Junho:MAI-Voice-2",
        "ko-KR-SunHi:DragonHDLatestNeural", "ko-KR-Hyunsu:DragonHDLatestNeural",
        # 표준 neural — 여
        "ko-KR-SunHiNeural", "ko-KR-JiMinNeural", "ko-KR-SeoHyeonNeural",
        "ko-KR-SoonBokNeural", "ko-KR-YuJinNeural",
        # 표준 neural — 남
        "ko-KR-InJoonNeural", "ko-KR-BongJinNeural",
        "ko-KR-GookMinNeural", "ko-KR-HyunsuNeural",
    ],
    note="유료 S0 리소스를 쓸 것. 무료 F0로 뽑은 음성의 상업 이용은 근거가 약하다(docs/TTS_OPTIONS.md §1).",
)


# ---- Google Cloud Text-to-Speech -----------------------------------------

def _google_synth(creds: dict, text: str, voice: str, style: dict) -> bytes:
    key = creds["GOOGLE_TTS_API_KEY"]
    url = f"https://texttospeech.googleapis.com/v1/text:synthesize?key={key}"
    audio_cfg: dict = {"audioEncoding": "OGG_OPUS"}
    if style.get("speakingRate"):
        audio_cfg["speakingRate"] = float(style["speakingRate"])
    if style.get("pitchSemitones"):
        audio_cfg["pitch"] = float(style["pitchSemitones"])
    payload = {
        "input": {"text": text},
        "voice": {"languageCode": "ko-KR", "name": voice},
        "audioConfig": audio_cfg,
    }
    raw = _post(url, json.dumps(payload).encode("utf-8"),
                {"Content-Type": "application/json; charset=utf-8"})
    content = json.loads(raw.decode("utf-8")).get("audioContent")
    if not content:
        raise RuntimeError("Google 응답에 audioContent가 없다: " + _redact(raw.decode("utf-8", "replace")[:300]))
    return base64.b64decode(content)


GOOGLE = Provider(
    name="google",
    env=["GOOGLE_TTS_API_KEY"],
    ext=".ogg",
    price_per_million=30.0,          # Chirp 3: HD. Neural2는 16.0, WaveNet/Standard는 4.0
    max_chars=5000,
    default_voices={
        "v1": "ko-KR-Chirp3-HD-Leda",        # 여
        "v2": "ko-KR-Chirp3-HD-Charon",      # 남
        "v3": "ko-KR-Chirp3-HD-Puck",        # 남 (젊은 톤)
        "v4": "ko-KR-Chirp3-HD-Autonoe",     # 여
        "v5": "ko-KR-Chirp3-HD-Enceladus",   # 남 (낮은 톤)
    },
    synth=_google_synth,
    billed_chars=lambda text, voice, style: len(text),
    candidates=(
        ["ko-KR-Chirp3-HD-" + n for n in (
            # 여
            "Achernar", "Aoede", "Autonoe", "Callirrhoe", "Despina", "Erinome",
            "Gacrux", "Kore", "Laomedeia", "Leda", "Pulcherrima", "Sulafat",
            "Vindemiatrix", "Zephyr",
            # 남
            "Achird", "Algenib", "Algieba", "Alnilam", "Charon", "Enceladus",
            "Fenrir", "Iapetus", "Orus", "Puck", "Rasalgethi", "Sadachbia",
            "Sadaltager", "Schedar", "Umbriel", "Zubenelgenubi",
        )]
        + ["ko-KR-Neural2-A", "ko-KR-Neural2-B", "ko-KR-Neural2-C"]
        + ["ko-KR-Wavenet-A", "ko-KR-Wavenet-B", "ko-KR-Wavenet-C", "ko-KR-Wavenet-D"]
    ),
    note="Chirp 3: HD에는 감정 지정이 없다. 무료 한도 월 100만 자 — 재생성이 사실상 공짜다.",
)


# ---- ElevenLabs -----------------------------------------------------------

def _eleven_synth(creds: dict, text: str, voice: str, style: dict) -> bytes:
    # voice는 voice_id다(이름이 아니다). Voice Library/Voice Design에서 받아 온다.
    url = (f"https://api.elevenlabs.io/v1/text-to-speech/{voice}"
           f"?output_format=mp3_44100_128")
    payload = {
        "text": text,
        "model_id": style.get("model_id", "eleven_multilingual_v2"),
        "voice_settings": {
            "stability": float(style.get("stability", 0.5)),
            "similarity_boost": float(style.get("similarity_boost", 0.75)),
        },
    }
    return _post(url, json.dumps(payload).encode("utf-8"), {
        "xi-api-key": creds["ELEVENLABS_API_KEY"],
        "Content-Type": "application/json",
        "Accept": "audio/mpeg",
    })


ELEVENLABS = Provider(
    name="elevenlabs",
    env=["ELEVENLABS_API_KEY"],
    ext=".mp3",                      # ogg를 안 준다 → 별도 변환 필요
    price_per_million=0.0,           # 크레딧제 (1문자 = 1크레딧)
    max_chars=5000,
    default_voices={f"v{i}": f"<voice_id_v{i}>" for i in range(1, 6)},
    synth=_eleven_synth,
    billed_chars=lambda text, voice, style: len(text),
    note="ogg를 반환하지 않는다. .mp3로 받은 뒤 ffmpeg로 .ogg로 바꿔 넣어야 한다. "
         "유료 플랜에서만 상업 이용 가능. 목소리는 voice_id로 지정한다.",
)


PROVIDERS = {p.name: p for p in (AZURE, GOOGLE, ELEVENLABS)}


# --------------------------------------------------------------------------
# 대본 읽기
# --------------------------------------------------------------------------

def load_script(path: Path) -> dict:
    if not path.is_file():
        raise SystemExit(f"대본이 없다: {path}")
    data = json.loads(path.read_text(encoding="utf-8"))
    utts = data.get("utterances") or []
    if not utts:
        raise SystemExit(f"발화가 없다: {path}")

    seen = set()
    for u in utts:
        uid = u.get("id") or ""
        if not uid:
            raise SystemExit("id가 빈 발화가 있다.")
        if uid in seen:
            raise SystemExit(f"발화 id가 겹친다: {uid}")
        seen.add(uid)
        if not (u.get("text") or "").strip():
            raise SystemExit(f"{uid}: text가 비어 있다.")
        if not u.get("voiceId"):
            raise SystemExit(f"{uid}: voiceId가 없다.")
    return data


def load_json_map(path: str | None, what: str) -> dict:
    if not path:
        return {}
    p = Path(path)
    if not p.is_file():
        raise SystemExit(f"{what} 파일이 없다: {p}")
    return json.loads(p.read_text(encoding="utf-8"))


# --------------------------------------------------------------------------
# 계획 세우기
# --------------------------------------------------------------------------

Job = collections.namedtuple("Job", "uid voice_id provider_voice text style out_path chars billed")


def build_jobs(data, provider, voice_map, style_map, out_dir, args) -> list[Job]:
    only = {s.strip() for s in args.only.split(",")} if args.only else None
    jobs = []
    for u in data["utterances"]:
        uid, vid = u["id"], u["voiceId"]
        if only and uid not in only:
            continue
        if args.voice and vid != args.voice:
            continue
        pv = voice_map.get(vid)
        if not pv:
            raise SystemExit(
                f"{uid}: 목소리 '{vid}'에 대응하는 {provider.name} 음성이 없다.\n"
                f"--voice-map 으로 넘기거나 PROVIDERS['{provider.name}'].default_voices 를 채워라."
            )
        text = u["text"].strip()
        style = dict(style_map.get(vid, {}))
        style.update(style_map.get(uid, {}))     # 발화 단위 override
        out = out_dir / (uid + provider.ext)
        jobs.append(Job(uid, vid, pv, text, style, out,
                        len(text), provider.billed_chars(text, pv, style)))
        if args.limit and len(jobs) >= args.limit:
            break
    return jobs


def _safe(name: str) -> str:
    """음성 이름을 파일명에 쓸 수 있게 만든다 (ko-KR-Haena:MAI-Voice-2 → ko-KR-Haena_MAI-Voice-2)."""
    return re.sub(r"[^0-9A-Za-z._-]", "_", name)


def build_audition_jobs(data, provider, voice_map, style_map, out_dir, args) -> list[Job]:
    """
    배역별 시청용 대사 한 줄씩만 만든다.

    기본  : 배역 5개 × 각자의 대사 1줄 (현재 voice map 기준)
    비교용: --audition-voices 로 음성들을 지정하면 같은 대사 한 줄을 그 음성 전부로 만든다
    """
    by_id = {u["id"]: u for u in data["utterances"]}
    jobs = []

    if args.audition_voices:
        # 한 대사를 여러 음성으로 — 배역에 누구를 앉힐지 고를 때 쓴다
        line_id = args.audition_line or AUDITION_IDS.get(args.voice or "v5", "u067")
        u = by_id.get(line_id)
        if not u:
            raise SystemExit(f"--audition-line: 대본에 없는 발화 id다: {line_id}")
        names = [n.strip() for n in args.audition_voices.split(",") if n.strip()]
        if names == ["all"]:
            names = provider.candidates
            if not names:
                raise SystemExit(f"{provider.name}에는 등록된 후보 음성 목록이 없다. 이름을 직접 넘겨라.")
        for pv in names:
            style = dict(style_map.get(u["voiceId"], {}))
            out = out_dir / f"{line_id}__{_safe(pv)}{provider.ext}"
            jobs.append(Job(f"{line_id}@{pv}", u["voiceId"], pv, u["text"], style, out,
                            len(u["text"]), provider.billed_chars(u["text"], pv, style)))
        return jobs

    # 배역별 한 줄씩
    for vid in sorted(voice_map):
        if args.voice and vid != args.voice:
            continue
        line_id = AUDITION_IDS.get(vid)
        if not line_id:
            print(f"[건너뜀] {vid}: AUDITION_IDS에 시청 대사가 정해져 있지 않다.")
            continue
        u = by_id.get(line_id)
        if not u:
            raise SystemExit(f"{vid}: 대본에 없는 발화 id다: {line_id}")
        pv = voice_map[vid]
        style = dict(style_map.get(vid, {}))
        style.update(style_map.get(line_id, {}))
        out = out_dir / f"{vid}__{_safe(pv)}{provider.ext}"
        jobs.append(Job(f"{line_id}({vid})", vid, pv, u["text"], style, out,
                        len(u["text"]), provider.billed_chars(u["text"], pv, style)))
    return jobs


def report(jobs, provider, out_dir, existing, args) -> dict:
    # 한 대사를 여러 음성으로 굽는 중이면 배역이 아니라 음성이 비교 축이다
    per_provider_voice = bool(getattr(args, "audition_voices", None))
    by_voice = collections.defaultdict(lambda: {"calls": 0, "chars": 0, "billed": 0, "voice": ""})
    for j in jobs:
        b = by_voice[j.provider_voice if per_provider_voice else j.voice_id]
        b["calls"] += 1
        b["chars"] += j.chars
        b["billed"] += j.billed
        b["voice"] = j.provider_voice

    total_chars = sum(j.chars for j in jobs)
    total_billed = sum(j.billed for j in jobs)
    cost = total_billed * provider.price_per_million / 1_000_000.0
    too_long = [j.uid for j in jobs if j.billed > provider.max_chars]

    audition = getattr(args, "audition", False)
    print(f"\n=== TTS {'시청(audition)' if audition else '생성'} 계획 — provider={provider.name} ===")
    print(f"출력 디렉터리 : {out_dir}")
    if audition:
        print("               (Assets 밖이다. 다 듣고 나면 통째로 지워도 된다)")
    else:
        print(f"Resources 경로: {RESOURCES_PREFIX}/<id>   (확장자 없음 · Utterance.clip에 넣을 값)")
    print(f"확장자        : {provider.ext}")
    print(f"호출 수       : {len(jobs)}"
          + (f"  (이미 있어 건너뜀 {existing})" if existing else ""))
    print(f"문자 수       : {total_chars:,}  (과금 기준 추정 {total_billed:,})")
    if provider.price_per_million:
        print(f"예상 비용     : ${cost:.4f}  (@ ${provider.price_per_million:.2f} / 1M자, 무료 한도 미반영)")
    else:
        print(f"예상 소모     : {total_billed:,} 크레딧 (1문자 = 1크레딧)")

    if per_provider_voice:
        print("\n음성별 분포")
        print(f"  {'발화':>5} {'문자':>7} {'과금추정':>9}  공급자 음성")
        for key in sorted(by_voice):
            b = by_voice[key]
            print(f"  {b['calls']:>5} {b['chars']:>7,} {b['billed']:>9,}  {key}")
    else:
        print("\n목소리별 분포")
        print(f"  {'id':<5} {'발화':>5} {'문자':>7} {'과금추정':>9}  공급자 음성")
        for vid in sorted(by_voice):
            b = by_voice[vid]
            print(f"  {vid:<5} {b['calls']:>5} {b['chars']:>7,} {b['billed']:>9,}  {b['voice']}")

    if too_long:
        print(f"\n[경고] 1회 호출 한도({provider.max_chars}자)를 넘는 발화: {', '.join(too_long)}")
    if provider.ext != ".ogg":
        print(f"\n[경고] {provider.name}는 .ogg를 주지 않는다. {provider.ext}로 받은 뒤 변환해야 한다.")
    if provider.note:
        print(f"\n[참고] {provider.note}")

    if args.verbose:
        print("\n발화별")
        for j in jobs:
            print(f"  {j.uid}  {j.voice_id}→{j.provider_voice:<32} {j.chars:>3}자  "
                  f"style={j.style or '-'}  →  {j.out_path.name}")

    return {
        "provider": provider.name,
        "calls": len(jobs),
        "skipped_existing": existing,
        "total_chars": total_chars,
        "billed_chars_estimate": total_billed,
        "estimated_usd": round(cost, 4) if provider.price_per_million else None,
        "extension": provider.ext,
        "out_dir": str(out_dir),
        "resources_prefix": RESOURCES_PREFIX,
        "by_voice": {k: dict(v) for k, v in by_voice.items()},
        "oversize": too_long,
    }


# --------------------------------------------------------------------------
# main
# --------------------------------------------------------------------------

def _force_utf8_console() -> None:
    """Windows 콘솔 기본 코드페이지(cp949)에서 한글·기호가 터지는 것을 막는다."""
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:  # noqa: BLE001 — 재설정이 안 되는 환경이면 그냥 둔다
            pass


def main(argv=None) -> int:
    _force_utf8_console()
    ap = argparse.ArgumentParser(
        description="script.json의 발화를 상업 TTS로 합성해 파일로 굽는다.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="API 키는 환경변수로만 넘긴다. 절대 인자로 받지 않는다.",
    )
    ap.add_argument("--provider", choices=sorted(PROVIDERS), default="azure")
    ap.add_argument("--script", type=Path, default=DEFAULT_SCRIPT)
    ap.add_argument("--out-dir", type=Path, default=DEFAULT_OUT_DIR)
    ap.add_argument("--voice-map", help='JSON: {"v1": "<공급자 음성 이름>", ...}')
    ap.add_argument("--style-map",
                    help='JSON: {"v5": {"rate": "-8%%", "pitch": "-2st"}, "u123": {"style": "angry"}} '
                         "— 키는 voiceId 또는 발화 id")
    ap.add_argument("--dry-run", action="store_true",
                    help="무엇을 호출할지만 출력한다. 네트워크도 파일 쓰기도 없다.")
    ap.add_argument("--force", action="store_true", help="이미 있는 파일도 다시 만든다")
    ap.add_argument("--only", help="쉼표로 구분한 발화 id만")
    ap.add_argument("--voice", help="이 목소리(v1 등)만")
    ap.add_argument("--limit", type=int, help="앞에서 N개만 (시험용)")
    ap.add_argument("--clip-map", type=Path,
                    help="id → Resources clip 경로 대응표를 이 JSON으로 쓴다 (script.json은 건드리지 않는다)")
    ap.add_argument("--report", type=Path, help="계획/결과 요약을 이 JSON으로 쓴다")
    ap.add_argument("--verbose", action="store_true", help="발화별로 전부 출력")
    ap.add_argument("--audition", action="store_true",
                    help="전량 생성 대신 배역별 시청 대사 한 줄씩만 만든다 (기본 출력: <repo>/tts_audition)")
    ap.add_argument("--audition-voices",
                    help="같은 대사를 여러 음성으로 만든다. 쉼표로 구분한 음성 이름, 또는 'all'")
    ap.add_argument("--audition-line", help="시청에 쓸 발화 id (기본: AUDITION_IDS)")
    ap.add_argument("--list-voices", action="store_true",
                    help="이 공급자의 한국어 후보 음성을 출력하고 끝낸다")
    args = ap.parse_args(argv)

    provider = PROVIDERS[args.provider]

    if args.list_voices:
        _force_utf8_console()
        print(f"{provider.name} 한국어 후보 음성 {len(provider.candidates)}종")
        for n in provider.candidates:
            print("  " + n)
        print(f"\n한 대사로 전부 들어 보기:\n"
              f"  python tools/tts_generate.py --provider {provider.name} "
              f"--audition --audition-voices all")
        return 0

    data = load_script(args.script)

    voice_map = dict(provider.default_voices)
    voice_map.update(load_json_map(args.voice_map, "--voice-map"))
    style_map = load_json_map(args.style_map, "--style-map")

    if args.audition or args.audition_voices:
        args.audition = True
        # 시청 파일은 Assets 밖에 떨군다 — Unity가 임포트하면 안 된다
        out_dir = args.out_dir if args.out_dir != DEFAULT_OUT_DIR else AUDITION_OUT_DIR
        jobs = build_audition_jobs(data, provider, voice_map, style_map, out_dir, args)
    else:
        out_dir = args.out_dir
        jobs = build_jobs(data, provider, voice_map, style_map, out_dir, args)

    existing = 0
    if not args.force:
        kept = []
        for j in jobs:
            if j.out_path.is_file() and j.out_path.stat().st_size > 0:
                existing += 1
            else:
                kept.append(j)
        jobs = kept

    summary = report(jobs, provider, out_dir, existing, args)

    if args.clip_map:
        clips = {u["id"]: f"{RESOURCES_PREFIX}/{u['id']}" for u in data["utterances"]}
        if not args.dry_run:
            args.clip_map.parent.mkdir(parents=True, exist_ok=True)
            args.clip_map.write_text(json.dumps(clips, ensure_ascii=False, indent=2), encoding="utf-8")
            print(f"\nclip 대응표 → {args.clip_map}")
        else:
            print(f"\n[dry-run] clip 대응표 {len(clips)}줄을 {args.clip_map}에 쓸 예정")

    if args.dry_run:
        print("\n[dry-run] 네트워크 호출도 파일 쓰기도 하지 않았다.")
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
            print(f"리포트 → {args.report}")
        return 0

    if not jobs:
        print("\n할 일이 없다.")
        return 0

    creds = _require_env(provider.env)          # 키가 없으면 여기서 멈춘다
    out_dir.mkdir(parents=True, exist_ok=True)

    ok, failed = 0, []
    for i, j in enumerate(jobs, 1):
        try:
            audio = provider.synth(creds, j.text, j.provider_voice, j.style)   # ← 유일한 호출 지점
            if not audio:
                raise RuntimeError("빈 응답")
            j.out_path.write_bytes(audio)
            ok += 1
            print(f"[{i}/{len(jobs)}] {j.uid} {j.voice_id} {len(audio):>7,}B  → {j.out_path.name}")
        except Exception as e:                                   # noqa: BLE001
            failed.append(j.uid)
            print(f"[{i}/{len(jobs)}] {j.uid} 실패: {_redact(str(e))}", file=sys.stderr)

    print(f"\n완료 {ok} / 실패 {len(failed)}")
    if failed:
        print("실패한 발화: " + ", ".join(failed), file=sys.stderr)
    if args.audition:
        print(f"들어 볼 것: {out_dir}")
        print("마음에 드는 음성을 정했으면 --voice-map JSON에 적어서 전량 생성으로 넘어간다.")
    else:
        print("Unity에서 Resources 폴더를 다시 임포트하고, 새로 생긴 .meta를 커밋해 GUID를 고정할 것.")

    summary["written"] = ok
    summary["failed"] = failed
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"리포트 → {args.report}")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
