using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;

namespace ModelViewer.Clustering
{
    public abstract class DirectionalDistributionClustering : BaseSphericalClustering
    {
        protected HashSet<Vector3> remainingPointsSet;
        protected float[] precalculatedDensities;

        protected abstract float CalculateDistributionDensity(Vector3 point);
        protected abstract List<Vector3> FindDensityMaxima();
        protected abstract void EstimateDistributionParameters();
        protected abstract string GetAlgorithmName();
        protected override abstract void CalculateParameterVariability(); 

        public override void InitializeClustering(List<Vector3> points)
        {
            LogHandler.LogClusteringStart(GetAlgorithmName(), points.Count);
            dataPoints = normalizeVectors ? points.Select(p => p.normalized).ToList() : new List<Vector3>(points);
            clusterAssignments = new List<int>(new int[dataPoints.Count]);
            remainingPointsSet = new HashSet<Vector3>(dataPoints);
            centroids = new List<Vector3>();
            AnalyzeVectorDistribution();
            EstimateDistributionParameters();
            PrecalculateDensities();
        }

        protected virtual void AnalyzeVectorDistribution()
        {
            CalculateDynamicParameters();
        }

        protected void PrecalculateDensities()
        {
            densityStopwatch.Start();
            precalculatedDensities = new float[dataPoints.Count];
            Parallel.For(0, dataPoints.Count, i =>
            {
                precalculatedDensities[i] = CalculateDistributionDensity(dataPoints[i]);
            });
            densityStopwatch.Stop();

            float min = precalculatedDensities.Min();
            float max = precalculatedDensities.Max();
            float average = precalculatedDensities.Average();
            
            LogHandler.Log($"Precalculated densities:\n- Min = {min:F8}\n- Max = {max:F8}\n- Average = {average:F8}");
            //  Note: Densities represent relative probability per unit area on the unit sphere surface.
            //  Values are normalized and may be clamped to the range [{MIN_DENSITY}, {MAX_DENSITY}].
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
            centroids = InitializeClustersByDensity();
            initStopwatch.Stop();

            clusterStopwatch.Start();
            UpdateClusters();
            clusterStopwatch.Stop();

            LogClusterDistribution();
            ProcessCoherentClusters();

            clusterParametersStopwatch.Start();
            CalculateClusterParameters();
            CalculateParameterVariability();
            clusterParametersStopwatch.Stop();

            totalStopwatch.Stop();

            LogClusteringTimes(GetAlgorithmName());
        }

        protected virtual List<Vector3> InitializeClustersByDensity()
        {
            List<Vector3> initialCentroids = new List<Vector3>();
            if (remainingPointsSet == null || remainingPointsSet.Count == 0)
            {
                LogHandler.Log("Warning: remainingPointsSet is null or empty in InitializeClustersByDensity");
                return initialCentroids;
            }

            for (int i = 0; i < numberOfClusters - 1 && remainingPointsSet.Count > 0; i++)
            {
                maximaStopwatch.Start();
                List<Vector3> maxima = FindDensityMaxima();
                maximaStopwatch.Stop();

                if (maxima.Count > 0)
                {
                    if (this is BinghamClustering)
                    {
                        foreach (var maximum in maxima)
                        {
                            if (initialCentroids.Count < numberOfClusters - 1)
                            {
                                initialCentroids.Add(maximum);
                                ExtractNeighborhood(maximum, initialCentroids.Count - 1);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        Vector3 newCentroid = maxima[0];
                        initialCentroids.Add(newCentroid);
                        ExtractNeighborhood(newCentroid, i);
                    }
                }
                else
                {
                    LogHandler.Log($"No maxima found for cluster {i}. Stopping initialization.");
                    break;
                }
            }

            FinalizeInitialization(initialCentroids);

            return initialCentroids;
        }

        protected virtual Vector3 SelectCentroidFromMaxima(List<Vector3> maxima)
        {
            return maxima[0];
        }

        protected virtual void ExtractNeighborhood(Vector3 centroid, int clusterIndex)
        {
            if (dataPoints == null || clusterAssignments == null)
            {
                LogHandler.Log("Error: dataPoints or clusterAssignments is null in ExtractNeighborhood");
                return;
            }

            int neighborhoodSize = Math.Max(1, remainingPointsSet.Count / (numberOfClusters - clusterIndex));
            var points = remainingPointsSet.ToList();
            var distances = new float[points.Count];

            ParallelComputation.ParallelFor(points, (point, i) =>
            {
                distances[i] = 1 - Vector3.Dot(point, centroid);
            });

            int k = neighborhoodSize;
            int left = 0;
            int right = points.Count - 1;

            while (left < right)
            {
                int pivotIndex = Partition(points, distances, left, right);
                if (pivotIndex == k)
                {
                    break;
                }
                else if (pivotIndex < k)
                {
                    left = pivotIndex + 1;
                }
                else
                {
                    right = pivotIndex - 1;
                }
            }

            for (int i = 0; i < k; i++)
            {
                int index = dataPoints.IndexOf(points[i]);
                if (index >= 0 && index < clusterAssignments.Count)
                {
                    clusterAssignments[index] = clusterIndex;
                    remainingPointsSet.Remove(points[i]);
                }
            }
        }

        private int Partition(List<Vector3> points, float[] distances, int left, int right)
        {
            float pivot = distances[right];
            int i = left - 1;

            for (int j = left; j < right; j++)
            {
                if (distances[j] <= pivot)
                {
                    i++;
                    Swap(points, distances, i, j);
                }
            }

            Swap(points, distances, i + 1, right);
            return i + 1;
        }

        private void Swap(List<Vector3> points, float[] distances, int i, int j)
        {
            var tempPoint = points[i];
            points[i] = points[j];
            points[j] = tempPoint;

            var tempDistance = distances[i];
            distances[i] = distances[j];
            distances[j] = tempDistance;
        }

        protected virtual void FinalizeInitialization(List<Vector3> initialCentroids)
        {
            foreach (var point in remainingPointsSet)
            {
                int index = dataPoints.IndexOf(point);
                if (index >= 0 && index < clusterAssignments.Count)
                {
                    clusterAssignments[index] = numberOfClusters - 1;
                }
            }

            var lastClusterPoints = dataPoints.Where((p, i) => clusterAssignments[i] == numberOfClusters - 1);
            if (lastClusterPoints.Any())
            {
                Vector3 lastCentroid = MathUtilities.CalculateDirectionalMean(lastClusterPoints.ToList());
                initialCentroids.Add(lastCentroid);
            }
            else
            {
                LogHandler.Log("\nNo normal vectors in the last cluster. Using random vector as centroid.\n");
                initialCentroids.Add(dataPoints[UnityEngine.Random.Range(0, dataPoints.Count)]);
            }
        }

        protected virtual void UpdateClusters()
        {
            bool converged = false;
            this.iteration = 0;
            float previousMeanDistance = float.MaxValue;

            while (!converged && this.iteration < maxIterations)
            {
                this.iteration++;

                var newAssignments = new List<int>(new int[dataPoints.Count]);
                var newCentroids = new Vector3[numberOfClusters];
                var clusterSizes = new int[numberOfClusters];

                ParallelComputation.ParallelFor(dataPoints, (point, i) =>
                {
                    int closestCluster = FindClosestCentroid(point);
                    newAssignments[i] = closestCluster;
                    lock (newCentroids)
                    {
                        newCentroids[closestCluster] += point;
                        clusterSizes[closestCluster]++;
                    }
                });

                for (int i = 0; i < numberOfClusters; i++)
                {
                    if (clusterSizes[i] > 0)
                    {
                        newCentroids[i] = newCentroids[i].normalized;
                    }
                    else
                    {
                        newCentroids[i] = dataPoints[UnityEngine.Random.Range(0, dataPoints.Count)];
                    }
                }

                float meanDistance = CalculateMeanCentroidDistance(centroids, newCentroids.ToList());

                if (Math.Abs(meanDistance - previousMeanDistance) < convergenceThreshold)
                {
                    converged = true;
                }

                previousMeanDistance = meanDistance;
                centroids = newCentroids.ToList();
                clusterAssignments = newAssignments.ToList();
            }
        }

        protected virtual int FindClosestCentroid(Vector3 point)
        {
            int closestIndex = 0;
            float maxSimilarity = float.MinValue;

            for (int i = 0; i < centroids.Count; i++)
            {
                float similarity = Vector3.Dot(point, centroids[i]);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        protected virtual float CalculateMeanCentroidDistance(List<Vector3> oldCentroids, List<Vector3> newCentroids)
        {
            float totalDistance = 0;
            for (int i = 0; i < oldCentroids.Count; i++)
            {
                totalDistance += 1 - Vector3.Dot(oldCentroids[i], newCentroids[i]);
            }
            return totalDistance / oldCentroids.Count;
        }

        public override int[] GetClusterAssignments()
        {
            return clusterAssignments.ToArray();
        }

        protected Matrix<double> CalculateClusterCovarianceMatrix(List<Vector3> clusterPoints)
        {
            Vector3 mean = MathUtilities.CalculateDirectionalMean(clusterPoints);
            var covMatrix = Matrix<double>.Build.Dense(3, 3);

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double covariance = clusterPoints.Average(v => (v[i] - mean[i]) * (v[j] - mean[j]));
                    covMatrix[i, j] = covariance;
                }
            }
            return covMatrix;
        }

        protected override abstract void CalculateClusterParameters();

        private List<int> GetMeshTriangles()
        {
            var mesh = GameObject.FindObjectOfType<MeshFilter>().sharedMesh;
            return mesh.triangles.ToList();
        }
    }
}