using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics;
using ModelViewer.Utilities;

namespace ModelViewer.Clustering
{
    public class SphericalKMeansClustering : BaseSphericalClustering
    {
        private const int STABLE_ITERATIONS_THRESHOLD = 2;
        private const float BASE_SAMPLING_RATE = 0.1f;
        private const float MAX_SAMPLING_RATE = 0.15f;
        private const int SAMPLING_RATE_THRESHOLD = 50;
        private const int MAX_SAMPLE_SIZE = 800;

        private List<float> clusterParameters;

        public override void InitializeClustering(List<Vector3> points)
        {
            LogHandler.LogClusteringStart("Spherical K-Means", points.Count);
            dataPoints = normalizeVectors ? points.Select(p => p.normalized).ToList() : new List<Vector3>(points);
            CalculateDynamicParameters();
        }

        public override void ExecuteClustering()
        {
            if (dataPoints == null || dataPoints.Count == 0)
            {
                LogHandler.Log("No data points available for clustering.");
                return;
            }

            totalStopwatch.Start();

            initStopwatch.Start();
            InitializeCentroids();
            initStopwatch.Stop();

            clusterStopwatch.Start();
            ClusterDataPoints();
            clusterStopwatch.Stop();

            LogClusterDistribution();

            ProcessCoherentClusters();

            clusterParametersStopwatch.Start();
            CalculateClusterParameters();
            CalculateParameterVariability();
            clusterParametersStopwatch.Stop();

            totalStopwatch.Stop();

            LogClusteringTimes("Spherical K-Means");
        }

        private void InitializeCentroids()
        {
            centroids = InitializeCentroidsKMeansPlusPlus(dataPoints, numberOfClusters);
            clusterAssignments = new List<int>(new int[dataPoints.Count]);
        }

        private List<Vector3> InitializeCentroidsKMeansPlusPlus(List<Vector3> points, int k)
        {
            var random = new System.Random();
            var centroids = new List<Vector3>();
            var availablePoints = new List<int>(Enumerable.Range(0, points.Count));

            // Wybierz pierwszy centroid losowo
            int firstIndex = random.Next(availablePoints.Count);
            centroids.Add(points[availablePoints[firstIndex]]);
            availablePoints.RemoveAt(firstIndex);

            float samplingRate = CalculateSamplingRate(k);

            for (int i = 1; i < k; i++)
            {
                var sampledPoints = SamplePoints(points, availablePoints, samplingRate);
                var distances = ParallelComputation.ParallelSelect(sampledPoints, 
                    point => centroids.Min(centroid => MathUtilities.CosineDistance(point, centroid)));
                
                var sum = distances.Sum();
                var target = random.NextDouble() * sum;

                for (int j = 0; j < distances.Count; j++)
                {
                    target -= distances[j];
                    if (target <= 0)
                    {
                        Vector3 newCentroid = sampledPoints[j];
                        centroids.Add(newCentroid);
                        int removedIndex = availablePoints.FindIndex(idx => points[idx] == newCentroid);
                        if (removedIndex != -1)
                        {
                            availablePoints.RemoveAt(removedIndex);
                        }
                        break;
                    }
                }
            }

            return centroids;
        }

        private float CalculateSamplingRate(int k)
        {
            if (k <= SAMPLING_RATE_THRESHOLD)
            {
                return BASE_SAMPLING_RATE;
            }
            else
            {
                float t = (k - SAMPLING_RATE_THRESHOLD) / (float)(numberOfClusters - SAMPLING_RATE_THRESHOLD);
                t = Mathf.Sqrt(t);
                return Mathf.Lerp(BASE_SAMPLING_RATE, MAX_SAMPLING_RATE, t);
            }
        }

        private List<Vector3> SamplePoints(List<Vector3> points, List<int> availableIndices, float samplingRate)
        {
            int sampleSize = Math.Min(MAX_SAMPLE_SIZE, Math.Max(numberOfClusters, (int)(points.Count * samplingRate)));
            sampleSize = Math.Min(sampleSize, availableIndices.Count);
            return availableIndices.OrderBy(x => Guid.NewGuid()).Take(sampleSize).Select(i => points[i]).ToList();
        }

        private void ClusterDataPoints()
        {
            bool converged = false;
            this.iteration = 0;
            int stableIterations = 0;
            float previousMeanDistance = float.MaxValue;

            while (!converged && this.iteration < maxIterations)
            {
                this.iteration++;
                var newAssignments = ParallelComputation.ParallelSelect(dataPoints, FindClosestCentroid);
                List<Vector3> newCentroids = ClusterOperations.CalculateNewCentroids(dataPoints, newAssignments, numberOfClusters);

                float meanDistance = CalculateMeanCentroidDistance(centroids, newCentroids);

                if (Math.Abs(meanDistance - previousMeanDistance) < convergenceThreshold)
                {
                    stableIterations++;
                    if (stableIterations >= STABLE_ITERATIONS_THRESHOLD)
                    {
                        converged = true;
                    }
                }
                else
                {
                    stableIterations = 0;
                }

                previousMeanDistance = meanDistance;
                centroids = newCentroids;
                clusterAssignments = newAssignments;
            }
        }

        private int FindClosestCentroid(Vector3 point)
        {
            int closestIndex = 0;
            float maxSimilarity = float.MinValue;

            for (int i = 0; i < numberOfClusters; i++)
            {
                float similarity = MathUtilities.CosineSimilarity(point, centroids[i]);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private float CalculateMeanCentroidDistance(List<Vector3> oldCentroids, List<Vector3> newCentroids)
        {
            return ParallelComputation.ParallelSelect(oldCentroids, (oldCentroid, i) => 
                MathUtilities.CosineDistance(oldCentroid, newCentroids[i])).Average();
        }

        protected override void CalculateClusterParameters()
        {
            clusterParameters = new List<float>();
            int actualClusterCount = clusterAssignments.Max() + 1;

            for (int i = 0; i < actualClusterCount; i++)
            {
                var clusterPoints = dataPoints.Where((_, index) => clusterAssignments[index] == i).ToList();
                if (clusterPoints.Count > 0)
                {
                    Vector3 centroid = CalculateClusterCentroid(clusterPoints);
                    float averageSimilarity = CalculateCosineSimilarity(centroid, clusterPoints);
                    clusterParameters.Add(averageSimilarity);
                }
                else
                {
                    clusterParameters.Add(0);
                }
            }

            CalculateParameterStatistics(clusterParameters.Where(p => p != 0), "Average Cosine Similarity");
        }

        private Vector3 CalculateClusterCentroid(List<Vector3> clusterPoints)
        {
            Vector3 sum = Vector3.zero;
            foreach (var point in clusterPoints)
            {
                sum += point;
            }
            return (sum / clusterPoints.Count).normalized;
        }

        protected override void CalculateParameterVariability()
        {
            var nonZeroParameters = clusterParameters.Where(p => p != 0).Select(p => (double)p).ToList();
            double cv = CalculateCV(nonZeroParameters);

            LogHandler.Log($"\nParameter Variability for Spherical K-Means:");
            LogHandler.Log($"Average Cosine Similarity CV: {cv:F2}%");
        }
    }
}