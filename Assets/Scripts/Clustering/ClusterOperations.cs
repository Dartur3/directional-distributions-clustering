using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;

namespace ModelViewer.Clustering
{
    public static class ClusterOperations
    {
        public static List<Vector3> CalculateNewCentroids(List<Vector3> dataPoints, List<int> clusterAssignments, int numberOfClusters)
        {
            var newCentroids = new Vector3[numberOfClusters];
            var clusterSizes = new int[numberOfClusters];

            ParallelComputation.ParallelFor(dataPoints, (point, i) =>
            {
                int clusterIndex = clusterAssignments[i];
                lock (newCentroids)
                {
                    newCentroids[clusterIndex] += point;
                    clusterSizes[clusterIndex]++;
                }
            });

            return ParallelComputation.ParallelSelect(newCentroids, (centroid, i) => 
                clusterSizes[i] > 0 ? MathUtilities.CalculateDirectionalMean(new List<Vector3> { centroid }) : Vector3.zero);
        }

        public static void HandleEmptyClusters(List<Vector3> dataPoints, List<int> clusterAssignments, List<Vector3> centroids)
        {
            var clusterSizes = clusterAssignments.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var emptyClusters = Enumerable.Range(0, centroids.Count).Except(clusterSizes.Keys).ToList();

            foreach (var emptyCluster in emptyClusters)
            {
                var largestCluster = clusterSizes.OrderByDescending(kv => kv.Value).First();
                int newCentroidIndex = clusterAssignments.IndexOf(largestCluster.Key);
                centroids[emptyCluster] = dataPoints[newCentroidIndex];
                clusterSizes[emptyCluster] = 1;
                clusterSizes[largestCluster.Key]--;
                clusterAssignments[newCentroidIndex] = emptyCluster;
            }
        }
    }
}