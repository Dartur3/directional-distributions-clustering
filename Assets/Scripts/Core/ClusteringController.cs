using UnityEngine;
using System.Collections.Generic;
using ModelViewer.Clustering;
using ModelViewer.Utilities;
using ModelViewer.Visualization;
using ModelViewer.UI;

namespace ModelViewer.Core
{
    public class ClusteringController : MonoBehaviour
    {
        private NormalExtractor normalExtractor;
        private BaseSphericalClustering currentAlgorithm;
        private StaticWireframeRenderer[] wireframeRenderers;

        private List<Vector3> extractedNormals;
        private int[] clusterAssignments;
        private HashSet<int> unassignedTriangles;
        private GameObject importedModel;

        public bool IsClusteringCompleted { get; private set; }

        public event System.Action OnClusteringCompleted;

        private void Awake()
        {
            FindRequiredComponents();
        }

        private void FindRequiredComponents()
        {
            normalExtractor = FindObjectOfType<NormalExtractor>();

            if (normalExtractor == null)
            {
                Debug.LogWarning("NormalExtractor not found in the scene. Make sure this component exists.");
            }
        }

        public void SetImportedModel(GameObject model)
        {
            importedModel = model;
            IsClusteringCompleted = false;
        }

        private void ExtractNormals()
        {
            if (importedModel == null)
            {
                ErrorHandler.DisplayError("No model available for normal extraction.");
                return;
            }

            MeshFilter meshFilter = importedModel.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                ErrorHandler.DisplayError("MeshFilter not found on the imported model.");
                return;
            }

            extractedNormals = normalExtractor.ExtractNormals(meshFilter);
            if (extractedNormals == null || extractedNormals.Count == 0)
            {
                ErrorHandler.DisplayError("Failed to extract normals from the mesh.");
                return;
            }
        }

        public void ExecuteClustering(int numberOfClusters, ClusteringAlgorithm algorithm)
        {
            LogHandler.Clear();
            
            ExtractNormals();

            if (extractedNormals == null || extractedNormals.Count == 0)
            {
                ErrorHandler.DisplayError("No normals available for clustering.");
                return;
            }

            // Remove previous algorithm component if it exists
            if (currentAlgorithm != null)
            {
                Destroy(currentAlgorithm as MonoBehaviour);
            }

            currentAlgorithm = CreateAlgorithm(algorithm);
            if (currentAlgorithm == null)
            {
                ErrorHandler.DisplayError("Failed to create clustering algorithm.");
                return;
            }

            currentAlgorithm.SetNumberOfClusters(numberOfClusters);
            currentAlgorithm.InitializeClustering(extractedNormals);
            currentAlgorithm.ExecuteClustering();

            clusterAssignments = currentAlgorithm.GetClusterAssignments();
            unassignedTriangles = currentAlgorithm.GetUnassignedTriangles();
            IsClusteringCompleted = true;

            // Aktualizacja liczby klastrów po utworzeniu spójnych klastrów
            numberOfClusters = currentAlgorithm.GetNumberOfClusters();

            UpdateWireframeRenderers();

            OnClusteringCompleted?.Invoke();

        }

        private BaseSphericalClustering CreateAlgorithm(ClusteringAlgorithm algorithm)
        {
            // Remove any existing algorithm components
            foreach (var existingAlgorithm in GetComponents<BaseSphericalClustering>())
            {
                Destroy(existingAlgorithm);
            }

            // Create and return the new algorithm component
            switch (algorithm)
            {
                case ClusteringAlgorithm.SphericalKMeans:
                    return gameObject.AddComponent<SphericalKMeansClustering>();
                case ClusteringAlgorithm.VMF:
                    return gameObject.AddComponent<VMFClustering>();
                case ClusteringAlgorithm.Bingham:
                    return gameObject.AddComponent<BinghamClustering>();
                case ClusteringAlgorithm.Kent:
                    return gameObject.AddComponent<KentClustering>();
                default:
                    return null;
            }
        }

        public int GetExtractedNormalsCount()
        {
            return extractedNormals?.Count ?? 0;
        }

        public int[] GetClusterAssignments()
        {
            return clusterAssignments;
        }

        public HashSet<int> GetUnassignedTriangles()
        {
            return currentAlgorithm?.GetUnassignedTriangles() ?? new HashSet<int>();
        }

        private void UpdateWireframeRenderers()
        {
            wireframeRenderers = FindObjectsOfType<StaticWireframeRenderer>();
            foreach (var renderer in wireframeRenderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    renderer.UpdateClusterAssignments(clusterAssignments, unassignedTriangles);
                }
            }
        }

        public void ResetClustering()
        {
            extractedNormals = null;
            clusterAssignments = null;
            unassignedTriangles = null;
            IsClusteringCompleted = false;

            if (wireframeRenderers != null)
            {
                foreach (var renderer in wireframeRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.ResetClusterVisualization();
                    }
                }
            }

            if (currentAlgorithm != null)
            {
                Destroy(currentAlgorithm as MonoBehaviour);
                currentAlgorithm = null;
            }
        }

        public int GetFinalClusterCount()
        {
            return currentAlgorithm?.GetNumberOfClusters() ?? 0;
        }
    }
}