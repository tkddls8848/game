namespace Detective.Eavesdrop
{
    /// <summary>소리 하나를 실제로 어떻게 울릴지. 음량·정위·음색.</summary>
    public struct VoiceMixLevel
    {
        /// <summary>0~1. 스피커에 실을 음량.</summary>
        public float Gain;

        /// <summary>-1(왼쪽) ~ +1(오른쪽).</summary>
        public float Pan;

        /// <summary>저역 통과 차단 주파수(Hz). <see cref="VoiceMix.OpenLowPassHz"/>면 손대지 않는다.</summary>
        public int LowPassHz;

        /// <summary>실제 거리(월드 유닛). 화면 표기·디버그용.</summary>
        public float DistanceUnits;

        public bool IsSilent { get { return Gain <= 0.001f; } }
    }

    /// <summary>
    /// 거리감. 인물이 어디 서 있느냐에 따라 소리가 달라진다 — 멀면 작고, 옆이면 한쪽으로 쏠리고,
    /// 벽을 넘으면 고음이 깎인다.
    ///
    /// <b>여기서 지켜야 하는 선이 하나 있다.</b> 거리는 <i>음량·정위·음색</i>만 바꾸고
    /// <i>알아듣는지</i>는 바꾸지 않는다. 알아듣는 판정은 <see cref="AudibilityModel"/>이
    /// 방 기준으로 이미 했고, <see cref="SliceSolvability"/>가 "한 방에 고정되면 전부는 못 듣는다"를
    /// 그 방 기준으로 <b>증명</b>해 뒀다. 거리가 같은 방 대사를 못 알아듣게 만들면 그 증명이
    /// 조용히 무효가 된다 — 게임은 멀쩡히 돌고 풀 수 있다는 보장만 사라진다.
    ///
    /// 그래서 같은 방 소리는 <see cref="SameRoomFloor"/> 아래로 내려가지 않는다. 방 구석에 서 있어도
    /// 들리기는 한다. 다만 가까이 갈 이유는 생긴다 — 작게 들리는 것과 또렷하게 들리는 것은 다르다.
    ///
    /// 순수 C#이다(§18-1). Unity의 AudioSource도 Godot의 AudioStreamPlayer도 여기서 나온 숫자를
    /// 받아 쓰기만 한다.
    /// </summary>
    public static class VoiceMix
    {
        /// <summary>이 거리 안에서는 감쇠가 없다. 사람 사이 대화 거리.</summary>
        public const float FullHearingRadius = 2.0f;

        /// <summary>이 거리를 넘으면 들리지 않는다.</summary>
        public const float MaxAudibleUnits = 20f;

        /// <summary>
        /// 같은 방에서 음량이 여기 아래로 내려가지 않는다. 알아듣는다고 판정된 소리를
        /// 거리 때문에 못 듣게 만들지 않기 위한 바닥이다.
        /// </summary>
        public const float SameRoomFloor = 0.42f;

        /// <summary>벽 너머 소리의 최대 음량. 가까이 붙어도 이보다 크지 않다.</summary>
        public const float MuffledCeiling = 0.34f;

        /// <summary>손대지 않음을 뜻하는 차단 주파수.</summary>
        public const int OpenLowPassHz = 22000;

        /// <summary>벽 바로 옆에서의 차단 주파수. 멀어지면 더 깎인다.</summary>
        public const int MuffledNearHz = 900;

        /// <summary>벽 너머 먼 곳의 차단 주파수. 웅얼거림만 남는다.</summary>
        public const int MuffledFarHz = 380;

        /// <summary>이 거리에서 정위가 한쪽으로 완전히 쏠린다.</summary>
        public const float PanReferenceUnits = 5.5f;

        /// <summary>정위 최대치. 1.0까지 밀면 한쪽 귀가 비어 부자연스럽다.</summary>
        public const float MaxPan = 0.85f;

        /// <summary>
        /// 청취점과 소리가 난 지점으로 실제 울림을 구한다.
        /// <paramref name="level"/>은 <see cref="AudibilityModel.Judge"/>가 이미 낸 판정이다 —
        /// 여기서 다시 판정하지 않는다.
        /// </summary>
        public static VoiceMixLevel For(float listenerX, float listenerY,
                                        float sourceX, float sourceY,
                                        Audibility level)
        {
            float dx = sourceX - listenerX;
            float dy = sourceY - listenerY;
            float distance = Sqrt(dx * dx + dy * dy);

            var mix = new VoiceMixLevel
            {
                DistanceUnits = distance,
                Pan = 0f,
                LowPassHz = OpenLowPassHz,
                Gain = 0f
            };
            if (level == Audibility.None) return mix;

            float near = Nearness(distance);

            if (level == Audibility.Full)
            {
                // 같은 방: 바닥에서 1.0까지.
                mix.Gain = SameRoomFloor + (1f - SameRoomFloor) * near;
            }
            else
            {
                // 벽 너머: 천장에서 0까지. 고음이 거리에 따라 더 깎인다.
                mix.Gain = MuffledCeiling * near;
                mix.LowPassHz = (int)(MuffledFarHz + (MuffledNearHz - MuffledFarHz) * near);
            }

            mix.Pan = Clamp(dx / PanReferenceUnits, -MaxPan, MaxPan);
            return mix;
        }

        /// <summary>
        /// 가까움(1 = 코앞, 0 = 들리지 않는 거리). 제곱해서 떨어뜨린다 —
        /// 선형으로 떨어뜨리면 멀리서도 또박또박 들려 거리감이 나지 않는다.
        /// </summary>
        public static float Nearness(float distance)
        {
            if (distance <= FullHearingRadius) return 1f;
            if (distance >= MaxAudibleUnits) return 0f;
            float t = (distance - FullHearingRadius) / (MaxAudibleUnits - FullHearingRadius);
            float linear = 1f - t;
            return linear * linear;
        }

        /// <summary>
        /// 음량을 데시벨로. Godot은 dB를 받고 Unity는 선형을 받는다.
        /// 0에 가까운 값은 -inf가 되므로 잘라 준다.
        /// </summary>
        public static float ToDecibels(float gain)
        {
            if (gain <= 0.0008f) return -80f;
            // 20 * log10(gain)
            return 20f * Log10(gain);
        }

        // ── 수학 (System.Math에 의존하지 않고 float로 둔다) ──

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static float Sqrt(float value)
        {
            return value <= 0f ? 0f : (float)System.Math.Sqrt(value);
        }

        private static float Log10(float value)
        {
            return (float)System.Math.Log10(value);
        }
    }
}
