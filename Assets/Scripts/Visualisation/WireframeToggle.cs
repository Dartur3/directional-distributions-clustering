using UnityEngine;
using ModelViewer.Core;

namespace ModelViewer.Visualization
{
    public class WireframeToggle : MonoBehaviour
    {
        private StaticWireframeRenderer[] wireframeRenderers;
        private ClusteringController clusteringController;

        private void Start()
        {
            UpdateWireframeRenderers();
            clusteringController = FindObjectOfType<ClusteringController>();
            if (clusteringController == null)
            {
                Debug.LogError("ClusteringController not found in the scene.");
            }
        }

        private void UpdateWireframeRenderers()
        {
            wireframeRenderers = FindObjectsOfType<StaticWireframeRenderer>();
        }

        public void ToggleWireframe()
        {
            wireframeRenderers = FindObjectsOfType<StaticWireframeRenderer>();
            bool newState = !wireframeRenderers[0].enabled;

            foreach (var renderer in wireframeRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = newState;
                    
                    if (newState && clusteringController != null && clusteringController.IsClusteringCompleted)
                    {
                        renderer.UpdateClusterAssignments(
                            clusteringController.GetClusterAssignments(),
                            clusteringController.GetUnassignedTriangles()
                        );
                    }
                    else
                    {
                        renderer.ResetClusterVisualization();
                    }
                }
            }
        }

        public void RefreshWireframeRenderers()
        {
            UpdateWireframeRenderers();
            foreach (var renderer in wireframeRenderers)
            {
                if (renderer != null)
                {
                    renderer.ResetClusterVisualization();
                }
            }
        }
    }
}