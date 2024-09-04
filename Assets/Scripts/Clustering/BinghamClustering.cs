using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;
using System;
using MathNet.Numerics.LinearAlgebra;
using System.Threading.Tasks;

namespace ModelViewer.Clustering
{
    public class BinghamClustering : DirectionalDistributionClustering
    {
        private const double MIN_DENSITY = 1e-10;
        private const double MAX_DENSITY = 1e6;
        private const double MAX_DISPERSION = 1000.0;
        private const double MIN_DISPERSION = 0.001;
        private const double EPSILON = 1e-10;
        private const double MIN_NORMALIZATION_CONSTANT = 1e-6;
        private const float LOCAL_MAXIMA_THRESHOLD = 0.95f;
        private const float DENSITY_COMPARISON_EPSILON = 1e-6f;
        private const float MIN_DENSITY_THRESHOLD = 0.1f;
        private const int MIN_POINTS_PER_CLUSTER = 10;
        private const float INITIAL_NEIGHBORHOOD_FACTOR = 0.1f;

        private int neighborhoodSize;
        private double[] dispersionParameters;
        private Vector3[] orientationMatrix;
        private double normalizationConstant;
        private double modelComplexity;
        private List<Vector3> clusterParameters;
        private List<double> clusterCosineSimilarities;
        private List<(Vector3 point, float density)> cachedLocalMaxima;
        private int totalLocalMaximaFound = 0;
        private Matrix<double> covarianceMatrix;
        private float densityThresholdFactor = 0.8f;

        protected override void AnalyzeVectorDistribution()
        {
            base.AnalyzeVectorDistribution();
            covarianceMatrix = CalculateCovarianceMatrix();
            CalculateOrientationAndConcentration();
            CalculateModelComplexity();
            LogClusteringStatistics();
        }

        private Matrix<double> CalculateCovarianceMatrix()
        {
            Vector3 mean = MathUtilities.CalculateDirectionalMean(dataPoints);
            var covarianceMatrix = Matrix<double>.Build.Dense(3, 3);

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double covariance = ParallelComputation.ParallelSelect(dataPoints,
                        v => (v[i] - mean[i]) * (v[j] - mean[j])).Average();
                    covarianceMatrix[i, j] = covariance;
                }
            }
            
            LogHandler.Log($"\nCovariance Matrix calculated:\n" +
                        $"  [{covarianceMatrix[0, 0]:F6} {covarianceMatrix[0, 1]:F6} {covarianceMatrix[0, 2]:F6}]\n" +
                        $"  [{covarianceMatrix[1, 0]:F6} {covarianceMatrix[1, 1]:F6} {covarianceMatrix[1, 2]:F6}]\n" +
                        $"  [{covarianceMatrix[2, 0]:F6} {covarianceMatrix[2, 1]:F6} {covarianceMatrix[2, 2]:F6}]\n");

            return covarianceMatrix;
        }

        private void CalculateOrientationAndConcentration()
        {
            var evd = covarianceMatrix.Evd();
            var concentrationValues = evd.EigenValues.Select(x => x.Real).ToArray();
            orientationMatrix = evd.EigenVectors.EnumerateColumns()
                .Select(col => new Vector3((float)col[0], (float)col[1], (float)col[2]))
                .ToArray();

            Array.Sort(concentrationValues, orientationMatrix);
            Array.Reverse(concentrationValues);
            Array.Reverse(orientationMatrix);

            for (int i = 0; i < 3; i++)
            {
                orientationMatrix[i] = orientationMatrix[i].normalized;
            }

            dispersionParameters = new double[3];
            for (int i = 0; i < 3; i++)
            {
                dispersionParameters[i] = 1.0 / (2.0 * (concentrationValues[i] + EPSILON));
                dispersionParameters[i] = Math.Min(Math.Max(dispersionParameters[i], MIN_DISPERSION), MAX_DISPERSION);
            }

            LogHandler.Log($"Eigen decomposition completed - Concentration values:\n" +
                           $"- Concentration value 0: {concentrationValues[0]:F6}\n" +
                           $"- Concentration value 1: {concentrationValues[1]:F6}\n" +
                           $"- Concentration value 2: {concentrationValues[2]:F6}");
            
            LogHandler.Log($"\nEstimated Bingham dispersion parameters:\n" +
                           $"  [{dispersionParameters[0]:F4}, {dispersionParameters[1]:F4}, {dispersionParameters[2]:F4}]\n" +
                           $"\nOrientation matrix:\n" +
                           $"  [{orientationMatrix[0].x:F4}, {orientationMatrix[0].y:F4}, {orientationMatrix[0].z:F4}]\n" +
                           $"  [{orientationMatrix[1].x:F4}, {orientationMatrix[1].y:F4}, {orientationMatrix[1].z:F4}]\n" +
                           $"  [{orientationMatrix[2].x:F4}, {orientationMatrix[2].y:F4}, {orientationMatrix[2].z:F4}]\n");
        }

        private void CalculateModelComplexity()
        {
            double spread = orientationMatrix.Max(x => x.magnitude) - orientationMatrix.Min(x => x.magnitude);
            double avgConcentration = orientationMatrix.Average(x => x.magnitude);
            modelComplexity = spread * avgConcentration / dataPoints.Count;
        }

        protected override void EstimateDistributionParameters()
        {
            normalizationConstant = CalculateNormalizationConstant();
        }

        private double CalculateNormalizationConstant()
        {
            double sum = dispersionParameters.Sum();
            if (sum < MIN_DISPERSION)
            {
                return 1.0 / (4.0 * Math.PI);
            }
            else if (sum > MAX_DISPERSION)
            {
                return Math.Sqrt(sum / (2 * Math.PI));
            }
            else
            {
                return sum / (4 * Math.PI * Math.Sinh(sum));
            }
        }

        protected override float CalculateDistributionDensity(Vector3 point)
        {
            double exponent = 0.0;
            for (int i = 0; i < 3; i++)
            {
                double dotProduct = Vector3.Dot(point, orientationMatrix[i]);
                exponent += dispersionParameters[i] * dotProduct * dotProduct;
            }
            
            double result = normalizationConstant * Math.Exp(exponent);
            return (float)Math.Min(Math.Max(result, MIN_DENSITY), MAX_DENSITY);
        }

        protected override List<Vector3> InitializeClustersByDensity()
        {
            List<Vector3> initialCentroids = new List<Vector3>();
            cachedLocalMaxima = null;
            
            // Inicjalizacja clusterAssignments
            clusterAssignments = new List<int>(new int[dataPoints.Count]);
            for (int i = 0; i < clusterAssignments.Count; i++)
            {
                clusterAssignments[i] = -1;
            }

            // Inicjalizacja remainingPointsSet
            remainingPointsSet = new HashSet<Vector3>(dataPoints);

            int desiredNeighborhoodSize = CalculateDesiredNeighborhoodSize();

            float currentDensityThreshold = densityThresholdFactor;

            while (initialCentroids.Count < numberOfClusters && currentDensityThreshold >= MIN_DENSITY_THRESHOLD)
            {
                maximaStopwatch.Start();
                List<Vector3> newMaxima = FindDensityMaximaWithThreshold(currentDensityThreshold);
                maximaStopwatch.Stop();

                foreach (var maximum in newMaxima)
                {
                    if (initialCentroids.Count < numberOfClusters)
                    {
                        initialCentroids.Add(maximum);
                        ExtractNeighborhood(maximum, initialCentroids.Count - 1);
                    }
                    else
                    {
                        break;
                    }
                }

                currentDensityThreshold *= 0.9f;
            }

            // Jeśli nadal brakuje klastrów, użyj losowych punktów
            while (initialCentroids.Count < numberOfClusters)
            {
                Vector3 randomPoint = dataPoints[UnityEngine.Random.Range(0, dataPoints.Count)];
                if (!initialCentroids.Contains(randomPoint))
                {
                    initialCentroids.Add(randomPoint);
                    ExtractNeighborhood(randomPoint, initialCentroids.Count - 1);
                }
            }
            
            // Sprawdzenie, ile punktów zostało przypisanych
            int assignedPoints = clusterAssignments.Count(x => x != -1);
            
            return initialCentroids;
        }

        protected override List<Vector3> FindDensityMaxima()
        {
            if (remainingPointsSet == null || remainingPointsSet.Count == 0)
            {
                LogHandler.Log("Warning: remainingPointsSet is null or empty in FindDensityMaxima");
                return new List<Vector3>();
            }

            maximaStopwatch.Start();
            
            maximaStopwatch.Start();
            FindAndCacheLocalMaxima(densityThresholdFactor);
            maximaStopwatch.Stop();

            var result = cachedLocalMaxima.Select(x => x.point).ToList();
            cachedLocalMaxima.Clear();

            maximaStopwatch.Stop();
            return result;
        }

        private List<Vector3> FindDensityMaximaWithThreshold(float densityThreshold)
        {
            if (remainingPointsSet == null || remainingPointsSet.Count == 0)
            {
                LogHandler.Log("Warning: remainingPointsSet is null or empty in FindDensityMaximaWithThreshold");
                return new List<Vector3>();
            }

            maximaStopwatch.Start();
            FindAndCacheLocalMaxima(densityThreshold);
            maximaStopwatch.Stop();

            var result = cachedLocalMaxima.Select(x => x.point).ToList();
            cachedLocalMaxima.Clear();

            return result;
        }

        private void FindAndCacheLocalMaxima(float densityThreshold)
        {

            cachedLocalMaxima = new List<(Vector3 point, float density)>();
            var remainingPointsList = remainingPointsSet.ToList();
            
            remainingPointsList.Sort((a, b) => a.x.CompareTo(b.x) != 0 ? a.x.CompareTo(b.x) : 
                                               a.y.CompareTo(b.y) != 0 ? a.y.CompareTo(b.y) : 
                                               a.z.CompareTo(b.z));

            float maxDensity = remainingPointsList.Max(p => CalculateDistributionDensity(p));
            float actualDensityThreshold = maxDensity * densityThreshold;

            foreach (var point in remainingPointsList)
            {
                float density = CalculateDistributionDensity(point);
                if (density >= actualDensityThreshold && IsLocalMaximum(point, density))
                {
                    cachedLocalMaxima.Add((point, density));
                }
            }

            cachedLocalMaxima = cachedLocalMaxima.OrderByDescending(x => x.density).ToList();
            totalLocalMaximaFound += cachedLocalMaxima.Count;
        }

        private bool IsLocalMaximum(Vector3 point, float density)
        {
            const float neighborhoodRadius = 0.1f;
            return !remainingPointsSet.Any(neighbor => 
                Vector3.Distance(point, neighbor) <= neighborhoodRadius && 
                CalculateDistributionDensity(neighbor) > density + DENSITY_COMPARISON_EPSILON);
        }

        protected override void ExtractNeighborhood(Vector3 centroid, int clusterIndex)
        {
            if (dataPoints == null || clusterAssignments == null)
            {
                LogHandler.Log("Error: dataPoints or clusterAssignments is null in ExtractNeighborhood");
                return;
            }

            int desiredNeighborhoodSize = CalculateDesiredNeighborhoodSize();
            var points = remainingPointsSet.ToList();
            var distances = new float[points.Count];

            ParallelComputation.ParallelFor(points, (point, i) =>
            {
                distances[i] = 1 - Vector3.Dot(point, centroid);
            });

            var sortedIndices = Enumerable.Range(0, points.Count).OrderBy(i => distances[i]).ToList();

            int extractedCount = 0;
            foreach (int i in sortedIndices)
            {
                if (extractedCount >= desiredNeighborhoodSize)
                    break;

                int index = dataPoints.IndexOf(points[i]);
                if (index >= 0 && index < clusterAssignments.Count && clusterAssignments[index] == -1)
                {
                    clusterAssignments[index] = clusterIndex;
                    remainingPointsSet.Remove(points[i]);
                    extractedCount++;
                }
            }
        }

        private int CalculateDesiredNeighborhoodSize()
        {
            int baseSize = Mathf.FloorToInt(dataPoints.Count * INITIAL_NEIGHBORHOOD_FACTOR / numberOfClusters);
            return Mathf.Max(MIN_POINTS_PER_CLUSTER, baseSize);
        }

        protected override void UpdateClusters()
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
                        LogHandler.Log($"Warning: Cluster {i} is empty");
                    }
                }

                // Obsługa pustych klastrów
                HandleEmptyClusters(newAssignments, newCentroids, clusterSizes);

                float meanDistance = CalculateMeanCentroidDistance(centroids, newCentroids.ToList());

                if (Math.Abs(meanDistance - previousMeanDistance) < convergenceThreshold * 0.1f)
                {
                    converged = true;
                }

                previousMeanDistance = meanDistance;
                centroids = newCentroids.ToList();
                clusterAssignments = newAssignments;
            }
        }

        private void HandleEmptyClusters(List<int> newAssignments, Vector3[] newCentroids, int[] clusterSizes)
        {
            var emptyClusters = Enumerable.Range(0, numberOfClusters)
                .Where(i => clusterSizes[i] == 0)
                .ToList();

            if (emptyClusters.Count > 0)
            {
                var sortedClusters = Enumerable.Range(0, numberOfClusters)
                    .OrderByDescending(i => clusterSizes[i])
                    .ToList();

                foreach (int emptyCluster in emptyClusters)
                {
                    int largestCluster = sortedClusters.First(c => c != emptyCluster);
                    SplitCluster(largestCluster, emptyCluster, newAssignments, newCentroids, clusterSizes);
                }
            }
        }

        private void SplitCluster(int sourceCluster, int targetCluster, List<int> assignments, Vector3[] centroids, int[] sizes)
        {
            var pointsInCluster = dataPoints.Where((_, i) => assignments[i] == sourceCluster).ToList();
            int pointsToMove = pointsInCluster.Count / 2;

            for (int i = 0; i < pointsToMove; i++)
            {
                int index = dataPoints.IndexOf(pointsInCluster[i]);
                assignments[index] = targetCluster;
            }

            sizes[sourceCluster] /= 2;
            sizes[targetCluster] = sizes[sourceCluster];

            centroids[targetCluster] = CalculateClusterCentroid(assignments, targetCluster);
            centroids[sourceCluster] = CalculateClusterCentroid(assignments, sourceCluster);
        }

        private Vector3 CalculateClusterCentroid(List<int> assignments, int clusterId)
        {
            var clusterPoints = dataPoints.Where((_, i) => assignments[i] == clusterId);
            return MathUtilities.CalculateDirectionalMean(clusterPoints.ToList());
        }

        public override void ExecuteClustering()
        {        
            // Reset stanu przed każdym wywołaniem
            cachedLocalMaxima = null;
            totalLocalMaximaFound = 0;

            base.ExecuteClustering();
        }

        protected override string GetAlgorithmName()
        {
            return "Bingham";
        }

        private void LogClusteringStatistics()
        {
            var spreads = orientationMatrix.Select(axis => 
            {
                float dotProducts = dataPoints.Select(v => Mathf.Abs(Vector3.Dot(v, axis))).Average();
                return 1 - dotProducts;
            }).ToList();

            double multimodalityIndex = (spreads[1] + spreads[2]) / (2 * spreads[0]);
            double overallConcentration = orientationMatrix.Sum(v => v.magnitude);
            double conditionNumber = orientationMatrix[0].magnitude / orientationMatrix[2].magnitude;

            LogHandler.Log(
                $"Bingham Distribution Analysis:\n" +
                $"- Multimodality index: {multimodalityIndex:F4}\n" +
                $"- Overall concentration: {overallConcentration:F4}\n" +
                $"- Condition number: {conditionNumber:F4}\n" +
                $"\nSpreads along principal axes:\n" +
                $"1. {spreads[0]:F4}\n" +
                $"2. {spreads[1]:F4}\n" +
                $"3. {spreads[2]:F4}"
            );
        }

        protected override void CalculateClusterParameters()
        {
            clusterParameters = new List<Vector3>();
            clusterCosineSimilarities = new List<double>();
            for (int i = 0; i < numberOfClusters; i++)
            {
                var clusterPoints = dataPoints.Where((_, index) => clusterAssignments[index] == i).ToList();
                if (clusterPoints.Count > 0)
                {
                    Matrix<double> clusterCovarianceMatrix = CalculateClusterCovarianceMatrix(clusterPoints);
                    var evd = clusterCovarianceMatrix.Evd();
                    var eigenValues = evd.EigenValues.Select(x => x.Real).OrderByDescending(x => x).ToArray();
                    
                    double z1 = 1.0 / (2.0 * (eigenValues[0] + EPSILON));
                    double z2 = 1.0 / (2.0 * (eigenValues[1] + EPSILON));
                    double z3 = 1.0 / (2.0 * (eigenValues[2] + EPSILON));

                    z1 = Math.Min(Math.Max(z1, MIN_DISPERSION), MAX_DISPERSION);
                    z2 = Math.Min(Math.Max(z2, MIN_DISPERSION), MAX_DISPERSION);
                    z3 = Math.Min(Math.Max(z3, MIN_DISPERSION), MAX_DISPERSION);

                    clusterParameters.Add(new Vector3((float)z1, (float)z2, (float)z3));

                    Vector3 clusterMeanDirection = MathUtilities.CalculateDirectionalMean(clusterPoints);
                    double cosineSimilarity = CalculateCosineSimilarity(clusterMeanDirection, clusterPoints);
                    clusterCosineSimilarities.Add(cosineSimilarity);
                }
            }

            CalculateParameterStatistics(clusterParameters.Select(v => v.x), "z1");
            CalculateParameterStatistics(clusterParameters.Select(v => v.y), "z2");
            CalculateParameterStatistics(clusterParameters.Select(v => v.z), "z3");
            CalculateParameterStatistics(clusterCosineSimilarities.Select(cs => (float)cs), "Cosine Similarity");
        }

        protected override void CalculateParameterVariability()
        {
            var z1Values = clusterParameters.Select(p => (double)p.x).ToList();
            var z2Values = clusterParameters.Select(p => (double)p.y).ToList();
            var z3Values = clusterParameters.Select(p => (double)p.z).ToList();

            double cvZ1 = CalculateCV(z1Values);
            double cvZ2 = CalculateCV(z2Values);
            double cvZ3 = CalculateCV(z3Values);
            double cvCosineSimilarity = CalculateCV(clusterCosineSimilarities);

            LogHandler.Log($"\nParameter Variability for Bingham:");
            LogHandler.Log($"z1 CV: {cvZ1:F2}%");
            LogHandler.Log($"z2 CV: {cvZ2:F2}%");
            LogHandler.Log($"z3 CV: {cvZ3:F2}%");
            LogHandler.Log($"Cosine Similarity CV: {cvCosineSimilarity:F2}%");
        }
    }
}