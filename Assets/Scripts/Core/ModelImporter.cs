using Dummiesman;
using UnityEngine;
using UnityEditor;
using TMPro;
using System;
using System.Collections.Generic;
using ModelViewer.Utilities;
using ModelViewer.Clustering;
using ModelViewer.UI;
using ModelViewer.Visualization;
using ModelViewer.Core;

namespace ModelViewer.Core
{
    public class ModelImporter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1f, -4.5f);
        [SerializeField] private Vector3 parentScale = Vector3.one;
        [SerializeField] private float targetSize = 2f;
        [SerializeField] private ModelManipulator modelManipulator;
        [SerializeField] private ClusteringToggle clusteringToggle;
        [SerializeField] private ClusteringController clusteringController;

        private GameObject lastImportedModel;

        public event Action OnModelImported;

        private void Start()
        {
            ErrorHandler.Initialize(errorText);
        }

        public void ImportModel()
        {
            ErrorHandler.ClearError();
            LogHandler.Clear();

            clusteringToggle = FindObjectOfType<ClusteringToggle>();

            string filePath = EditorUtility.OpenFilePanel("Select OBJ Model", "", "obj");
            if (IsInvalidFilePath(filePath))
            {
                ErrorHandler.DisplayError(lastImportedModel == null ? "No model imported and no file selected." : null);
                return;
            }

            try
            {
                GameObject loadedObject = new OBJLoader().Load(filePath);
                if (loadedObject == null)
                {
                    throw new Exception("Failed to load the OBJ file.");
                }

                SetupModel(loadedObject);
                
                if (clusteringToggle != null)
                {
                    clusteringToggle.SetClusteringUIActive(true);
                }

                OnModelImported?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorHandler.DisplayError(ex.Message);
            }
        }

        private bool IsInvalidFilePath(string filePath)
        {
            return string.IsNullOrEmpty(filePath) || !filePath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase);
        }

        private void SetupModel(GameObject loadedObject)
        {
            if (lastImportedModel != null)
            {
                Destroy(lastImportedModel);
                FindObjectOfType<WireframeToggle>().RefreshWireframeRenderers();
                
                if (clusteringToggle != null)
                {
                    clusteringToggle.SetClusteringUIActive(false);
                }
            }

            GameObject parentObject = CreateParentObject();
            GameObject pivotObject = CreatePivotObject(parentObject);

            GameObject mergedModel = MergeMeshes(loadedObject);
            mergedModel.transform.SetParent(pivotObject.transform, false);

            Bounds bounds = CalculateBounds(mergedModel);
            CenterAndScaleModel(pivotObject, mergedModel, bounds);

            AddMeshCollider(mergedModel);
            AddRenderersAndClusteringComponents(mergedModel);

            lastImportedModel = parentObject;
            if (modelManipulator != null)
            {
                modelManipulator.SetModelParent(parentObject);
            }

            FindObjectOfType<WireframeToggle>().RefreshWireframeRenderers();

            if (clusteringToggle != null && clusteringController != null)
            {
                clusteringController.SetImportedModel(mergedModel);
                clusteringToggle.SetMaxClusterCount(mergedModel.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
            }

            // Clean up the original loaded object
            Destroy(loadedObject);

            OnModelImported?.Invoke();
        }

        private GameObject CreateParentObject()
        {
            GameObject parentObject = new GameObject("ModelParent");
            parentObject.transform.position = spawnPosition;
            parentObject.transform.localScale = parentScale;
            return parentObject;
        }

        private GameObject CreatePivotObject(GameObject parent)
        {
            GameObject pivotObject = new GameObject("ModelPivot");
            pivotObject.transform.SetParent(parent.transform, false);
            return pivotObject;
        }

        private GameObject MergeMeshes(GameObject originalObject)
        {
            MeshFilter[] meshFilters = originalObject.GetComponentsInChildren<MeshFilter>();
            CombineInstance[] combine = new CombineInstance[meshFilters.Length];

            for (int i = 0; i < meshFilters.Length; i++)
            {
                combine[i].mesh = meshFilters[i].sharedMesh;
                combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            }

            GameObject mergedObject = new GameObject("MergedModel");
            MeshFilter mergedMeshFilter = mergedObject.AddComponent<MeshFilter>();
            MeshRenderer mergedMeshRenderer = mergedObject.AddComponent<MeshRenderer>();

            mergedMeshFilter.mesh = new Mesh();
            mergedMeshFilter.mesh.CombineMeshes(combine);

            // Combine materials
            List<Material> materials = new List<Material>();
            foreach (MeshRenderer meshRenderer in originalObject.GetComponentsInChildren<MeshRenderer>())
            {
                materials.AddRange(meshRenderer.sharedMaterials);
            }
            mergedMeshRenderer.materials = materials.ToArray();

            return mergedObject;
        }

        private void CenterAndScaleModel(GameObject pivotObject, GameObject model, Bounds bounds)
        {
            Vector3 centerOffset = -bounds.center;
            pivotObject.transform.localPosition = centerOffset;
            model.transform.localPosition = -centerOffset;

            float scale = targetSize / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            model.transform.localScale = Vector3.one * scale;
        }

        private Bounds CalculateBounds(GameObject obj)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return new Bounds(Vector3.zero, Vector3.zero);
            return renderer.bounds;
        }

        private void AddMeshCollider(GameObject model)
        {
            MeshFilter meshFilter = model.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                MeshCollider collider = model.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        private void AddRenderersAndClusteringComponents(GameObject model)
        {
            var wireframeRenderer = model.AddComponent<StaticWireframeRenderer>();
            wireframeRenderer.enabled = false;
        }
    }
}