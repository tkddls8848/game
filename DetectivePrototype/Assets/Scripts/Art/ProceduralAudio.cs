using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// 코드로 합성하는 효과음. 외부 음원이 없을 때 종소리·종이·조사 소리를 대신한다.
    /// 모두 짧은 단발음이라 런타임에 한 번 만들어 두고 재사용한다.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        /// <summary>괘종시계 종. 기본음 + 비화성 배음(진짜 종처럼)이 3초쯤 사그라든다.</summary>
        public static AudioClip ClockChime()
        {
            const float duration = 3.2f;
            const float f0 = 392f; // G4
            float[] partials = { 1f, 2.0f, 2.76f, 4.07f, 5.4f };
            float[] weights = { 1f, 0.55f, 0.35f, 0.18f, 0.08f };
            float[] decays = { 1.1f, 1.6f, 2.2f, 3.0f, 4.0f };

            return Render("ClockChime", duration, t =>
            {
                float s = 0f;
                for (int i = 0; i < partials.Length; i++)
                {
                    s += Mathf.Sin(2f * Mathf.PI * f0 * partials[i] * t) * weights[i] * Mathf.Exp(-t * decays[i]);
                }
                float strike = Mathf.Exp(-t * 40f) * 0.6f; // 첫 타격의 짧은 어택
                return (s * 0.28f + strike * Noise(t)) * Mathf.Clamp01(t * 200f);
            });
        }

        /// <summary>종이 넘기는 소리. 짧은 잡음이 두 번 스친다.</summary>
        public static AudioClip PageTurn()
        {
            return Render("PageTurn", 0.35f, t =>
            {
                float env = Mathf.Exp(-Mathf.Abs(t - 0.06f) * 35f) + 0.7f * Mathf.Exp(-Mathf.Abs(t - 0.2f) * 30f);
                return Noise(t) * env * 0.35f;
            });
        }

        /// <summary>조사 확인음. 낮은 두 음이 짧게.</summary>
        public static AudioClip Inspect()
        {
            return Render("Inspect", 0.45f, t =>
            {
                float f = t < 0.18f ? 330f : 440f;
                float env = Mathf.Exp(-((t < 0.18f) ? t : t - 0.18f) * 14f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.22f;
            });
        }

        private static float Noise(float t)
        {
            // 시간 기반 결정적 잡음(재생마다 같은 소리).
            float v = Mathf.Sin(t * 12345.678f) * 43758.5453f;
            return (v - Mathf.Floor(v)) * 2f - 1f;
        }

        private static AudioClip Render(string name, float duration, System.Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f);

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
