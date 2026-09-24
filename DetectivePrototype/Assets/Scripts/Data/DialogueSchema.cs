using System;
using Detective.Core;

namespace Detective.Data
{
    /// <summary>
    /// 대사 한 줄. 평범한 대사 외에 네 가지 역할을 겸할 수 있다.
    ///   revealsClaims  : 이 줄을 들으면 인물의 claims(본인이 주장하는 행적) 전체가 타임라인에 증언으로 기록된다
    ///   requiresEvidence: 그 단서를 가진 상태에서 말을 걸어야 나온다(조건부 대사)
    ///   claimTick/Room : 이 줄 자체가 한 시각의 행적 주장이다(자백으로 말을 바꾸는 경우 등)
    ///   sightingTick/Target: 스케줄에서 자동 생성되는 목격 대사를 이 문구로 덮어쓴다(단독으로는 나오지 않는다)
    /// JsonUtility가 빠진 int를 0으로 채우므로 쓰지 않는 tick 필드는 -1을 적는다.
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        public string id;
        public string text;

        public bool revealsClaims;
        public string requiresEvidence;

        public int claimTick = -1;
        public string claimRoom;

        public int sightingTick = -1;
        public string sightingTarget;

        /// <summary>claimTick을 ms로(경계 함수). 없으면 GameTime.NoTime.</summary>
        public int ClaimMs { get { return GameTime.TickToMs(claimTick); } }

        /// <summary>sightingTick을 ms로(경계 함수). 없으면 GameTime.NoTime.</summary>
        public int SightingMs { get { return GameTime.TickToMs(sightingTick); } }

        public bool IsSightingOverride { get { return sightingTick >= 0 && !string.IsNullOrEmpty(sightingTarget); } }
        public bool IsClaim { get { return claimTick >= 0 && !string.IsNullOrEmpty(claimRoom); } }
    }

    /// <summary>dialogue/dialogue_*.json 하나 = 인물 한 명의 대사.</summary>
    [Serializable]
    public class DialogueFile
    {
        public string npcId;
        public DialogueLine[] lines;

        public DialogueFile Normalized()
        {
            if (lines == null) lines = new DialogueLine[0];
            return this;
        }
    }
}
