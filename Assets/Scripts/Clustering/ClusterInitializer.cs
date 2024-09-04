using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;

namespace ModelViewer.Clustering
{
    public static class ClusterInitializer
    {
        public static List<Vector3> InitializeCentroidsKMeansPlusPlus(List<Vector3> dataPoints, int numberOfClusters)
        {
            var random = new System.Random();
            var centroids = new List<Vector3> { dataPoints[random.Next(dataPoints.Count)] };

            for (int k = 1; k < numberOfClusters; k++)
            {
                var distances = ParallelComputation.ParallelSelect(dataPoints, 
                    point => centroids.Min(centroid => MathUtilities.CosineDistance(point, centroid)));
                
                var sum = distances.Sum();
                var target = random.NextDouble() * sum;

                for (int i = 0; i < distances.Count; i++)
                {
                    target -= distances[i];
                    if (target <= 0)
                    {
                        centroids.Add(dataPoints[i]);
                        break;
                    }
                }
            }

            return centroids;
        }

        // Możemy dodać tutaj inne metody inicjalizacji w przyszłości
    }
}