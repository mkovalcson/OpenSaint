using System.Runtime.CompilerServices;

namespace ServoAnimator
{
    /// <summary>Exact min/max queries over precomputed powers-of-two summaries.
    /// A zoomed-out pixel does not need to scan thousands of audio buckets.</summary>
    internal sealed class PeakEnvelope
    {
        private static readonly ConditionalWeakTable<float[], PeakEnvelope> Cache = new();
        private readonly List<(float[] Min, float[] Max)> _levels = new();
        public static PeakEnvelope For(float[] min, float[] max) =>
            Cache.GetValue(min, _ => new PeakEnvelope(min, max));

        private PeakEnvelope(float[] min, float[] max)
        {
            _levels.Add((min, max));
            while (min.Length > 1)
            {
                var nextMin = new float[(min.Length + 1) / 2];
                var nextMax = new float[nextMin.Length];
                for (int i = 0; i < nextMin.Length; i++)
                {
                    int a = 2 * i, b = Math.Min(a + 1, min.Length - 1);
                    nextMin[i] = Math.Min(min[a], min[b]);
                    nextMax[i] = Math.Max(max[a], max[b]);
                }
                _levels.Add((nextMin, nextMax));
                min = nextMin; max = nextMax;
            }
        }

        public (float Min, float Max) Query(int first, int last)
        {
            float min = float.MaxValue, max = float.MinValue;
            while (first <= last)
            {
                int level = 0, width = 1;
                while (level + 1 < _levels.Count && width <= (last - first + 1) / 2 &&
                       first % (width * 2) == 0) { level++; width *= 2; }
                min = Math.Min(min, _levels[level].Min[first / width]);
                max = Math.Max(max, _levels[level].Max[first / width]);
                first += width;
            }
            return (min, max);
        }
    }
}
