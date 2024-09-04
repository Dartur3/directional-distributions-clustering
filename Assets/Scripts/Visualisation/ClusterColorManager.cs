using UnityEngine;
using System.Collections.Generic;

namespace ModelViewer.Visualization
{
    public class ClusterColorManager : MonoBehaviour
    {
        private Dictionary<int, Color> clusterColors;
        private float colorSeed = 0;

        private void Awake()
        {
            clusterColors = new Dictionary<int, Color>();
        }

        public Color GetColorForCluster(int clusterId)
        {
            if (!clusterColors.TryGetValue(clusterId, out Color color))
            {
                color = GenerateDistinctColor();
                clusterColors[clusterId] = color;
            }
            return color;
        }

        public void ResetColors()
        {
            clusterColors.Clear();
            colorSeed = 0;
        }

        private Color GenerateDistinctColor()
        {
            float goldenRatio = 0.618033988749895f;
            colorSeed += goldenRatio;
            colorSeed %= 1;

            float h = colorSeed;
            float s = 0.5f + 0.3f * Mathf.PerlinNoise(colorSeed * 100, 0);
            float v = 0.7f + 0.3f * Mathf.PerlinNoise(0, colorSeed * 100);

            return Color.HSVToRGB(h, s, v);
        }

        public Color GetColorForUnassignedTriangles()
        {
            return Color.black;
        }
    }
}