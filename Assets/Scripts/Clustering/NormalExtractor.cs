using UnityEngine;
using System.Collections.Generic;

namespace ModelViewer.Clustering
{
    public class NormalExtractor : MonoBehaviour
    {
        public List<Vector3> ExtractNormals(MeshFilter meshFilter)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("MeshFilter or Mesh is missing.");
                return null;
            }

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            List<Vector3> normals = new List<Vector3>();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v1 = vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 1]];
                Vector3 v3 = vertices[triangles[i + 2]];

                Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
                normals.Add(normal);
            }

            return normals;
        }
    }
}