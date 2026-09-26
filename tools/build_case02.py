#!/usr/bin/env python3
"""
case_02 시나리오 생성기 — 장면을 1차 자료로 두고 동선·대본·소리를 파생시킨다.

## 왜 생성기인가

이 시나리오는 세 가지가 서로 맞물려야 성립한다.
  * 두 사람이 **대화**하려면 그 시각에 **같은 방**에 있어야 한다 (동선)
  * 단서는 그 대화에서 **흘러나와야** 한다 (독백으로 설명하면 게임이 아니다)
  * 그러면서 **한 방에 서서는 전부 들을 수 없어야** 한다 (§5 보장)

손으로 세 파일(script/tracks/events)을 따로 적으면 이 셋이 반드시 어긋난다.
그래서 **장면**만 적고 나머지를 뽑아낸다. 장면에 누가 있는지가 곧 동선이므로,
"대화하는데 그 방에 없는 사람" 같은 모순이 구조적으로 생길 수 없다.

## 이전 대본의 문제 (실측)

발화 267개 중 **43%가 독백**이었고 주고받는 대화에 속한 것은 **15%**였다.
이동은 사람당 2.6회, 첫 3분은 전원 정지. 사람들이 대부분 혼자 중얼거렸다.

## 한국어와 영어를 같이 쓴다

나중에 번역하면 대본이 바뀔 때마다 어긋난다. 여기서 두 언어를 나란히 적고
script.json(한국어)과 locale/case_02.script.en.json(영어)을 함께 내보낸다.

  python tools/build_case02.py            # 생성
  python tools/build_case02.py --check    # 최신인지만 확인
"""
from __future__ import annotations

import argparse
import json
import pathlib
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
DATA = REPO / "DetectivePrototype/Assets/Resources/GameData"
CASE = DATA / "cases/case_02"
LOCALE = DATA / "locale"

RUN_MS = 600_000          # 회차 10분 (8:10 ~ 8:20)
SWAP_MS = 241_500         # 바꿔치기 순간 = 8시 14분 01.5초 (case_02.json의 정답 시각)

# 인물. 목소리 번호는 대본이 쓰고, 이름은 플레이어가 알아내야 하는 것이다.
VICTIM, CLARA, MARCO, JULIAN, HELEN = "npc_victim", "npc_a", "npc_b", "npc_c", "npc_d"
VOICE = {VICTIM: "v5", CLARA: "v1", MARCO: "v2", JULIAN: "v3", HELEN: "v4"}

STUDY, GUEST, HALL, LOBBY, DINING, STORAGE = (
    "room_victim", "room_suspect", "room_hall", "room_lobby", "room_dining", "room_storage")

# 발화 길이 추정. 한국어 기준으로 잡고 영어가 길어도 타이밍은 한국어가 정한다
# (음성을 넣을 때 VoiceClipPlan이 실제 길이로 다시 검사한다).
MS_BASE = 850
MS_PER_CHAR = 128
MS_GAP = 420              # 말과 말 사이 숨


class Line:
    """대사 한 줄. fact를 달면 그 발화가 그 사실을 나르는 유일한 근거가 된다."""

    def __init__(self, npc, ko, en, fact=None, pause=0, at=None):
        self.npc = npc
        self.ko = ko
        self.en = en
        self.fact = fact
        self.pause = pause      # 이 줄 **앞**에 더 둘 침묵(ms)
        # 이 줄이 반드시 울려야 하는 절대 시각(ms). 8시 14분처럼 다른 방의 소리와
        # 동시에 나야 하는 줄에만 쓴다. 앞 대사가 밀려 이 시각을 넘기면 오류로 멈춘다.
        self.at = at

    @property
    def duration(self):
        return MS_BASE + len(self.ko) * MS_PER_CHAR


class Scene:
    """한 방에서 한 시간대에 벌어지는 일. who가 곧 그 시각의 동선이다."""

    def __init__(self, sid, room, start, end, who, lines=(), note=""):
        self.sid = sid
        self.room = room
        self.start = start
        self.end = end
        self.who = list(who)
        self.lines = list(lines)
        self.note = note        # 작가 메모. 데이터에 나가지 않는다


def L(npc, ko, en, fact=None, pause=0, at=None):
    return Line(npc, ko, en, fact, pause, at)


# ═══════════════════════════════════════════════════════════════════
#  장면
#
#  인물의 상황:
#    에드먼드 — 심장이 약하다. 니트로 알약 없이는 발작을 못 넘긴다. 오늘 밤
#               헬렌의 1907년 기록을 들이밀고 서명을 받아 낼 작정이다.
#    헬렌   — 주치의. 기록 조작이 발각됐다. 오늘 밤 안에 처리해야 한다.
#             색과 크기가 같은 설탕 알약을 가지고 왔다. **범인**
#    클라라 — 개인 비서. 장부를 관리한다. 에드먼드가 무언가 캐고 있다는 걸 안다.
#    마르코 — 요리사. 어릴 때 이 집에서 빵을 얻어먹었다. 창고를 드나든다. 눈치가 빠르다.
#    줄리안 — 조카. 빚이 있고 유산을 기대한다. 삼촌에게 돈을 부탁하려 한다.
#
#  사실이 놓이는 방 (한 방이 전부 듣지 못하게 갈라 둔다):
#    서재   voice4 · voice5 · blackmail · drawer_place
#    식당   voice1 · voice2 · edmund_heart_pills · helen_left_storage
#    로비   voice3
#    손님방 helen_past
#    복도   clock_814 · swap_moment
#    창고   bottle_in_coal · sugar_pills
#
#  1회차로는 못 모으는 근거: swap_moment(복도)와 edmund_heart_pills(식당)가
#  8시 14분에 동시에 울린다. 둘 다 발화가 하나뿐이므로 어느 조합으로도 겹침을 피할 수 없다.
# ═══════════════════════════════════════════════════════════════════

SCENES = [
    # ─── 1막 : 저녁 전 ────────────────────────────────────────────
    Scene("a_study", STUDY, 0, 99_000, [VICTIM, HELEN], note="진찰인 척하는 협박", lines=[
        L(HELEN, "팔 걷어 주세요, 에드먼드 헤일 선생님.",
          "Roll up your sleeve, Mr Edmund Hale.", fact="fact_voice5_is_edmund"),
        L(VICTIM, "매일 같은 자리를 누르는군.",
          "You press the same spot every day."),
        L(HELEN, "같은 자리가 가장 정직합니다.",
          "The same spot tells the truth best."),
        L(VICTIM, "오늘은 손이 차오.",
          "Your hands are cold today."),
        L(HELEN, "맥이 어제보다 고르지 않습니다. 저녁은 가볍게 드세요.",
          "Your pulse is less even than yesterday. Eat lightly tonight."),
        L(VICTIM, "헬렌 선생, 그 가방 열기 전에 들을 얘기가 있소.",
          "Doctor Moreau. Before you open that bag, there is something you need to hear.",
          fact="fact_voice4_is_helen", pause=800),
        L(HELEN, "말씀하세요.", "Go on."),
        L(VICTIM, "내가 당신 예전 병원에 사람을 보냈소. 1907년 겨울 기록을 봤소. 당신이 고친 그 줄도.",
          "I sent someone to your old hospital. I have read the winter of 1907. And the line you altered.",
          fact="fact_blackmail", pause=900),
        L(HELEN, "…….", "…", pause=1400),
        L(VICTIM, "앉으시오. 서 있으면 손이 더 떨리오.",
          "Sit down. Your hands shake more when you stand."),
        L(HELEN, "그 기록은 열아홉 해 전 것입니다.",
          "That record is nineteen years old."),
        L(VICTIM, "사람이 죽은 건 열아홉 해 전이 아니지. 지금도 죽어 있소.",
          "The person did not die nineteen years ago. They are still dead now."),
        L(HELEN, "무엇을 원하십니까.", "What do you want."),
        L(VICTIM, "겁주려는 게 아니오. 서명 하나만 받으면 되오. 내일 아침까지 생각해 보시오.",
          "I am not trying to frighten you. I need one signature. Think it over by morning."),
        L(HELEN, "무슨 서명입니까.", "A signature on what."),
        L(VICTIM, "그건 내일 보면 아오.",
          "You will see it tomorrow."),
        L(HELEN, "그 종이는 지금 어디 있습니까.",
          "Where is that paper now?"),
        L(VICTIM, "그건 당신이 알 일이 아니고. 약은 늘 두는 곳에 두시오 — 책상 오른쪽 서랍.",
          "That is not yours to know. Leave the medicine where it always goes — the right-hand drawer of the desk.",
          fact="fact_drawer_place", pause=700),
        L(HELEN, "스물네 알 채워 두었습니다. 발작이 오면 혀 밑에 넣으세요.",
          "I have left it filled with twenty-four. If an attack comes, under the tongue."),
        L(VICTIM, "삼십 년 해 온 일이오.", "I have been doing it thirty years."),
        L(HELEN, "삼십 년 동안 한 번도 늦은 적이 없으셨죠.",
          "And in thirty years you have never once been late with it."),
        L(VICTIM, "늦으면 끝이니까.",
          "Because late is the end of it."),
        L(HELEN, "가방을 두고 가겠습니다. 저녁 뒤에 다시 오겠습니다.",
          "I will leave my bag. I will come back after dinner."),
        L(VICTIM, "문은 닫고 가시오.", "Close the door behind you."),
    ]),

    Scene("a_dining", DINING, 0, 92_000, [MARCO, CLARA], note="저녁 준비. 두 이름이 여기서 나온다", lines=[
        L(MARCO, "오늘 수프는 소금을 덜 넣었습니다. 선생님 심장 때문에.",
          "I put less salt in the soup tonight. Because of his heart."),
        L(CLARA, "잘했어요.", "Good."),
        L(MARCO, "그런데 접시는 다섯 개입니까.",
          "Five places, then?"),
        L(CLARA, "다섯이에요. 조카가 왔어요.",
          "Five. The nephew is here."),
        L(MARCO, "이름이 마르코 맞으시죠? 어릴 때 여기서 빵을 얻어먹었습니다.",
          "It is Marco, isn't it? I used to be given bread here when I was small.",
          fact="fact_voice2_is_marco"),
        L(CLARA, "알아요. 그때 빵을 준 사람이 나였어요.",
          "I know. I was the one who gave it to you."),
        L(MARCO, "…기억 안 나실 줄 알았습니다.",
          "…I thought you wouldn't remember."),
        L(CLARA, "이 집 일은 다 기억해요. 그게 내 일이에요.",
          "I remember everything in this house. That is my work."),
        L(MARCO, "오늘은 장부를 내드릴까요?",
          "Shall I bring the ledger out tonight?"),
        L(CLARA, "오늘은 안 찾으실 거예요. 클라라 씨가 그렇게 말했다고 전해도 좋아요.",
          "He won't be asking for it tonight. You can tell him Miss Clara said so.",
          fact="fact_voice1_is_clara"),
        L(MARCO, "여섯째 줄 때문입니까.",
          "Is it because of the sixth line?"),
        L(CLARA, "그 얘긴 그만해요.", "Enough of that."),
        L(MARCO, "지워진 줄이 하나 있다고 들었습니다. 지운 사람 얘기도요.",
          "I heard there is a line that was erased. And talk of who erased it."),
        L(CLARA, "들은 걸 다 옮기면 이 집에서 못 살아요.",
          "Repeat everything you hear and you won't last in this house."),
        L(MARCO, "…접시 나르겠습니다.", "…I'll carry the plates."),
        L(CLARA, "마르코.", "Marco."),
        L(MARCO, "네.", "Yes."),
        L(CLARA, "오늘은 아무것도 보지 않은 걸로 해요. 부탁이에요.",
          "Tonight, you saw nothing. I'm asking you."),
        L(MARCO, "무엇을 보면 안 됩니까.",
          "What is it I shouldn't see?"),
        L(CLARA, "…모르겠어요. 그냥 그런 밤이에요.",
          "…I don't know. It is only that kind of night."),
    ]),

    Scene("a_guest", GUEST, 6_000, 88_000, [JULIAN], note="줄리안은 아직 혼자다 — 소리만 낸다", lines=[]),

    # ─── 2막 : 흩어진다 ───────────────────────────────────────────
    Scene("b_lobby", LOBBY, 128_000, 212_000, [CLARA, JULIAN],
          note="조카의 빚. 줄리안 이름이 여기서 나온다", lines=[
        L(CLARA, "편지에 뭐라고요, 줄리안?",
          "What does the letter say, Julian?", fact="fact_voice3_is_julian"),
        L(JULIAN, "이달 말까지랍니다. 이달 말.",
          "By the end of this month, it says. This month."),
        L(CLARA, "얼마예요.", "How much."),
        L(JULIAN, "말하면 표정이 변할 겁니다.",
          "Your face would change if I told you."),
        L(CLARA, "이미 변했어요.", "It already has."),
        L(JULIAN, "이 집 한 채 값입니다.",
          "The price of this house."),
        L(CLARA, "…….", "…", pause=1100),
        L(JULIAN, "삼촌은 갚아 줄 수 있어요. 갚아 줄 마음이 없을 뿐이죠.",
          "My uncle can pay it. He simply has no wish to."),
        L(CLARA, "삼촌한테 말할 생각이에요?",
          "Do you mean to tell him?"),
        L(JULIAN, "오늘 밤에. 저녁 종 전에요.",
          "Tonight. Before the dinner bell."),
        L(CLARA, "지금은 모로 선생하고 얘기 중이에요. 문이 닫혀 있었어요.",
          "He is with Doctor Moreau just now. The door was shut."),
        L(JULIAN, "닫혀 있었다고요. 진찰에 문을 닫습니까.",
          "Shut, you say. Does one shut the door for an examination?"),
        L(CLARA, "나는 아무것도 못 봤어요.",
          "I saw nothing."),
        L(JULIAN, "비서가 아무것도 못 보는 집이군요.",
          "A house where the secretary sees nothing."),
        L(CLARA, "보는 게 일이 아니에요. 적는 게 일이죠.",
          "Seeing is not my work. Writing it down is."),
        L(JULIAN, "그럼 적힌 것 중에 이상한 게 있었습니까.",
          "Then was there anything strange among the things you wrote?"),
        L(CLARA, "…지난달에 약값이 두 번 나갔어요. 같은 약이 두 번.",
          "…Last month the medicine was paid for twice. The same medicine, twice."),
        L(JULIAN, "누가 받았습니까.", "Who received it?"),
        L(CLARA, "그건 적혀 있지 않았어요.",
          "That was not written down."),
    ]),

    Scene("b_storage", STORAGE, 132_000, 210_000, [HELEN, MARCO],
          note="마르코가 창고에서 헬렌과 마주친다. 어색한 장면", lines=[
        L(MARCO, "선생님? 여기서 뭘 찾으십니까.",
          "Doctor? What are you looking for down here?"),
        L(HELEN, "약 가방을 두고 온 것 같아서요.",
          "I thought I had left my bag."),
        L(MARCO, "가방은 위에 두고 오셨다고 하셨는데요.",
          "You said you had left it upstairs."),
        L(HELEN, "…그렇군요.", "…So I did."),
        L(MARCO, "여기는 석탄하고 와인밖에 없습니다.",
          "There is nothing down here but coal and wine."),
        L(HELEN, "석탄은 누가 넣습니까.",
          "Who fills the coal?"),
        L(MARCO, "제가 아침에 넣습니다. 왜요.",
          "I do, in the mornings. Why?"),
        L(HELEN, "하루에 한 번이면 밤에는 아무도 안 오겠군요.",
          "Once a day. Then nobody comes down at night."),
        L(MARCO, "…그렇습니다.", "…That's right."),
        L(HELEN, "수프 냄새가 좋네요.",
          "The soup smells good."),
        L(MARCO, "선생님.", "Doctor."),
        L(HELEN, "네.", "Yes."),
        L(MARCO, "손에 재가 묻으셨습니다.",
          "There is ash on your hand."),
        L(HELEN, "…통에 손을 짚었어요. 어두워서.",
          "…I steadied myself on the bin. It is dark."),
        L(MARCO, "불을 켜 드릴까요.",
          "Shall I bring a light?"),
        L(HELEN, "아니에요. 올라가겠습니다.",
          "No. I am going up."),
    ]),

    Scene("b_study_alone", STUDY, 100_000, 200_000, [VICTIM],
          note="에드먼드 혼자. 장부를 넘긴다 — 소리만", lines=[]),

    # ─── 3막 : 8시 14분 ───────────────────────────────────────────
    Scene("c_dining", DINING, 218_000, 360_000, [VICTIM, MARCO],
          note="에드먼드가 자기 약을 설명한다. 그 순간 위층에서 약이 바뀌고 있다", lines=[
        L(VICTIM, "수프는 됐고. 앉게, 마르코.",
          "The soup will do. Sit down, Marco."),
        L(MARCO, "저는 서 있겠습니다.",
          "I'll stand, sir."),
        L(VICTIM, "삼십 년 전에 자네 아버지가 이 자리에 앉았어.",
          "Thirty years ago your father sat in that chair."),
        L(MARCO, "기억합니다.", "I remember."),
        L(VICTIM, "자네 아버지는 나한테 거짓말을 한 번도 안 했지.",
          "Your father never lied to me. Not once."),
        L(MARCO, "…네.", "…No, sir."),
        # ── 8시 14분. 복도의 서랍 소리와 동시에 울린다 ──
        L(VICTIM, "그 병 없이는 발작이 왔을 때 십 분을 못 버티네. 그러니 서랍에서 절대 빼지 말라고 했지.",
          "Without that bottle I won't last ten minutes once an attack starts. That is why I said never take it out of the drawer.",
          fact="fact_edmund_heart_pills", at=239_000),
        L(MARCO, "모로 선생이 잘 챙기실 겁니다.",
          "Doctor Moreau will see to it."),
        L(VICTIM, "그렇겠지.", "I suppose she will."),
        L(MARCO, "…선생님.", "…Sir."),
        L(VICTIM, "말하게.", "Speak."),
        L(MARCO, "아까 복도에서 모로 선생이 창고 문에서 나오는 걸 봤습니다. 치마에 석탄 가루가 묻어 있었고요.",
          "A while ago I saw Doctor Moreau come out of the cellar door, in the corridor. There was coal dust on her skirt.",
          fact="fact_helen_left_storage", pause=1200),
        L(VICTIM, "석탄 가루.", "Coal dust."),
        L(MARCO, "손에도 묻어 있었습니다. 제가 물었더니 통에 손을 짚었다고 하셨습니다.",
          "On her hand as well. When I asked, she said she had steadied herself on the bin."),
        L(VICTIM, "창고에는 뭐가 있나.",
          "What is kept in that cellar."),
        L(MARCO, "석탄하고 와인입니다. 그리고 재요.",
          "Coal and wine. And ash."),
        L(VICTIM, "…재.", "…Ash."),
        L(MARCO, "제가 잘못 봤을 수도 있습니다.",
          "I may have been mistaken."),
        L(VICTIM, "아니야. 자네 눈은 좋았어.",
          "No. Your eyes were always good."),
        L(MARCO, "선생님, 약을 지금 확인해 보시겠습니까.",
          "Sir — would you care to check your medicine now?"),
        L(VICTIM, "저녁 종에 먹으면 되네. 늘 그랬어.",
          "I take it at the bell. I always have."),
        L(MARCO, "…네.", "…Yes, sir."),
        L(VICTIM, "마르코. 자네 아버지 얘기를 왜 했는지 아나.",
          "Marco. Do you know why I spoke of your father?"),
        L(MARCO, "모르겠습니다.", "I don't, sir."),
        L(VICTIM, "이 집에 정직한 사람이 몇 남았는지 세어 보고 있었네.",
          "I was counting how many honest people are left in this house."),
    ]),

    Scene("c_hall", HALL, 216_000, 278_000, [CLARA, JULIAN],
          note="복도. 서재 소리를 들을 수 있는 유일한 곳이다", lines=[
        L(JULIAN, "여기서 기다릴 겁니다. 나오시면 바로 말할 거예요.",
          "I'll wait here. I'll speak to him the moment he comes out."),
        L(CLARA, "모로 선생이 아직 안에 있어요.",
          "Doctor Moreau is still inside."),
        L(JULIAN, "삼촌은 아까 식당으로 내려가셨는데요.",
          "But my uncle went down to the dining room a while ago."),
        L(CLARA, "…그럼 저 안에 혼자 있는 거예요.",
          "…Then she is in there alone."),
        L(CLARA, "벽시계가 이제 여덟 시 십사 분이군요. 저녁 종까지 십육 분.",
          "The wall clock says fourteen minutes past eight now. Sixteen minutes to the bell.",
          fact="fact_clock_814", at=234_500),
        # ── 8시 14분. 식당의 약 이야기와 동시에 울린다 ──
        L(JULIAN, "쉿. 서재에서 서랍 여는 소리요. 지금 저 안엔 모로 선생 혼자일 텐데.",
          "Hush. That is a drawer opening in the study. There should be no one in there but Doctor Moreau.",
          fact="fact_swap_moment", at=240_800),
        L(CLARA, "선생님 서랍은 약을 두는 자리예요.",
          "That drawer of his is where the medicine goes."),
        L(JULIAN, "…또 났어요.", "…There it goes again."),
        L(CLARA, "유리 소리예요.", "That was glass."),
        L(JULIAN, "들어가 볼까요.", "Shall I go in?"),
        L(CLARA, "가요. 여기 서 있는 걸 보이면 안 돼요.",
          "Come away. We shouldn't be seen standing here."),
        L(JULIAN, "무엇을 보이면 안 되는 겁니까.",
          "What is it we shouldn't be seen doing?"),
        L(CLARA, "…올라가요.", "…Upstairs."),
    ]),

    Scene("c_study_swap", STUDY, 232_000, 300_000, [HELEN],
          note="바꿔치기. 헬렌은 혼자이므로 말하지 않는다 — 소리만 남는다", lines=[]),

    # ─── 4막 : 숨긴다 · 소문 ──────────────────────────────────────
    Scene("d_guest", GUEST, 290_000, 378_000, [CLARA, JULIAN],
          note="소문. 헬렌의 과거가 여기서 나온다", lines=[
        L(JULIAN, "모로 선생을 전에 본 적이 있어요. 신문에서.",
          "I have seen Doctor Moreau before. In a newspaper."),
        L(CLARA, "무슨 신문이요.", "What newspaper?"),
        L(JULIAN, "이 년 전 지방 신문입니다. 도서관에서 봤어요.",
          "A provincial paper, two years ago. I saw it in a library."),
        L(CLARA, "왜 그런 걸 찾아봤어요.",
          "Why were you looking for such a thing?"),
        L(JULIAN, "빚을 지면 사람을 알아봐 두게 됩니다.",
          "When you owe money you learn to look people up."),
        L(JULIAN, "그 신문에 실린 얼굴이 모로 선생이었어요. 환자 둘이 죽고, 기록이 고쳐졌다고 적혀 있었죠. 그 병원 이름이 갈색 약병에도 찍혀 있었고.",
          "The face in that paper was Doctor Moreau. Two patients dead, and the record altered, it said. The name of that hospital was stamped on a brown bottle too.",
          fact="fact_helen_past", pause=800),
        L(CLARA, "…성 앨런.", "…St Allen."),
        L(JULIAN, "이름을 아시는군요.",
          "So you know the name."),
        L(CLARA, "약값 청구서에 두 번 적혀 있었어요.",
          "It was written twice on the medicine accounts."),
        L(JULIAN, "그 얘기를 삼촌한테 했어요?",
          "Did you tell my uncle that?"),
        L(CLARA, "아뇨.", "No."),
        L(JULIAN, "삼촌이 먼저 알고 있었던 것 같아요.",
          "I think he already knew."),
        L(CLARA, "…….", "…", pause=1000),
        L(JULIAN, "왜 그런 얼굴을 하세요.",
          "Why do you look like that?"),
        L(CLARA, "내가 그 청구서를 그냥 넘겼어요. 두 번이나.",
          "I passed those accounts without a word. Twice."),
        L(JULIAN, "비서가 할 일을 한 겁니다.",
          "You did what a secretary does."),
        L(CLARA, "오늘 저녁은 아무 얘기도 하지 말아요. 부탁이에요.",
          "Say nothing at dinner tonight. I'm asking you."),
        L(JULIAN, "그건 약속 못 합니다.",
          "That I cannot promise."),
    ]),

    Scene("d_storage_hide", STORAGE, 316_000, 382_000, [HELEN],
          note="석탄통 밑에 병을 밀어 넣고 재를 덮는다 — 소리만", lines=[]),

    # ─── 5막 : 발견 ───────────────────────────────────────────────
    Scene("e_dining", DINING, 396_000, 478_000, [VICTIM, HELEN, MARCO],
          note="헬렌이 태연하게 돌아온다", lines=[
        L(HELEN, "늦었습니다. 지하가 춥네요.",
          "I am late. It is cold downstairs."),
        L(VICTIM, "지하에 갔었소?",
          "You were downstairs?"),
        L(HELEN, "물을 찾았어요.", "I was looking for water."),
        L(MARCO, "물은 부엌에 있습니다.",
          "The water is in the kitchen."),
        L(HELEN, "…그렇군요.", "…Of course it is."),
        L(VICTIM, "선생, 손을 보여 주시오.",
          "Doctor. Show me your hands."),
        L(HELEN, "…왜요.", "…Why."),
        L(VICTIM, "보여 주시오.", "Show me."),
        L(HELEN, "씻었습니다.", "I washed them."),
        L(VICTIM, "그렇겠지.", "I am sure you did."),
        L(MARCO, "수프를 데워 오겠습니다.",
          "I'll warm the soup."),
        L(VICTIM, "앉아 있게, 마르코.",
          "Stay where you are, Marco."),
        L(VICTIM, "종이 울리면 약을 먹겠소. 늘 하던 대로.",
          "When the bell rings I will take my medicine. As always."),
        L(HELEN, "네. 늘 하던 대로요.",
          "Yes. As always."),
        L(VICTIM, "선생은 내가 그 약을 먹는 걸 서른 번쯤 봤겠지.",
          "You must have watched me take it thirty times."),
        L(HELEN, "더 됩니다.", "More than that."),
    ]),

    Scene("e_storage_find", STORAGE, 398_000, 476_000, [CLARA, JULIAN],
          note="와인을 가지러 내려가 병과 상자를 찾는다", lines=[
        L(CLARA, "와인은 저 뒤쪽 선반이에요.",
          "The wine is on the shelf at the back."),
        L(JULIAN, "여기 재가 흩어져 있는데요. 아침에 넣었다면서.",
          "There is ash scattered here. He said he filled it in the morning."),
        L(CLARA, "손대지 말아요.", "Don't touch it."),
        L(JULIAN, "발자국도 있어요. 작아요.",
          "There are footprints too. Small ones."),
        L(CLARA, "…….", "…", pause=900),
        L(JULIAN, "잠깐. 석탄통 밑에 갈색 유리 약병이 처박혀 있어요. 재를 덮어 놨는데 라벨이 안 타고 남았네. 성 앨런 요양원이라고 찍혀 있어요.",
          "Wait. There is a brown glass bottle shoved under the coal bin. Ash heaped over it, but the label did not burn. St Allen Sanatorium, it says.",
          fact="fact_bottle_in_coal", pause=800),
        L(CLARA, "…그 병은 선생님 서랍에 있어야 하는 병이에요.",
          "…That bottle should be in his drawer."),
        L(JULIAN, "안에 알약이 그대로 있어요. 스물네 알쯤.",
          "The tablets are still inside. Some two dozen."),
        L(CLARA, "그럼 서랍에 있는 건 뭐예요.",
          "Then what is in the drawer?"),
        L(JULIAN, "그리고 이건 뭡니까. 쓰레기통에 약국 상자가.",
          "And what is this? A chemist's box in the bin."),
        L(CLARA, "읽어 봐요.", "Read it."),
        L(JULIAN, "설탕 알약 열두 알. 혀 밑에서 녹기는 녹는다. 다만 심장에는 아무 일도 하지 않는다 — 그렇게 적혀 있어요.",
          "Twelve sugar pills. They do dissolve under the tongue. They simply do nothing to the heart — that is what it says.",
          fact="fact_sugar_pills", pause=600),
        L(CLARA, "…열두 알.", "…Twelve."),
        L(JULIAN, "삼촌은 종이 울리면 약을 먹습니다.",
          "My uncle takes his medicine at the bell."),
        L(CLARA, "위로 올라가요. 지금.",
          "Upstairs. Now."),
        L(JULIAN, "병은 가져갑니다.",
          "I am taking the bottle."),
    ]),

    # ─── 6막 : 종 ─────────────────────────────────────────────────
    Scene("f_dining", DINING, 494_000, 596_000, [VICTIM, HELEN, MARCO, CLARA, JULIAN],
          note="다섯이 한 방에. 아무도 제때 말하지 못한다", lines=[
        L(CLARA, "선생님.", "Sir."),
        L(VICTIM, "앉게. 종이 곧 울리네.",
          "Sit. The bell is nearly on us."),
        L(JULIAN, "삼촌, 지금 보여 드릴 게 있습니다.",
          "Uncle, there is something I must show you now."),
        L(HELEN, "식사 중입니다, 헤일 씨.",
          "We are at table, Mr Hale."),
        L(JULIAN, "모로 선생은 빠지시죠.",
          "Doctor Moreau can stay out of this."),
        L(VICTIM, "줄리안. 자네 빚 얘기는 내일 듣겠네.",
          "Julian. I will hear about your debt tomorrow."),
        L(JULIAN, "빚 얘기가 아닙니다.",
          "It is not about the debt."),
        L(CLARA, "선생님, 약을 지금 보셔야 해요.",
          "Sir, you need to look at your medicine now."),
        L(HELEN, "약은 제가 오늘 아침에 확인했습니다.",
          "I checked the medicine myself this morning."),
        L(MARCO, "…선생님, 얼굴색이.",
          "…Sir, your colour."),
        L(VICTIM, "괜찮아.", "It is nothing."),
        L(VICTIM, "…아니군. 약을 가져오게. 서랍에 있어.",
          "…No. Fetch my medicine. It is in the drawer.", pause=900),
        L(HELEN, "제가 가져오겠습니다.",
          "I will bring it."),
        L(CLARA, "아니요.", "No."),
        L(HELEN, "제가 주치의입니다.",
          "I am his physician."),
        L(JULIAN, "여기 있습니다, 삼촌.",
          "It is here, uncle."),
        L(VICTIM, "…그건 어디서 났나.",
          "…Where did you get that."),
        L(JULIAN, "창고 석탄통 밑에서요.",
          "From under the coal bin in the cellar."),
        L(HELEN, "…….", "…", pause=1200),
        L(VICTIM, "선생.", "Doctor."),
        L(HELEN, "말씀하세요.", "Yes."),
        L(VICTIM, "서명은 필요 없게 됐소.",
          "I will not be needing that signature."),
        L(MARCO, "종이 울립니다.", "The bell."),
    ]),
]


# ═══════════════════════════════════════════════════════════════════
#  사실 · 결론 (case_02.json의 정답과 맞물린다 — id를 바꾸지 않는다)
# ═══════════════════════════════════════════════════════════════════

FACTS = {
    "fact_voice1_is_clara": ("목소리 v1은 저택의 개인 비서 클라라 보스다",
                             "Voice v1 is Clara Boss, the private secretary of the house"),
    "fact_voice2_is_marco": ("목소리 v2는 요리사 마르코 벨리니다",
                             "Voice v2 is Marco Bellini, the cook"),
    "fact_voice3_is_julian": ("목소리 v3은 피해자의 조카 줄리안 헤일이다",
                              "Voice v3 is Julian Hale, the victim's nephew"),
    "fact_voice4_is_helen": ("목소리 v4는 주치의 헬렌 모로다",
                             "Voice v4 is Helen Moreau, the attending physician"),
    "fact_voice5_is_edmund": ("목소리 v5는 저택의 주인 에드먼드 헤일이다",
                              "Voice v5 is Edmund Hale, the master of the house"),
    "fact_edmund_heart_pills": ("에드먼드는 서재 서랍의 갈색 병에 든 니트로 알약 없이는 발작을 못 넘긴다",
                                "Without the nitroglycerin tablets in the brown bottle in his study drawer, "
                                "Edmund cannot survive an attack"),
    "fact_blackmail": ("에드먼드는 헬렌의 1907년 진료 기록 조작을 알아내 그것으로 그를 쥐고 있었다",
                       "Edmund had found Helen's falsified 1907 record and was holding it over her"),
    "fact_helen_past": ("헬렌은 예전 병원에서 환자를 죽게 하고 기록을 고친 사람이다",
                        "Helen let a patient die at a former hospital and altered the record"),
    "fact_clock_814": ("벽시계로 여덟 시 십사 분인 순간이 회차의 이 지점이다",
                       "This point in the run is fourteen minutes past eight by the wall clock"),
    "fact_swap_moment": ("그 순간 서재에서 서랍이 열리고 있었고, 그 방에는 헬렌 혼자였다",
                         "At that moment a drawer was being opened in the study, "
                         "and Helen was alone in that room"),
    "fact_sugar_pills": ("바꿔 넣은 알약은 심장에 아무 일도 하지 않는 설탕 알약이다",
                         "The substituted pills are sugar and do nothing for the heart"),
    "fact_drawer_place": ("에드먼드의 약이 놓이는 곳은 서재 책상 오른쪽 서랍이다",
                          "Edmund's medicine is kept in the right-hand drawer of the study desk"),
    "fact_bottle_in_coal": ("창고 석탄통에 성 앨런 요양원 라벨이 붙은 갈색 약병이 숨겨져 있다",
                            "A brown bottle labelled St Allen Sanatorium is hidden in the cellar coal bin"),
    "fact_helen_left_storage": ("헬렌이 창고에서 나오는 것을 본 사람이 있다",
                                "Someone saw Helen coming out of the cellar"),
}

CONCLUSIONS = [
    ("concl_voices_named",
     "다섯 목소리의 이름: v1 클라라 보스, v2 마르코 벨리니, v3 줄리안 헤일, v4 헬렌 모로, v5 에드먼드 헤일",
     "The five voices: v1 Clara Boss, v2 Marco Bellini, v3 Julian Hale, v4 Helen Moreau, v5 Edmund Hale",
     ["fact_voice1_is_clara", "fact_voice2_is_marco", "fact_voice3_is_julian",
      "fact_voice4_is_helen", "fact_voice5_is_edmund"]),
    ("concl_culprit", "범인은 목소리 v4 — 주치의 헬렌 모로다",
     "The killer is voice v4 — Helen Moreau, the attending physician",
     ["fact_voice4_is_helen", "fact_swap_moment", "fact_drawer_place"]),
    ("concl_motive", "동기는 협박 — 에드먼드가 헬렌의 옛 기록 조작을 쥐고 서명을 요구했다",
     "The motive was blackmail — Edmund held her altered record and demanded a signature",
     ["fact_voice4_is_helen", "fact_voice5_is_edmund", "fact_blackmail", "fact_helen_past"]),
    ("concl_time", "범행 시각은 벽시계 여덟 시 십사 분이다",
     "The time of the act was fourteen minutes past eight by the wall clock",
     ["fact_clock_814", "fact_swap_moment"]),
    ("concl_place", "범행 장소는 서재 — 책상 오른쪽 서랍이다",
     "The place was the study — the right-hand drawer of the desk",
     ["fact_drawer_place", "fact_swap_moment"]),
    ("concl_method", "수법은 약 바꿔치기 — 니트로 알약을 설탕 알약으로 갈아 두어 발작을 막지 못하게 했다",
     "The method was substitution — sugar pills in place of nitroglycerin, so the attack could not be stopped",
     ["fact_edmund_heart_pills", "fact_sugar_pills", "fact_swap_moment"]),
    ("concl_evidence", "결정적 증거는 창고 석탄통에 숨겨진 성 앨런 요양원 라벨의 갈색 약병이다",
     "The decisive evidence is the brown bottle labelled St Allen Sanatorium, hidden in the cellar coal bin",
     ["fact_bottle_in_coal", "fact_helen_left_storage", "fact_voice2_is_marco", "fact_voice1_is_clara"]),
]

# 손으로 적는 극적인 소리. 문소리·발소리는 MovementEvents가 트랙에서 뽑으므로 적지 않는다.
EVENTS = [
    # 서재 — 바꿔치기. 복도에 귀를 두면 이것이 들린다.
    dict(id="ev_drawer_open", startMs=SWAP_MS - 2_000, durationMs=1_100, room=STUDY,
         npcId=HELEN, kind="object", ko="책상 서랍이 열린다", en="a desk drawer slides open"),
    dict(id="ev_pills_rattle", startMs=SWAP_MS, durationMs=1_600, room=STUDY,
         npcId=HELEN, kind="object", ko="유리병 안에서 알약이 굴러간다", en="tablets roll inside a glass bottle"),
    dict(id="ev_drawer_close", startMs=SWAP_MS + 4_200, durationMs=1_100, room=STUDY,
         npcId=HELEN, kind="object", ko="서랍이 조심스럽게 닫힌다", en="the drawer is closed carefully"),
    # 창고 — 병을 숨긴다.
    dict(id="ev_coal_shift", startMs=336_000, durationMs=2_400, room=STORAGE,
         npcId=HELEN, kind="object", ko="석탄이 무너져 내린다", en="coal shifts and slides"),
    dict(id="ev_ash_scatter", startMs=346_000, durationMs=1_800, room=STORAGE,
         npcId=HELEN, kind="object", ko="재를 손으로 덮는 소리", en="ash being spread by hand"),
    # 서재 — 에드먼드가 장부를 넘긴다.
    dict(id="ev_ledger_1", startMs=104_000, durationMs=1_500, room=STUDY,
         npcId=VICTIM, kind="object", ko="장부를 넘기는 소리", en="ledger pages turning"),
    dict(id="ev_ledger_2", startMs=142_000, durationMs=1_500, room=STUDY,
         npcId=VICTIM, kind="object", ko="장부를 덮는 소리", en="the ledger closing"),
    # 손님 방 — 줄리안이 편지를 접는다.
    dict(id="ev_letter", startMs=30_000, durationMs=1_400, room=GUEST,
         npcId=JULIAN, kind="object", ko="종이를 접는 소리", en="paper being folded"),
    dict(id="ev_chair", startMs=52_000, durationMs=1_200, room=GUEST,
         npcId=JULIAN, kind="object", ko="의자가 밀린다", en="a chair pushed back"),
    # 저택 — 괘종시계. 복도에서 시각을 알 수 있게 한다.
    dict(id="ev_clock_quarter", startMs=300_000, durationMs=3_000, room=HALL,
         npcId="", kind="house", ko="괘종시계가 한 번 울린다", en="the long-case clock strikes once"),
    dict(id="ev_clock_half", startMs=592_000, durationMs=4_000, room=HALL,
         npcId="", kind="house", ko="저녁 종이 울린다", en="the dinner bell rings"),
    # 창고 — 병이 깨지지는 않지만 유리가 부딪힌다(큰 소리라 벽을 넘는다).
    dict(id="ev_glass_knock", startMs=452_000, durationMs=1_500, room=STORAGE,
         npcId=JULIAN, kind="break", ko="유리병이 석탄에 부딪힌다", en="a glass bottle knocks against coal"),
]


# ═══════════════════════════════════════════════════════════════════
#  파생
# ═══════════════════════════════════════════════════════════════════

def layout_lines():
    """장면 안에 대사를 깐다. 창을 넘치면 오류로 멈춘다 — 조용히 잘리면 안 된다."""
    utterances = []
    en_map = {}
    fact_utterances = {}
    problems = []

    for scene in SCENES:
        t = scene.start
        for index, line in enumerate(scene.lines):
            t += line.pause
            if line.at is not None:
                if line.at < t:
                    problems.append(
                        f"{scene.sid}[{index}]: 고정 시각 {line.at}ms에 닿지 못했다 "
                        f"(앞 대사가 {t}ms까지 밀렸다) — 앞 대사를 줄여라")
                    break
                t = line.at
            uid = f"{scene.sid}_{index:02d}"
            duration = line.duration
            if t + duration > scene.end:
                problems.append(
                    f"{scene.sid}: 대사가 장면 창을 넘친다 ({t + duration}ms > {scene.end}ms) — "
                    f"창을 늘리거나 대사를 줄여라")
                break
            utterances.append(dict(id=uid, startMs=t, durationMs=duration,
                                   voiceId=VOICE[line.npc], room=scene.room,
                                   text=line.ko, clip=""))
            en_map[uid] = line.en
            if line.fact:
                fact_utterances.setdefault(line.fact, []).append(uid)
            t += duration + MS_GAP

    utterances.sort(key=lambda u: (u["startMs"], u["id"]))
    return utterances, en_map, fact_utterances, problems


def derive_tracks():
    """장면 소속이 곧 동선이다. 사람마다 자기가 있던 방을 시간 순으로 모은다."""
    per_npc = {}
    for scene in SCENES:
        for npc in scene.who:
            per_npc.setdefault(npc, []).append((scene.start, scene.end, scene.room))

    tracks = []
    problems = []
    for npc in sorted(per_npc):
        spans = sorted(per_npc[npc])
        merged = []
        for start, end, room in spans:
            if merged and merged[-1]["room"] == room and start - merged[-1]["endMs"] < 2_000:
                merged[-1]["endMs"] = max(merged[-1]["endMs"], end)   # 같은 방 연속은 붙인다
                continue
            if merged and start < merged[-1]["endMs"]:
                problems.append(f"{npc}: 두 장면이 겹친다 ({merged[-1]['room']} / {room} at {start}ms) — "
                                f"한 사람이 두 방에 있을 수 없다")
            merged.append(dict(startMs=start, endMs=end, room=room))
        tracks.append(dict(npcId=npc, segments=merged))
    return tracks, problems


def build():
    utterances, en_map, fact_utterances, line_problems = layout_lines()
    tracks, track_problems = derive_tracks()
    problems = line_problems + track_problems

    missing = [fid for fid in FACTS if fid not in fact_utterances]
    if missing:
        problems.append("사실을 나르는 대사가 없다: " + ", ".join(sorted(missing)))

    script = dict(
        caseId="case_02",
        durationMs=RUN_MS,
        speakers=[dict(voiceId=VOICE[n], npcId=n)
                  for n in (CLARA, MARCO, JULIAN, HELEN, VICTIM)],
        utterances=utterances,
        facts=[dict(id=fid, description=FACTS[fid][0], utteranceIds=fact_utterances.get(fid, []))
               for fid in FACTS],
        conclusions=[dict(id=cid, text=ko, requiresFacts=req) for cid, ko, _en, req in CONCLUSIONS],
    )

    events = dict(events=[dict(id=e["id"], startMs=e["startMs"], durationMs=e["durationMs"],
                               room=e["room"], npcId=e["npcId"], kind=e["kind"],
                               text=e["ko"], muffledText="", sound="",
                               loud=(e["kind"] == "break"))
                          for e in EVENTS])

    english = dict(locale="en", entries=(
        [dict(id=uid, text=text) for uid, text in sorted(en_map.items())]
        + [dict(id=fid, text=FACTS[fid][1]) for fid in FACTS]
        + [dict(id=cid, text=en) for cid, _ko, en, _req in CONCLUSIONS]
        + [dict(id=e["id"], text=e["en"]) for e in EVENTS]
    ))

    return script, dict(tracks=tracks), events, english, problems


def report(script, tracks, events):
    """설계가 의도대로 됐는지 사람이 읽을 수 있게 요약한다."""
    utts = script["utterances"]
    by_room = {}
    for u in utts:
        by_room[u["room"]] = by_room.get(u["room"], 0) + 1

    # 대화 비율: 같은 방 인접 발화의 화자가 바뀌는가
    exchange = 0
    i = 0
    while i < len(utts):
        j = i
        while (j + 1 < len(utts) and utts[j + 1]["room"] == utts[i]["room"]
               and utts[j + 1]["startMs"] - utts[j]["startMs"] < 12_000):
            j += 1
        block = utts[i:j + 1]
        if len({u["voiceId"] for u in block}) >= 2:
            exchange += len(block)
        i = j + 1

    transits = sum(max(0, len(t["segments"]) - 1) for t in tracks["tracks"])
    facts_by_room = {}
    for fact in script["facts"]:
        for uid in fact["utteranceIds"]:
            room = next(u["room"] for u in utts if u["id"] == uid)
            facts_by_room.setdefault(room, []).append(fact["id"])

    print(f"발화 {len(utts)}개 · 이벤트 {len(events['events'])}개 · 이동 {transits}회 "
          f"(사람당 {transits / 5:.1f})")
    print(f"주고받는 대화에 속한 발화: {exchange}/{len(utts)} ({exchange * 100 // len(utts)}%)")
    print("방별 발화:", {r.replace('room_', ''): n for r, n in sorted(by_room.items(), key=lambda kv: -kv[1])})
    print("방별 사실:")
    for room in sorted(facts_by_room):
        print(f"  {room.replace('room_', ''):9s} {len(facts_by_room[room])}개  "
              f"{', '.join(f.replace('fact_', '') for f in facts_by_room[room])}")


def main(argv=None):
    ap = argparse.ArgumentParser(description="case_02 시나리오 생성")
    ap.add_argument("--check", action="store_true", help="쓰지 않고 검사만 한다")
    args = ap.parse_args(argv)

    script, tracks, events, english, problems = build()

    if problems:
        print("설계 문제:", file=sys.stderr)
        for p in problems:
            print("  " + p, file=sys.stderr)
        return 1

    report(script, tracks, events)

    if args.check:
        print("\n--check: 쓰지 않았다.")
        return 0

    CASE.mkdir(parents=True, exist_ok=True)
    LOCALE.mkdir(parents=True, exist_ok=True)
    for path, obj in ((CASE / "script.json", script),
                      (CASE / "tracks.json", tracks),
                      (CASE / "events.json", events),
                      (LOCALE / "case_02.script.en.json", english)):
        path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"  wrote {path.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
