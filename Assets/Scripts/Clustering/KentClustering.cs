using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;
using System;
using MathNet.Numerics.LinearAlgebra;

namespace ModelViewer.Clustering
{
    public class KentClustering : DirectionalDistributionClustering
    {
        private const double NORMALIZATION_CONSTANT_BASE = 1.0 / (4.0 * Math.PI);
        private const double MIN_KAPPA = 0.01;
        private const double MAX_KAPPA = 1000.0;
        private const double EPSILON = 1e-10;

        private Vector3 gamma1, gamma2, gamma3;
        private double kappa, beta;
        private double normalizationConstant;
        private List<(double kappa, double beta)> clusterParameters;
        private List<double> clusterCosineSimilarities;

        protected override void AnalyzeVectorDistribution()
        {
            base.AnalyzeVectorDistribution();
            
            Vector3 meanDirection = MathUtilities.CalculateDirectionalMean(dataPoints);
            Matrix<double> covarianceMatrix = CalculateCovarianceMatrix(meanDirection);
            
            var evd = covarianceMatrix.Evd();
            var concentrationValues = evd.EigenValues.Select(x => x.Real).ToArray();
            var orientationMatrix = evd.EigenVectors.EnumerateColumns()
                .Select(col => new Vector3((float)col[0], (float)col[1], (float)col[2]))
                .ToArray();

            Array.Sort(concentrationValues, orientationMatrix);
            Array.Reverse(concentrationValues);
            Array.Reverse(orientationMatrix);

            gamma1 = orientationMatrix[0].normalized;
            gamma2 = orientationMatrix[1].normalized;
            gamma3 = orientationMatrix[2].normalized;

            EstimateKappaAndBeta(concentrationValues);

            LogClusteringStatistics(meanDirection, covarianceMatrix, concentrationValues);
        }

        private Matrix<double> CalculateCovarianceMatrix(Vector3 meanDirection)
        {
            var covarianceMatrix = Matrix<double>.Build.Dense(3, 3);

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double covariance = ParallelComputation.ParallelSelect(dataPoints,
                        v => (v[i] - meanDirection[i]) * (v[j] - meanDirection[j])).Average();
                    covarianceMatrix[i, j] = covariance;
                }
            }

            return covarianceMatrix;
        }

        private void EstimateKappaAndBeta(double[] concentrationValues)
        {
            kappa = 1.0 / (2.0 * (concentrationValues[2] + EPSILON));
            kappa = Math.Min(Math.Max(kappa, MIN_KAPPA), MAX_KAPPA);

            double betaRange = (concentrationValues[1] - concentrationValues[2]) / (concentrationValues[0] - concentrationValues[2] + EPSILON);
            beta = kappa * betaRange;

            double maxBeta = kappa / 2.0 - EPSILON;
            beta = Math.Min(beta, maxBeta);
        }

        protected override void EstimateDistributionParameters()
        {
            normalizationConstant = CalculateNormalizationConstant(kappa, beta);

            LogHandler.LogParameterEstimation("Kappa", (float)kappa);
            LogHandler.LogParameterEstimation("Beta", (float)beta);
            LogHandler.LogParameterEstimation("Normalization constant", (float)normalizationConstant);
        }

        private double CalculateNormalizationConstant(double kappa, double beta)
        {
            if (beta == 0)
            {
                return NORMALIZATION_CONSTANT_BASE * MathUtilities.ModifiedBesselI0((float)kappa) / Math.Sinh(kappa);
            }
            else
            {
                return NORMALIZATION_CONSTANT_BASE * MathUtilities.ModifiedBesselI0((float)kappa) / Math.Sinh(kappa) * (1.0 - beta * beta / (2.0 * kappa));
            }
        }

        protected override float CalculateDistributionDensity(Vector3 point)
        {
            double x = Vector3.Dot(point, gamma1);
            double y = Vector3.Dot(point, gamma2);
            double z = Vector3.Dot(point, gamma3);

            double exponent = kappa * z + beta * (x * x - y * y);
            return (float)(1.0 / normalizationConstant * Math.Exp(exponent));
        }

        protected override List<Vector3> FindDensityMaxima()
        {
            if (remainingPointsSet == null || remainingPointsSet.Count == 0)
            {
                LogHandler.Log("Warning: remainingPointsSet is null or empty in FindDensityMaxima");
                return dataPoints.Count > 0 ? new List<Vector3> { dataPoints[0] } : new List<Vector3>();
            }

            var remainingPointsList = remainingPointsSet.ToList();
            var maxDensityPoints = new List<(Vector3 point, float density)>();
            float maxDensity = float.MinValue;

            for (int i = 0; i < remainingPointsList.Count; i++)
            {
                int originalIndex = dataPoints.IndexOf(remainingPointsList[i]);
                if (originalIndex >= 0 && originalIndex < precalculatedDensities.Length)
                {
                    float density = precalculatedDensities[originalIndex];
                    if (density > maxDensity)
                    {
                        maxDensityPoints.Clear();
                        maxDensityPoints.Add((remainingPointsList[i], density));
                        maxDensity = density;
                    }
                    else if (Math.Abs(density - maxDensity) < float.Epsilon)
                    {
                        maxDensityPoints.Add((remainingPointsList[i], density));
                    }
                }
            }

            // If no points were found, use the first point from remainingPointsList
            if (maxDensityPoints.Count == 0 && remainingPointsList.Count > 0)
            {
                maxDensityPoints.Add((remainingPointsList[0], 0f));
            }

            // If still no points, use the first point from dataPoints
            if (maxDensityPoints.Count == 0 && dataPoints.Count > 0)
            {
                maxDensityPoints.Add((dataPoints[0], 0f));
            }

            // Sort points by their position to ensure consistent results
            maxDensityPoints.Sort((a, b) => 
                a.point.x.CompareTo(b.point.x) != 0 ? a.point.x.CompareTo(b.point.x) :
                a.point.y.CompareTo(b.point.y) != 0 ? a.point.y.CompareTo(b.point.y) :
                a.point.z.CompareTo(b.point.z));

            return new List<Vector3> { maxDensityPoints[0].point };
        }

        protected override string GetAlgorithmName()
        {
            return "Kent";
        }

        private void LogClusteringStatistics(Vector3 meanDirection, Matrix<double> covarianceMatrix, double[] concentrationValues)
        {
            double concentration = kappa;
            double elipticity = beta / kappa;

            LogHandler.Log(
                $"\nKent Distribution Analysis:\n" +
                $"- Mean direction: {meanDirection}\n" +
                $"- Concentration (kappa): {concentration:F6}\n" +
                $"- Elipticity (beta/kappa): {elipticity:F6}\n" +
                $"- Mean direction (gamma1): {gamma1}\n" +
                $"- Major axis (gamma2): {gamma2}\n" +
                $"- Minor axis (gamma3): {gamma3}\n" +
                $"- Concentration values: [{concentrationValues[0]:F6}, {concentrationValues[1]:F6}, {concentrationValues[2]:F6}]\n" +
                $"- Covariance Matrix:\n" +
                $"  [{covarianceMatrix[0, 0]:F6} {covarianceMatrix[0, 1]:F6} {covarianceMatrix[0, 2]:F6}]\n" +
                $"  [{covarianceMatrix[1, 0]:F6} {covarianceMatrix[1, 1]:F6} {covarianceMatrix[1, 2]:F6}]\n" +
                $"  [{covarianceMatrix[2, 0]:F6} {covarianceMatrix[2, 1]:F6} {covarianceMatrix[2, 2]:F6}]\n"
            );
        }

        protected override void CalculateClusterParameters()
        {
            clusterParameters = new List<(double kappa, double beta)>();
            clusterCosineSimilarities = new List<double>();
            for (int i = 0; i < numberOfClusters; i++)
            {
                var clusterPoints = dataPoints.Where((_, index) => clusterAssignments[index] == i).ToList();
                if (clusterPoints.Count > 0)
                {
                    Vector3 meanDirection = MathUtilities.CalculateDirectionalMean(clusterPoints);
                    Matrix<double> covMatrix = CalculateClusterCovarianceMatrix(clusterPoints);
                    var evd = covMatrix.Evd();
                    var concentrationValues = evd.EigenValues.Select(x => x.Real).OrderByDescending(x => x).ToArray();

                    double clusterKappa = 1.0 / (2.0 * (concentrationValues[2] + EPSILON));
                    clusterKappa = Math.Min(Math.Max(clusterKappa, MIN_KAPPA), MAX_KAPPA);

                    double betaRange = (concentrationValues[1] - concentrationValues[2]) / (concentrationValues[0] - concentrationValues[2] + EPSILON);
                    double clusterBeta = clusterKappa * betaRange;
                    clusterBeta = Math.Min(clusterBeta, clusterKappa / 2.0 - EPSILON);

                    clusterParameters.Add((clusterKappa, clusterBeta));

                    double cosineSimilarity = CalculateCosineSimilarity(meanDirection, clusterPoints);
                    clusterCosineSimilarities.Add(cosineSimilarity);
                }
            }

            CalculateParameterStatistics(clusterParameters.Select(p => (float)p.kappa), "Kappa");
            CalculateParameterStatistics(clusterParameters.Select(p => (float)p.beta), "Beta");
            CalculateParameterStatistics(clusterCosineSimilarities.Select(cs => (float)cs), "Cosine Similarity");
        }

        protected override void CalculateParameterVariability()
        {
            var kappaValues = clusterParameters.Select(p => p.kappa).ToList();
            var betaValues = clusterParameters.Select(p => p.beta).ToList();

            double cvKappa = CalculateCV(kappaValues);
            double cvBeta = CalculateCV(betaValues);
            double cvCosineSimilarity = CalculateCV(clusterCosineSimilarities);

            LogHandler.Log($"\nParameter Variability for Kent:");
            LogHandler.Log($"Kappa CV: {cvKappa:F2}%");
            LogHandler.Log($"Beta CV: {cvBeta:F2}%");
            LogHandler.Log($"Cosine Similarity CV: {cvCosineSimilarity:F2}%");
        }
    }
}