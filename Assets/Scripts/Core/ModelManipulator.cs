using UnityEngine;
using UnityEngine.EventSystems;

namespace ModelViewer.Core
{
    public class ModelManipulator : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 1000f;
        [SerializeField] private float maxZoomSpeed = 7f;
        [SerializeField] private float zoomSmoothTime = 0.1f;
        [SerializeField] private float minZoomDistance = 1f;
        [SerializeField] private float maxZoomDistance = 10f;
        [SerializeField] private float translationSensitivity = 1f;

        private const float ZoomSpeedMinFactor = 0.1f;
        private const float ZoomSpeedMaxFactor = 1f;
        private const float ZoomThreshold = 0.01f;

        private GameObject modelParent;
        private Camera mainCamera;
        private Vector3 initialCameraPosition;
        private float currentZoomVelocity;
        private float targetZoomDistance;
        private Vector3 lastMousePosition;

        private bool isRotating;
        private bool isTranslating;
        private bool isZooming;

        private void Start()
        {
            mainCamera = Camera.main;
            initialCameraPosition = mainCamera.transform.position;
            targetZoomDistance = Vector3.Distance(initialCameraPosition, Vector3.zero);
        }

        private void Update()
        {
            if (modelParent == null) return;

            HandleInteractions();
        }

        private void HandleInteractions()
        {
            HandleRotation();
            HandleTranslation();
            HandleZoom();
        }

        private void HandleRotation()
        {
            if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject() && TryGetRaycastHitOnModel(out _))
            {
                isRotating = true;
                SetCursorState(false);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isRotating = false;
                SetCursorState(true);
            }

            if (isRotating)
            {
                float rotationX = -Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
                float rotationY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;

                modelParent.transform.Rotate(Vector3.up, rotationX, Space.World);
                modelParent.transform.Rotate(Vector3.right, rotationY, Space.World);
            }
        }

        private void HandleTranslation()
        {
            if (Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject() && TryGetRaycastHitOnModel(out _))
            {
                isTranslating = true;
                SetCursorState(false);
                lastMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isTranslating = false;
                SetCursorState(true);
            }

            if (isTranslating)
            {
                Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;

                float distanceToModel = Vector3.Distance(mainCamera.transform.position, modelParent.transform.position);
                float viewportHeight = 2.0f * distanceToModel * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float viewportWidth = viewportHeight * mainCamera.aspect;

                Vector3 translation = new Vector3(
                    mouseDelta.x / Screen.width * viewportWidth,
                    mouseDelta.y / Screen.height * viewportHeight,
                    0
                ) * translationSensitivity;

                modelParent.transform.Translate(mainCamera.transform.right * translation.x + mainCamera.transform.up * translation.y, Space.World);
            }
        }

        private void HandleZoom()
        {
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (scrollInput != 0 && !EventSystem.current.IsPointerOverGameObject())
            {
                isZooming = true;
                float currentDistance = Vector3.Distance(mainCamera.transform.position, modelParent.transform.position);
                float zoomSpeedFactor = Mathf.Lerp(ZoomSpeedMinFactor, ZoomSpeedMaxFactor, currentDistance / maxZoomDistance);
                float zoomSpeed = maxZoomSpeed * zoomSpeedFactor;

                targetZoomDistance -= scrollInput * zoomSpeed;
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance);
            }

            if (isZooming)
            {
                float currentDistance = Vector3.Distance(mainCamera.transform.position, modelParent.transform.position);
                float newZoomDistance = Mathf.SmoothDamp(
                    currentDistance,
                    targetZoomDistance,
                    ref currentZoomVelocity,
                    zoomSmoothTime
                );

                Vector3 zoomDirection = (mainCamera.transform.position - modelParent.transform.position).normalized;
                mainCamera.transform.position = modelParent.transform.position + zoomDirection * newZoomDistance;

                if (Mathf.Abs(currentDistance - targetZoomDistance) < ZoomThreshold)
                {
                    isZooming = false;
                }
            }
        }

        private void SetCursorState(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Confined;
        }

        private bool TryGetRaycastHitOnModel(out RaycastHit hit)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out hit) && IsHitOnModel(hit.collider);
        }

        private bool IsHitOnModel(Collider collider)
        {
            return collider.gameObject.transform == modelParent.transform || 
                   collider.transform.IsChildOf(modelParent.transform);
        }

        public void SetModelParent(GameObject parent)
        {
            modelParent = parent;
            ResetCameraPosition();
        }

        private void ResetCameraPosition()
        {
            mainCamera.transform.position = initialCameraPosition;
            targetZoomDistance = Vector3.Distance(initialCameraPosition, modelParent.transform.position);
        }
    }
}