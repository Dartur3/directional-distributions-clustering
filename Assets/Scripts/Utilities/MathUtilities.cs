using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ModelViewer.Utilities
{
    public static class MathUtilities
    {
        public static Vector3 CalculateDirectionalMean(List<Vector3> vectors)
        {
            Vector3 sum = Vector3.zero;
            foreach (var vector in vectors)
            {
                sum += vector;
            }
            return sum.normalized;
        }

        public static float CosineSimilarity(Vector3 v1, Vector3 v2)
        {
            return Vector3.Dot(v1.normalized, v2.normalized);
        }

        public static float CosineDistance(Vector3 v1, Vector3 v2)
        {
            return 1f - CosineSimilarity(v1, v2);
        }

        public static float BesselRatio(float x)
        {
            if (x < 1e-6f)
            {
                return 1f / 3f + x * x / 15f;
            }
            return ModifiedBesselI1(x) / ModifiedBesselI0(x);
        }

        public static float ModifiedBesselI0(float x)
        {
            float ax = Mathf.Abs(x);
            if (ax < 3.75f)
            {
                float y = x / 3.75f;
                y *= y;
                return 1.0f + y * (3.5156229f + y * (3.0899424f + y * (1.2067492f
                    + y * (0.2659732f + y * (0.360768e-1f + y * 0.45813e-2f)))));
            }
            else
            {
                float y = 3.75f / ax;
                return (Mathf.Exp(ax) / Mathf.Sqrt(ax)) * (0.39894228f + y * (0.1328592e-1f
                    + y * (0.225319e-2f + y * (-0.157565e-2f + y * (0.916281e-2f
                    + y * (-0.2057706e-1f + y * (0.2635537e-1f + y * (-0.1647633e-1f
                    + y * 0.392377e-2f))))))));
            }
        }

        public static float ModifiedBesselI1(float x)
        {
            float ax = Mathf.Abs(x);
            if (ax < 3.75f)
            {
                float y = x / 3.75f;
                y *= y;
                return ax * (0.5f + y * (0.87890594f + y * (0.51498869f + y * (0.15084934f
                    + y * (0.2658733e-1f + y * (0.301532e-2f + y * 0.32411e-3f))))));
            }
            else
            {
                float y = 3.75f / ax;
                float ans = 0.2282967e-1f + y * (-0.2895312e-1f + y * (0.1787654e-1f
                    - y * 0.420059e-2f));
                ans = 0.39894228f + y * (-0.3988024e-1f + y * (-0.362018e-2f
                    + y * (0.163801e-2f + y * (-0.1031555e-1f + y * ans))));
                return ans * Mathf.Exp(ax) / Mathf.Sqrt(ax);
            }
        }

        public static float GetMedian<T>(IEnumerable<T> values) where T : IComparable<T>
        {
            var sortedValues = values.OrderBy(v => v).ToList();
            int count = sortedValues.Count;
            if (count == 0)
            {
                throw new InvalidOperationException("Cannot compute median for an empty set.");
            }
            if (count % 2 == 0)
            {
                var lower = sortedValues[(count / 2) - 1];
                var upper = sortedValues[count / 2];
                // If T is float or double, we can safely cast and average
                if (typeof(T) == typeof(float) || typeof(T) == typeof(double))
                {
                    return (float)((Convert.ToDouble(lower) + Convert.ToDouble(upper)) / 2.0);
                }
                // For other types, we'll return the lower value
                return Convert.ToSingle(lower);
            }
            else
            {
                return Convert.ToSingle(sortedValues[count / 2]);
            }
        }
    }
}