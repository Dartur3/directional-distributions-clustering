using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ModelViewer.Utilities;
using System.Diagnostics;

namespace ModelViewer.Clustering
{
    public class CoherentClusteringProcessor
    {
        private List<Vector3> normals;
        private List<int> triangles;
        private int[] initialClusterAssignments;
        private List<List<int>> adjacencyList;
        private int[] coherentClusterAssignments;
        private HashSet<int> unassignedTriangles;
        private int numberOfClusters;
        private Dictionary<int, UnassignedTriangleInfo> unassignedTrianglesInfo;
        private PriorityQueue<int, float> borderTriangles;
        private const int SYNC_FREQUENCY = 1; // Synchronizuj co iterację
        private long totalSyncTime = 0;
        private int syncCount = 0;
        private List<int> coherentClusterSizes = new List<int>();
        private List<int> unassignedTrianglesCounts = new List<int>();

        private class UnassignedTriangleInfo
        {
            public HashSet<int> AssignedNeighbors { get; set; }
            public Dictionary<int, float> ClusterDistances { get; set; }
            public int ClosestCluster { get; set; }
            public float ClosestDistance { get; set; }
            public bool IsBorder { get; set; }
        }

        public float TotalSyncTime { get; private set; }
        public float CoherentClusteringTime { get; private set; }
        public float UnassignedTrianglesTime { get; private set; }

        public CoherentClusteringProcessor(List<Vector3> normals, List<int> triangles, int[] initialClusterAssignments)
        {
            this.normals = normals;
            this.triangles = triangles;
            this.initialClusterAssignments = initialClusterAssignments;
            this.numberOfClusters = initialClusterAssignments.Max() + 1;
            BuildAdjacencyList();
        }

        private void BuildAdjacencyList()
        {
            int triangleCount = triangles.Count / 3;
            adjacencyList = new List<List<int>>(triangleCount);
            for (int i = 0; i < triangleCount; i++)
            {
                adjacencyList.Add(new List<int>());
            }

            var edgeToTriangle = new Dictionary<(int, int), List<int>>();

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int triangleIndex = i / 3;
                for (int j = 0; j < 3; j++)
                {
                    int v1 = triangles[i + j];
                    int v2 = triangles[i + (j + 1) % 3];
                    var edge = (Mathf.Min(v1, v2), Mathf.Max(v1, v2));

                    if (!edgeToTriangle.ContainsKey(edge))
                    {
                        edgeToTriangle[edge] = new List<int>();
                    }
                    edgeToTriangle[edge].Add(triangleIndex);
                }
            }

            foreach (var kvp in edgeToTriangle)
            {
                if (kvp.Value.Count == 2)
                {
                    int t1 = kvp.Value[0];
                    int t2 = kvp.Value[1];
                    adjacencyList[t1].Add(t2);
                    adjacencyList[t2].Add(t1);
                }
            }
        }

        public (int[], HashSet<int>) CreateCoherentClusters()
        {
            var totalStopwatch = Stopwatch.StartNew();

            coherentClusterAssignments = new int[initialClusterAssignments.Length];
            Array.Copy(initialClusterAssignments, coherentClusterAssignments, initialClusterAssignments.Length);
            unassignedTriangles = new HashSet<int>();

            LogHandler.Log($"\n-------------------------------------------------------------------------------\n" +
                        $"\nStarting coherent clustering. Original cluster count: {numberOfClusters}\n");

            for (int clusterId = 0; clusterId < numberOfClusters; clusterId++)
            {
                ProcessCluster(clusterId);
            }

            LogInitialCoherentClusteringStatistics();

            var unassignedStopwatch = Stopwatch.StartNew();
            AssignUnassignedTriangles();
            unassignedStopwatch.Stop();
            UnassignedTrianglesTime = (float)unassignedStopwatch.Elapsed.TotalMilliseconds;

            LogFinalCoherentClusteringStatistics();

            totalStopwatch.Stop();
            CoherentClusteringTime = (float)totalStopwatch.Elapsed.TotalMilliseconds - UnassignedTrianglesTime;

            return (coherentClusterAssignments, unassignedTriangles);
        }

        private void ProcessCluster(int clusterId)
        {
            List<int> clusterTriangles = GetTrianglesInCluster(clusterId);
            int originalClusterSize = clusterTriangles.Count;

            if (clusterTriangles.Count == 0)
            {
                coherentClusterSizes.Add(0);
                unassignedTrianglesCounts.Add(0);
                return;
            }

            int startTriangle = FindClosestTriangleToPrototype(clusterTriangles, clusterId);
            var coherentSubcluster = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(startTriangle);

            while (queue.Count > 0)
            {
                int currentTriangle = queue.Dequeue();
                if (!coherentSubcluster.Contains(currentTriangle))
                {
                    coherentSubcluster.Add(currentTriangle);
                    clusterTriangles.Remove(currentTriangle);

                    foreach (int neighbor in adjacencyList[currentTriangle])
                    {
                        if (coherentClusterAssignments[neighbor] == clusterId && !coherentSubcluster.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            foreach (int triangle in clusterTriangles)
            {
                coherentClusterAssignments[triangle] = -1;
                unassignedTriangles.Add(triangle);
            }

            coherentClusterSizes.Add(coherentSubcluster.Count);
            unassignedTrianglesCounts.Add(clusterTriangles.Count);
        }

        private void LogInitialCoherentClusteringStatistics()
        {
            LogHandler.Log("Initial Coherent Clustering Statistics:");
            LogCoherentClusteringStatistics(coherentClusterSizes, unassignedTrianglesCounts);
        }

        private void LogFinalCoherentClusteringStatistics()
        {
            var finalCoherentClusterSizes = new List<int>();
            var finalUnassignedTrianglesCounts = new List<int>();

            for (int i = 0; i < numberOfClusters; i++)
            {
                finalCoherentClusterSizes.Add(coherentClusterAssignments.Count(x => x == i));
            }
            finalUnassignedTrianglesCounts.Add(unassignedTriangles.Count);

            LogHandler.Log("\nFinal Coherent Clustering Statistics (after assigning unassigned triangles):");
            LogCoherentClusteringStatistics(finalCoherentClusterSizes, finalUnassignedTrianglesCounts);
        }

        private void LogCoherentClusteringStatistics(List<int> clusterSizes, List<int> unassignedCounts)
        {
            int sumCoherent = clusterSizes.Sum();
            int sumUnassigned = unassignedCounts.Sum();

            LogHandler.Log($"\n- Average coherent cluster size: {clusterSizes.Average():F2}" +
                           $"\n- Median coherent cluster size: {MathUtilities.GetMedian(clusterSizes):F2}" +
                           $"\n- Min coherent cluster size: {clusterSizes.Min()}" +
                           $"\n- Max coherent cluster size: {clusterSizes.Max()}" +
                           $"\n- Sum of coherent triangles: {sumCoherent}\n" +
                           $"\n- Average unassigned triangles size: {unassignedCounts.Average():F2}" +
                           $"\n- Median unassigned triangles size: {MathUtilities.GetMedian(unassignedCounts):F2}" +
                           $"\n- Min unassigned triangles size: {unassignedCounts.Min()}" +
                           $"\n- Max unassigned triangles size: {unassignedCounts.Max()}" +
                           $"\n- Sum of unassigned triangles: {sumUnassigned}");
        }

        private int FindClosestTriangleToPrototype(List<int> clusterTriangles, int clusterId)
        {
            Vector3 clusterPrototype = CalculateClusterPrototype(clusterId);
            return clusterTriangles.OrderBy(t => Vector3.Distance(normals[t], clusterPrototype)).First();
        }

        private Vector3 CalculateClusterPrototype(int clusterId)
        {
            var clusterNormals = initialClusterAssignments
                .Select((c, i) => new { Cluster = c, Normal = normals[i] })
                .Where(x => x.Cluster == clusterId)
                .Select(x => x.Normal);
            return MathUtilities.CalculateDirectionalMean(clusterNormals.ToList());
        }

        private void AssignUnassignedTriangles()
        {
            InitializeUnassignedTrianglesInfo();
            int maxIterations = 1000000; // Zwiększona maksymalna liczba iteracji
            int iterationCount = 0;
            int totalAssigned = 0;

            while (unassignedTrianglesInfo.Count > 0 && iterationCount < maxIterations)
            {
                if (borderTriangles.TryDequeue(out int triangle, out float _))
                {
                    if (unassignedTrianglesInfo.ContainsKey(triangle))
                    {
                        AssignTriangleToCluster(triangle, unassignedTrianglesInfo[triangle].ClosestCluster);
                        UpdateNeighbors(triangle);
                        totalAssigned++;

                        if (iterationCount % SYNC_FREQUENCY == 0)
                        {
                            SynchronizeContainers();
                        }
                    }
                }
                else
                {
                    break; // Jeśli nie ma więcej trójkątów do przetworzenia, zakończ pętlę
                }

                iterationCount++;
                if (iterationCount % 10000 == 0)
                {
                    LogHandler.Log($"Iteration {iterationCount}: Total assigned {totalAssigned}");
                }
            }

            LogAssignmentResults(iterationCount, totalAssigned);
        }

        private void InitializeUnassignedTrianglesInfo()
        {
            unassignedTrianglesInfo = new Dictionary<int, UnassignedTriangleInfo>();
            borderTriangles = new PriorityQueue<int, float>();

            foreach (int triangle in unassignedTriangles)
            {
                var info = new UnassignedTriangleInfo
                {
                    AssignedNeighbors = new HashSet<int>(),
                    ClusterDistances = new Dictionary<int, float>(),
                    IsBorder = false
                };

                foreach (int neighbor in adjacencyList[triangle])
                {
                    if (!unassignedTriangles.Contains(neighbor))
                    {
                        info.AssignedNeighbors.Add(neighbor);
                        int neighborCluster = coherentClusterAssignments[neighbor];
                        float distance = CalculateAngularDistance(triangle, neighborCluster);
                        info.ClusterDistances[neighborCluster] = distance;
                    }
                }

                if (info.ClusterDistances.Any())
                {
                    var closestCluster = info.ClusterDistances.OrderBy(kvp => kvp.Value).First();
                    info.ClosestCluster = closestCluster.Key;
                    info.ClosestDistance = closestCluster.Value;
                    info.IsBorder = true;
                    borderTriangles.Enqueue(triangle, info.ClosestDistance);
                }

                unassignedTrianglesInfo[triangle] = info;
            }
        }

        private float CalculateAngularDistance(int triangle, int clusterId)
        {
            Vector3 normal = normals[triangle];
            Vector3 centroid = CalculateClusterCentroid(clusterId);
            return Vector3.Angle(normal, centroid);
        }

        private Vector3 CalculateClusterCentroid(int clusterId)
        {
            var clusterNormals = coherentClusterAssignments
                .Select((c, i) => new { Cluster = c, Normal = normals[i] })
                .Where(x => x.Cluster == clusterId)
                .Select(x => x.Normal);
            return MathUtilities.CalculateDirectionalMean(clusterNormals.ToList());
        }

        private void AssignTriangleToCluster(int triangle, int cluster)
        {
            coherentClusterAssignments[triangle] = cluster;
            unassignedTriangles.Remove(triangle);
            unassignedTrianglesInfo.Remove(triangle);
            RemoveFromBorderTriangles(triangle);
        }

        private void UpdateNeighbors(int assignedTriangle)
        {
            foreach (int neighbor in adjacencyList[assignedTriangle])
            {
                if (coherentClusterAssignments[neighbor] != -1)
                {
                    continue;
                }

                if (unassignedTrianglesInfo.TryGetValue(neighbor, out var info))
                {
                    info.AssignedNeighbors.Add(assignedTriangle);
                    int newCluster = coherentClusterAssignments[assignedTriangle];
                    float distance = CalculateAngularDistance(neighbor, newCluster);
                    info.ClusterDistances[newCluster] = distance;

                    var closestCluster = info.ClusterDistances.OrderBy(kvp => kvp.Value).First();
                    info.ClosestCluster = closestCluster.Key;
                    info.ClosestDistance = closestCluster.Value;

                    RemoveFromBorderTriangles(neighbor);
                    borderTriangles.Enqueue(neighbor, info.ClosestDistance);

                    info.IsBorder = true;
                }
            }
        }

        private void SynchronizeContainers()
        {
            var stopwatch = Stopwatch.StartNew();
            
            var newBorderTriangles = new PriorityQueue<int, float>();
            foreach (var kvp in unassignedTrianglesInfo)
            {
                if (kvp.Value.IsBorder)
                {
                    newBorderTriangles.Enqueue(kvp.Key, kvp.Value.ClosestDistance);
                }
            }
            borderTriangles = newBorderTriangles;

            stopwatch.Stop();
            totalSyncTime += stopwatch.ElapsedMilliseconds;
            syncCount++;
        }

        private void RemoveFromBorderTriangles(int triangle)
        {
            var newBorderTriangles = new PriorityQueue<int, float>();
            while (borderTriangles.TryDequeue(out int t, out float priority))
            {
                if (t != triangle)
                {
                    newBorderTriangles.Enqueue(t, priority);
                }
            }
            borderTriangles = newBorderTriangles;
        }

        private void LogAssignmentResults(int iterationCount, int totalAssigned)
        {
            LogHandler.Log($"\nCoherent clustering completed:" +
                           $"\n- Total assigning iterations: {iterationCount}" +
                           $"\n- Total assigned: {totalAssigned}" +
                           $"\n- Remaining unassigned: {unassignedTriangles.Count}");

            if (unassignedTriangles.Count > 0)
            {
                LogHandler.Log($"Warning: {unassignedTriangles.Count} triangles remain unassigned. Further analysis may be required.");
                AnalyzeUnassignedTriangles();
            }

            TotalSyncTime = totalSyncTime;
        }

        private void AnalyzeUnassignedTriangles()
        {
            // Implementacja analizy nieprzypisanych trójkątów
            // Ta metoda powinna być zaimplementowana, jeśli chcemy przeprowadzić szczegółową analizę pozostałych nieprzypisanych trójkątów
        }

        private List<int> GetTrianglesInCluster(int clusterId)
        {
            return Enumerable.Range(0, initialClusterAssignments.Length)
                .Where(i => initialClusterAssignments[i] == clusterId)
                .ToList();
        }

        public int GetCoherentClusterCount()
        {
            return numberOfClusters;
        }
    }
}