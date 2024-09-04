using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ModelViewer.Core;
using ModelViewer.Clustering;

namespace ModelViewer.UI
{
    public class ClusteringToggle : MonoBehaviour
    {
        [SerializeField] private TMP_InputField clusterCountInput;
        [SerializeField] private GameObject clusteringUIGroup;
        [SerializeField] private ClusteringController clusteringController;

        private int maxClusterCount = 1;
        private ClusteringAlgorithm selectedAlgorithm = ClusteringAlgorithm.SphericalKMeans;

        private void Start()
        {
            clusterCountInput.onValidateInput += ValidateInput;
            SetClusteringUIActive(false);
        }

        private char ValidateInput(string text, int charIndex, char addedChar)
        {
            return char.IsDigit(addedChar) ? addedChar : '\0';
        }

        public void ApplyClustering()
        {
            if (int.TryParse(clusterCountInput.text, out int clusterCount) && 
                clusterCount >= 1 && clusterCount <= maxClusterCount)
            {
                clusteringController.ExecuteClustering(clusterCount, selectedAlgorithm);
            }
        }

        public void SetClusteringUIActive(bool active)
        {
            clusteringUIGroup.SetActive(active);
        }

        public void SetMaxClusterCount(int count)
        {
            maxClusterCount = count;
        }

        public void SetSelectedAlgorithm(ClusteringAlgorithm algorithm)
        {
            selectedAlgorithm = algorithm;
        }
    }
}