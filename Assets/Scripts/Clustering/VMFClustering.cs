using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;
using System;
using System.Threading.Tasks;

namespace ModelViewer.Clustering
{
    public class VMFClustering : DirectionalDistributionClustering
    {
        private const double MIN_DENSITY = 1e-10;
        private const double MAX_DENSITY = 1e6;
        private const double MIN_KAPPA = 0.01;
        private const double MAX_KAPPA = 1000.0;
        private const double HIGH_CONCENTRATION_THRESHOLD = 0.8;
        private const double EPSILON = 1e-10;

        private double kappa;
        private double normalizationConstant;
        private Vector3 meanDirection;
        private List<double> clusterKappaValues;
        private List<double> clusterCosineSimilarities;

        protected override void AnalyzeVectorDistribution()
        {
            base.AnalyzeVectorDistribution();
            meanDirection = MathUtilities.CalculateDirectionalMean(dataPoints);
            
            double averageDeviation = dataPoints.AsParallel().Average(v => 1 - Vector3.Dot(v, meanDirection));
            double variance = dataPoints.AsParallel().Average(v => {
                double dev = 1 - Vector3.Dot(v, meanDirection);
                return dev * dev;
            }) - averageDeviation * averageDeviation;
            
            LogClusteringStatistics(averageDeviation, variance);
        }

        private void LogClusteringStatistics(double averageDeviation, double variance)
        {
            var angles = dataPoints.Select(v => Math.Acos(Vector3.Dot(v, meanDirection)) * 180.0 / Math.PI);
            var averageAngle = angles.Average();
            var maxAngle = angles.Max();
            var minAngle = angles.Min();
            var angleStdDev = Math.Sqrt(angles.Select(a => (a - averageAngle) * (a - averageAngle)).Average());

            double uniformSpreadMeasure = Math.Sqrt(2.0 / 3.0);
            double relativeSpread = averageAngle / (90.0 * uniformSpreadMeasure);

            LogHandler.Log(
                $"\nClustering Statistics:\n" +
                $"- Mean direction: {meanDirection} (unit vector)\n" +
                $"- Average angle from mean: {averageAngle:F2}° (90° would indicate uniform spread)\n" +
                $"- Angle range: {minAngle:F2}° to {maxAngle:F2}°\n" +
                $"- Angle standard deviation: {angleStdDev:F2}°\n" +
                $"- Relative spread: {relativeSpread:F2} (1,0 would indicate uniform spread)\n" +
                $"- Average deviation: {averageDeviation:F6}\n" +
                $"- Variance: {variance:F6}\n"
            );
        }

        protected override void EstimateDistributionParameters()
        {
            EstimateKappa();
            CalculateNormalizationConstant();
        }

        private void EstimateKappa()
        {
            Vector3 R = Vector3.zero;
            foreach (var v in dataPoints)
            {
                R += v;
            }
            double rBar = R.magnitude / dataPoints.Count;

            double concentrationMeasure = CalculateConcentrationMeasure();

            LogHandler.Log($"R-bar: {rBar:F6}, Concentration measure: {concentrationMeasure:F6}");

            if (rBar > HIGH_CONCENTRATION_THRESHOLD)
            {
                kappa = MAX_KAPPA;
                LogHandler.Log($"Very high concentration detected, setting kappa to maximum: {kappa}");
            }
            else
            {
                // Using Banerjee et al. approximation for kappa estimation
                double d = 3; // dimension of the space (for 3D vectors)
                kappa = (rBar * d - Math.Pow(rBar, 3)) / (1 - Math.Pow(rBar, 2));
                kappa = Math.Max(kappa, MIN_KAPPA);
                kappa = Math.Min(kappa, MAX_KAPPA);
            }

            LogHandler.Log($"Estimated kappa: {kappa:F6}");
            InterpretKappa(rBar, concentrationMeasure);
        }

        private double CalculateConcentrationMeasure()
        {
            return dataPoints.AsParallel().Average(v => Math.Pow(Vector3.Dot(v, meanDirection), 2));
        }

        private void InterpretKappa(double rBar, double concentrationMeasure)
        {
            string interpretation;
            if (kappa < 1)
            {
                interpretation = "Very low kappa detected. This indicates extremely widely spread normal vectors, suggesting a highly complex or varied surface.";
            }
            else if (kappa < 2)
            {
                interpretation = "Low kappa detected. This indicates widely spread normal vectors, suggesting a complex or highly varied surface.";
            }
            else if (kappa < 5)
            {
                interpretation = "Moderate kappa detected. This suggests a mix of surface orientations with some local consistency.";
            }
            else if (kappa < 10)
            {
                interpretation = "Moderately high kappa detected. This indicates more consistent surface orientations, possibly suggesting smoother areas.";
            }
            else
            {
                interpretation = "High kappa detected. This suggests highly consistent surface orientations, indicating very smooth or uniform areas.";
            }

            LogHandler.Log($"Kappa estimation results:\n" +
                        $"- Estimated kappa: {kappa:F6}\n" +
                        $"- rBar: {rBar:F6} (0 - uniform spread, 1 - all vectors aligned)\n" +
                        $"- Concentration measure: {concentrationMeasure:F6} (higher values indicate more concentration)\n" +
                        $"\n{interpretation}\n");
        }

        private void CalculateNormalizationConstant()
        {
            if (kappa < MIN_KAPPA)
            {
                normalizationConstant = 1.0 / (4.0 * Math.PI);
            }
            else if (kappa > MAX_KAPPA)
            {
                normalizationConstant = Math.Sqrt(kappa / (2.0 * Math.PI));
            }
            else
            {
                normalizationConstant = kappa / (4.0 * Math.PI * Math.Sinh(kappa));
            }
            normalizationConstant = Math.Max(normalizationConstant, EPSILON);
            LogHandler.Log($"Normalization constant: c = {normalizationConstant:F8}");
        }

        protected override float CalculateDistributionDensity(Vector3 point)
        {
            double dotProduct = Vector3.Dot(point, meanDirection);
            double expTerm = Math.Exp(kappa * (dotProduct - 1));
            double result = normalizationConstant * expTerm;
            return (float)Math.Clamp(result, MIN_DENSITY, MAX_DENSITY);
        }

        protected override List<Vector3> FindDensityMaxima()
        {
            if (remainingPointsSet == null || remainingPointsSet.Count == 0)
            {
                LogHandler.Log("Warning: remainingPointsSet is null or empty in FindDensityMaxima");
                return new List<Vector3>();
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

            // Sort points by their position to ensure consistent results
            maxDensityPoints.Sort((a, b) => 
                a.point.x.CompareTo(b.point.x) != 0 ? a.point.x.CompareTo(b.point.x) :
                a.point.y.CompareTo(b.point.y) != 0 ? a.point.y.CompareTo(b.point.y) :
                a.point.z.CompareTo(b.point.z));

            return new List<Vector3> { maxDensityPoints[0].point };
        }

        protected override string GetAlgorithmName()
        {
            return "von Mises-Fisher (vMF)";
        }

        protected override void CalculateClusterParameters()
        {
            clusterKappaValues = new List<double>();
            clusterCosineSimilarities = new List<double>();
            for (int i = 0; i < numberOfClusters; i++)
            {
                var clusterPoints = dataPoints.Where((_, index) => clusterAssignments[index] == i).ToList();
                if (clusterPoints.Count > 0)
                {
                    Vector3 clusterMeanDirection = MathUtilities.CalculateDirectionalMean(clusterPoints);
                    double rBar = clusterPoints.Average(v => Vector3.Dot(v, clusterMeanDirection));
                    double d = 3; // dimension of the space (for 3D vectors)
                    double clusterKappa = (rBar * d - Math.Pow(rBar, 3)) / (1 - Math.Pow(rBar, 2));
                    clusterKappa = Math.Clamp(clusterKappa, MIN_KAPPA, MAX_KAPPA);
                    clusterKappaValues.Add(clusterKappa);

                    double cosineSimilarity = CalculateCosineSimilarity(clusterMeanDirection, clusterPoints);
                    clusterCosineSimilarities.Add(cosineSimilarity);
                }
            }

            CalculateParameterStatistics(clusterKappaValues.Select(k => (float)k), "Kappa");
            CalculateParameterStatistics(clusterCosineSimilarities.Select(cs => (float)cs), "Cosine Similarity");
        }

        protected override void CalculateParameterVariability()
        {
            double cvKappa = CalculateCV(clusterKappaValues);
            double cvCosineSimilarity = CalculateCV(clusterCosineSimilarities);

            LogHandler.Log($"\nParameter Variability for von Mises-Fisher (vMF):");
            LogHandler.Log($"Kappa CV: {cvKappa:F2}%");
            LogHandler.Log($"Cosine Similarity CV: {cvCosineSimilarity:F2}%");
        }
    }
}