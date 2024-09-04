using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ModelViewer.UI
{
    public class ClusteringAlgorithmSelector : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown algorithmDropdown;
        [SerializeField] private ClusteringToggle clusteringToggle;

        private void Start()
        {
            if (algorithmDropdown == null)
            {
                Debug.LogError("Algorith Dropdown is not assigned!");
                return;
            }

            if (clusteringToggle == null)
            {
                Debug.LogError("Clustering Toggle is not assigned!");
                return;
            }

            InitializeDropdown();
        }

        private void InitializeDropdown()
        {
            algorithmDropdown.ClearOptions();
            algorithmDropdown.AddOptions(new List<string> {
                "Basic Spherical K-Means",
                "von Mises-Fisher (vMF)",
                "Bingham",
                "Kent"
            });

            algorithmDropdown.onValueChanged.AddListener(OnAlgorithmSelected);
        }

        private void OnAlgorithmSelected(int index)
        {
            clusteringToggle.SetSelectedAlgorithm((ClusteringAlgorithm)index);
        }
    }

    public enum ClusteringAlgorithm
    {
        SphericalKMeans,
        VMF,
        Bingham,
        Kent
    }
}