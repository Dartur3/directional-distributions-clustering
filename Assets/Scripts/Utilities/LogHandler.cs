using UnityEngine;
using System.Text;
using TMPro;
using UnityEngine.UI;
using System.Collections.Concurrent;
using System.Collections;
using System;

namespace ModelViewer.Utilities
{
    public class LogHandler : MonoBehaviour
    {
        private static LogHandler instance;
        private StringBuilder logBuilder;
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private GameObject logPanel;
        [SerializeField] private Button copyButton;

        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                logBuilder = new StringBuilder();
                
                if (logPanel != null)
                {
                    logPanel.SetActive(false);
                }

                if (copyButton != null)
                {
                    copyButton.onClick.AddListener(CopyLogsToClipboard);
                }

                StartCoroutine(ProcessLogQueue());
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void Log(string message)
        {
            if (instance == null) return;
            instance.logQueue.Enqueue(message);
        }

        private IEnumerator ProcessLogQueue()
        {
            while (true)
            {
                while (logQueue.TryDequeue(out string message))
                {
                    logBuilder.AppendLine(message);
                    UpdateLogDisplay();
                }
                yield return null;
            }
        }

        public static void LogClusteringStart(string algorithmName, int pointCount)
        {
            Log($"Starting {algorithmName} clustering with {pointCount} normal vectors.\n");
        }

        public static void LogClusteringComplete(string algorithmName, double totalTime, double initTime, double densityTime, double maximaTime, double clusterTime, double coherentClusterTime, double unassignedTrianglesTime, double clusterParametersTime, double totalSyncTime)
        {
            double sumOfMeasuredTimes = initTime + densityTime + maximaTime + clusterTime + coherentClusterTime + unassignedTrianglesTime + clusterParametersTime + totalSyncTime;
            double difference = totalTime - sumOfMeasuredTimes;
            double relativeThreshold = 0.01; // 1% threshold
            
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine("\nExecution times:");
            logBuilder.AppendLine($"1. Initialization (excl. density & maxima): {initTime:F2} ms ({initTime/totalTime:P2})");
            logBuilder.AppendLine($"2. Density calculation: {densityTime:F2} ms ({densityTime/totalTime:P2})");
            logBuilder.AppendLine($"3. Maxima finding: {maximaTime:F2} ms ({maximaTime/totalTime:P2})");
            logBuilder.AppendLine($"4. Clustering: {clusterTime:F2} ms ({clusterTime/totalTime:P2})");
            logBuilder.AppendLine($"5. Coherent clustering: {coherentClusterTime:F2} ms ({coherentClusterTime/totalTime:P2})");
            logBuilder.AppendLine($"6. Unassigned triangles assignment: {unassignedTrianglesTime:F2} ms ({unassignedTrianglesTime/totalTime:P2})");
            logBuilder.AppendLine($"7. Cluster parameters calculation: {clusterParametersTime:F2} ms ({clusterParametersTime/totalTime:P2})");
            logBuilder.AppendLine($"8. Total synchronization time: {totalSyncTime:F2} ms ({totalSyncTime/totalTime:P2})");
            logBuilder.AppendLine($"9. Total execution time: {totalTime:F2} ms");

            double relativeDiscrepancy = Math.Abs(difference / totalTime);
            if (relativeDiscrepancy > relativeThreshold)
            {
                string discrepancyType = difference > 0 ? "positive" : "negative";
                logBuilder.AppendLine($"\nWarning: Significant {discrepancyType} time measurement discrepancy detected");
                logBuilder.AppendLine($"- Sum of measured times: {sumOfMeasuredTimes:F2} ms");
                logBuilder.AppendLine($"- Difference: {difference:F2} ms ({difference/totalTime:P2})");
                
                if (difference > 0)
                {
                    logBuilder.AppendLine("This positive discrepancy suggests that some operations might not be accounted for in the detailed time measurements.");
                }
                else
                {
                    logBuilder.AppendLine("This negative discrepancy suggests possible overlapping time measurements or overestimation of some operations.");
                }
            }

            Log(logBuilder.ToString());
        }

        public static void LogParameterEstimation(string parameterName, float value)
        {
            Log($"- Estimated {parameterName}: {value:F6}\n");
        }

        public static void Clear()
        {
            if (instance == null) return;
            instance.logBuilder.Clear();
            instance.UpdateLogDisplay();
        }

        private void UpdateLogDisplay()
        {
            if (logText != null)
            {
                logText.text = logBuilder.ToString();
            }
        }

        public void ToggleLogPanel()
        {
            if (logPanel != null)
            {
                logPanel.SetActive(!logPanel.activeSelf);
            }
        }

        public void CopyLogsToClipboard()
        {
            if (logBuilder != null && !string.IsNullOrEmpty(logBuilder.ToString()))
            {
                GUIUtility.systemCopyBuffer = logBuilder.ToString();
                Debug.Log("Logs copied to clipboard");
            }
            else
            {
                Debug.Log("No logs to copy");
            }
        }
    }
}