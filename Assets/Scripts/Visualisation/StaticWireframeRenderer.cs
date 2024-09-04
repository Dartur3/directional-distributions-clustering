using System.Collections.Generic;
using UnityEngine;
using ModelViewer.Clustering;

namespace ModelViewer.Visualization
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class StaticWireframeRenderer : MonoBehaviour {

        private const float WireframeDistance = 1.0001f;
        private List<Vector3> _renderingQueue;
        private Material _defaultWireMaterial;
        private ClusterColorManager _clusterColorManager;
        private Dictionary<int, int> _triangleClusterAssignments;
        private HashSet<int> _unassignedTriangles;
        private bool _isClusteringVisualizationActive = false;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;

        private void Start() {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            InitializeOnDemand();
        }

        private void InitializeOnDemand() {
            if (_renderingQueue != null) {
                return;
            }
            if (_meshFilter == null || _meshFilter.sharedMesh == null) {
                Debug.LogError("No mesh detected at " + gameObject.name, gameObject);
                return;
            }
            var mesh = _meshFilter.sharedMesh;

            _renderingQueue = new List<Vector3>();
            foreach (var triangle in mesh.triangles) {
                _renderingQueue.Add(mesh.vertices[triangle]);
            }

            _clusterColorManager = FindObjectOfType<ClusterColorManager>();
            if (_clusterColorManager == null) {
                Debug.LogError("ClusterColorManager not found in the scene.", gameObject);
            }

            // Disable the default renderer
            if (_meshRenderer != null) {
                _meshRenderer.enabled = false;
            }
        }

        private void CreateDefaultMaterial() {
            if (_defaultWireMaterial == null) {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                _defaultWireMaterial = new Material(shader) {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _defaultWireMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _defaultWireMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _defaultWireMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _defaultWireMaterial.SetInt("_ZWrite", 1);
            }
        }

        public void OnPreRender() {
            if (Camera.current != null && Camera.current.cullingMask == (1 << LayerMask.NameToLayer("UI"))) {
                return;
            }
            GL.wireframe = true;
        }

        public void OnRenderObject() {
            if (Camera.current != null && Camera.current.cullingMask == (1 << LayerMask.NameToLayer("UI"))) {
                return;
            }

            InitializeOnDemand();
            CreateDefaultMaterial();

            _defaultWireMaterial.SetPass(0);

            GL.PushMatrix();
            GL.MultMatrix(transform.localToWorldMatrix);

            // Render colored triangles if clustering is active
            if (_isClusteringVisualizationActive && _triangleClusterAssignments != null) {
                GL.Begin(GL.TRIANGLES);
                for (int i = 0; i < _renderingQueue.Count; i += 3) {
                    int triangleIndex = i / 3;
                    Color clusterColor;
                    if (_unassignedTriangles.Contains(triangleIndex) || !_triangleClusterAssignments.TryGetValue(triangleIndex, out int clusterId)) {
                        clusterColor = Color.black;
                    } else {
                        clusterColor = _clusterColorManager.GetColorForCluster(clusterId);
                    }
                    GL.Color(clusterColor);

                    GL.Vertex(_renderingQueue[i]);
                    GL.Vertex(_renderingQueue[i + 1]);
                    GL.Vertex(_renderingQueue[i + 2]);
                }
                GL.End();
            }

            // Always render black wireframe
            GL.Begin(GL.LINES);
            GL.Color(Color.black);
            for (int i = 0; i < _renderingQueue.Count; i += 3) {
                Vector3 v1 = _renderingQueue[i] * WireframeDistance;
                Vector3 v2 = _renderingQueue[i + 1] * WireframeDistance;
                Vector3 v3 = _renderingQueue[i + 2] * WireframeDistance;
                GL.Vertex(v1);
                GL.Vertex(v2);
                GL.Vertex(v2);
                GL.Vertex(v3);
                GL.Vertex(v3);
                GL.Vertex(v1);
            }
            GL.End();

            GL.PopMatrix();

            GL.wireframe = false;
        }

        public void OnPostRender() {
            GL.wireframe = false;
        }

        public void UpdateClusterAssignments(int[] clusterAssignments, HashSet<int> unassignedTriangles) {
            _triangleClusterAssignments = new Dictionary<int, int>();
            for (int i = 0; i < clusterAssignments.Length; i++) {
                if (clusterAssignments[i] != -1) {
                    _triangleClusterAssignments[i] = clusterAssignments[i];
                }
            }
            _unassignedTriangles = unassignedTriangles;
            _isClusteringVisualizationActive = true;
        }

        public void ResetClusterVisualization() {
            _triangleClusterAssignments = null;
            _unassignedTriangles = null;
            _isClusteringVisualizationActive = false;
        }

        private void OnDisable() {
            // Re-enable the default renderer when this component is disabled
            if (_meshRenderer != null) {
                _meshRenderer.enabled = true;
            }
        }
    }
}