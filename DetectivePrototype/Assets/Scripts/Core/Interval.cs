using System.Collections.Generic;

namespace Detective.Core
{
    /// <summary>1차원 구간 [Min, Max]. 벽에서 출입구를 빼낼 때 쓴다.</summary>
    public struct Interval
    {
        /// <summary>이 값보다 좁은 틈은 붙어 있는 것으로 본다.</summary>
        public const float MergeEpsilon = 0.001f;

        public readonly float Min;
        public readonly float Max;

        public Interval(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Length { get { return Max - Min; } }

        /// <summary>구간 목록에서 [cutMin, cutMax]를 잘라낸 결과를 돌려준다.</summary>
        public static List<Interval> Subtract(List<Interval> source, float cutMin, float cutMax)
        {
            var result = new List<Interval>();
            for (int i = 0; i < source.Count; i++)
            {
                Interval s = source[i];

                // 겹치지 않으면 그대로 유지
                if (cutMax <= s.Min || cutMin >= s.Max)
                {
                    result.Add(s);
                    continue;
                }

                if (cutMin > s.Min) result.Add(new Interval(s.Min, cutMin));
                if (cutMax < s.Max) result.Add(new Interval(cutMax, s.Max));
            }
            return result;
        }

        /// <summary>겹치거나 맞닿은 구간을 하나로 합친다(입력 순서와 무관).</summary>
        public static List<Interval> Merge(List<Interval> source)
        {
            var sorted = new List<Interval>(source);
            sorted.Sort((a, b) => a.Min.CompareTo(b.Min));

            var result = new List<Interval>();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (result.Count == 0)
                {
                    result.Add(sorted[i]);
                    continue;
                }

                Interval last = result[result.Count - 1];
                if (sorted[i].Min <= last.Max + MergeEpsilon)
                {
                    float max = sorted[i].Max > last.Max ? sorted[i].Max : last.Max;
                    result[result.Count - 1] = new Interval(last.Min, max);
                }
                else
                {
                    result.Add(sorted[i]);
                }
            }
            return result;
        }
    }
}
