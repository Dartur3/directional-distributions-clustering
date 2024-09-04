using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;
using System;
using System.Diagnostics;

namespace ModelViewer.Clustering
{
    public abstract class BaseSphericalClustering : MonoBehaviour
    {
        protected int numberOfClusters;
        protected int maxIterations;
        protected int iteration;
        protected int initialNumberOfClusters;
        protected float convergenceThreshold;
        protected bool normalizeVectors = true;

        protected List<Vector3> dataPoints;
        protected List<Vector3> centroids;
        protected List<int> clusterAssignments;
        protected HashSet<int> unassignedTriangles;

        protected Stopwatch totalStopwatch = new Stopwatch();
        protected Stopwatch initStopwatch = new Stopwatch();
        protected Stopwatch densityStopwatch = new Stopwatch();
        protected Stopwatch maximaStopwatch = new Stopwatch();
        protected Stopwatch clusterStopwatch = new Stopwatch();
        protected Stopwatch clusterParametersStopwatch = new Stopwatch();

        protected float coherentClusteringTime;
        protected float unassignedTrianglesTime;
        protected float totalSyncTime;

        protected CoherentClusteringProcessor coherentClusteringProcessor;

        public abstract void InitializeClustering(List<Vector3> points);
        public abstract void ExecuteClustering();

        protected virtual void CalculateDynamicParameters()
        {
            maxIterations = Mathf.Min(10000, Mathf.Max(100, 50 * Mathf.CeilToInt(Mathf.Log10(dataPoints.Count) * Mathf.Pow(numberOfClusters, 0.4f))));
            convergenceThreshold = 0.0001f * Mathf.Pow(numberOfClusters, 0.2f) / Mathf.Pow(dataPoints.Count, 0.4f);
            
            LogHandler.Log($"Calculated dynamic parameters:\n" +
                        $"- Max iterations: {maxIterations}\n" +
                        $"- Convergence threshold: {convergenceThreshold:F6}");
        }

        public virtual void SetNumberOfClusters(int k)
        {
            numberOfClusters = Mathf.Max(1, k);
            initialNumberOfClusters = numberOfClusters;
            LogHandler.Log($"Number of clusters set to {numberOfClusters}.\n");
            if (dataPoints != null && dataPoints.Count > 0)
            {
                CalculateDynamicParameters();
            }
        }

        protected virtual void ProcessCoherentClusters()
        {
            coherentClusteringProcessor = new CoherentClusteringProcessor(dataPoints, GetMeshTriangles(), clusterAssignments.ToArray());
            var result = coherentClusteringProcessor.CreateCoherentClusters();

            clusterAssignments = result.Item1.ToList();
            unassignedTriangles = result.Item2;
            coherentClusteringTime = coherentClusteringProcessor.CoherentClusteringTime;
            unassignedTrianglesTime = coherentClusteringProcessor.UnassignedTrianglesTime;
            totalSyncTime = coherentClusteringProcessor.TotalSyncTime;
            numberOfClusters = coherentClusteringProcessor.GetCoherentClusterCount();
        }

        protected virtual Vector3 CalculateClusterCentroid(int clusterId)
        {
            var clusterPoints = dataPoints.Where((_, index) => clusterAssignments[index] == clusterId).ToList();
            return MathUtilities.CalculateDirectionalMean(clusterPoints);
        }

        public virtual int GetNumberOfClusters()
        {
            return numberOfClusters;
        }

        public virtual int GetInitialNumberOfClusters()
        {
            return initialNumberOfClusters;
        }

        protected abstract void CalculateParameterVariability();

        protected virtual void LogClusterDistribution()
        {
            if (clusterAssignments == null || clusterAssignments.Count == 0)
            {
                LogHandler.Log("Cluster distribution could not be logged: No cluster assignments available.");
                return;
            }

            var clusterSizes = clusterAssignments
                .Where(c => c != -1)  // Ignore unassigned triangles
                .GroupBy(c => c)
                .Select(g => g.Count())
                .OrderBy(s => s)
                .ToList();

            if (clusterSizes.Count == 0)
            {
                LogHandler.Log("Cluster distribution could not be logged: All triangles are unassigned.");
                return;
            }

            int nonEmptyClusters = clusterSizes.Count;
            double averageSize = clusterSizes.Average();
            double medianSize = clusterSizes.Count % 2 == 0 
                ? (clusterSizes[clusterSizes.Count / 2 - 1] + clusterSizes[clusterSizes.Count / 2]) / 2.0 
                : clusterSizes[clusterSizes.Count / 2];
            int minSize = clusterSizes.Min();
            int maxSize = clusterSizes.Max();

            int unassignedCount = clusterAssignments.Count(c => c == -1);

            LogHandler.Log($"\nCluster Distribution:\n" +
            $"- Number of non-empty clusters: {nonEmptyClusters}\n" +
            $"- Average cluster size: {averageSize:F2} normal vectors\n" +
            $"- Median cluster size: {medianSize:F2} normal vectors\n" +
            $"- Min cluster size: {minSize} normal vectors\n" +
            $"- Max cluster size: {maxSize} normal vectors\n" +
            $"- Unassigned triangles: {unassignedCount}");

            LogHandler.Log($"\n{GetType().Name} completed in {iteration} iterations.");
        }

        protected virtual void LogClusteringTimes(string algorithmName)
        {
            double totalTime = totalStopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000;
            double initTime = initStopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000;
            double densityTime = densityStopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000;
            double maximaTime = maximaStopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000;
            double clusterTime = clusterStopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000;
            double clusterParametersTime = clusterParametersStopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000;

            LogHandler.LogClusteringComplete(
                algorithmName,
                totalTime,
                initTime - densityTime - maximaTime,
                densityTime,
                maximaTime,
                clusterTime,
                coherentClusteringTime,
                unassignedTrianglesTime,
                clusterParametersTime,
                totalSyncTime
            );
        }

        protected void CalculateParameterStatistics(IEnumerable<float> values, string parameterName)
        {
            if (!values.Any())
            {
                LogHandler.Log($"\n{parameterName} statistics: No data available");
                return;
            }

            float average = values.Average();
            float median = MathUtilities.GetMedian(values);
            float min = values.Min();
            float max = values.Max();

            LogHandler.Log($"\n{parameterName} statistics:");
            LogHandler.Log($"- Average: {average:F4}");
            LogHandler.Log($"- Median: {median:F4}");
            LogHandler.Log($"- Min: {min:F4}");
            LogHandler.Log($"- Max: {max:F4}");
        }

        public virtual int[] GetClusterAssignments()
        {
            return clusterAssignments.Select(c => c == -1 ? -1 : c).ToArray();
        }

        public virtual HashSet<int> GetUnassignedTriangles()
        {
            return unassignedTriangles ?? new HashSet<int>();
        }

        private List<int> GetMeshTriangles()
        {
            var mesh = GameObject.FindObjectOfType<MeshFilter>().sharedMesh;
            return mesh.triangles.ToList();
        }

        protected double CalculateCV(List<double> values)
        {
            if (values.Count == 0) return 0;

            double mean = values.Average();
            if (mean == 0) return 0;

            double variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
            double stdDev = Math.Sqrt(variance);
            return (stdDev / mean) * 100;
        }

        protected virtual float CalculateCosineSimilarity(Vector3 centroid, List<Vector3> clusterPoints)
        {
            return clusterPoints.Average(point => MathUtilities.CosineSimilarity(point, centroid));
        }

        protected virtual void CalculateClusterParameters()
        {
            clusterParametersStopwatch.Start();
            // Implementacja obliczania parametrów klastra
            clusterParametersStopwatch.Stop();
        }
    }
}