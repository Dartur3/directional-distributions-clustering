using UnityEngine;
using TMPro;

namespace ModelViewer.Utilities
{
    public class ErrorHandler : MonoBehaviour
    {
        private static ErrorHandler _instance;
        private TextMeshProUGUI _errorText;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
        }

        public static void Initialize(TextMeshProUGUI errorText)
        {
            if (_instance == null)
            {
                Debug.LogError("ErrorHandler instance is not created. Make sure to add ErrorHandler to a GameObject in your scene.");
                return;
            }
            _instance._errorText = errorText;
        }

        public static void DisplayError(string message)
        {
            if (_instance == null || _instance._errorText == null)
            {
                Debug.LogError("ErrorHandler is not set up correctly. Error message: " + message);
                return;
            }

            _instance._errorText.text = "Error: " + message;
            Debug.LogError("Model Import Error: " + message);
        }

        public static void ClearError()
        {
            if (_instance == null || _instance._errorText == null)
            {
                return;
            }

            _instance._errorText.text = string.Empty;
        }
    }
}