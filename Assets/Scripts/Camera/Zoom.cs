using UnityEngine;

public class Zoom : MonoBehaviour
{
    private float zoom;
    private float zoomMultiplier = 4f;
    private float minZoom = 2f;
    private float maxZoom = 5.2f;
    private float velocity = 0f;
    private float smoothTime = 0.25f;

    [SerializeField] private Camera cam;

    private void Start()
    {
        // It's good practice to check if the camera is assigned to prevent errors.
        if (cam == null)
        {
            Debug.LogError("Camera has not been assigned in the Inspector!", this.gameObject);
            cam = Camera.main; // Fallback to the main camera.
        }
        zoom = cam.orthographicSize;
    }

    private void Update()
    {
        // -------------------- MODIFICATION START --------------------

        // Step 1: Only check for input and modify our target 'zoom' variable
        // if the 'X' key is NOT being held down.
        if (!Input.GetKey(KeyCode.X))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            zoom -= scroll * zoomMultiplier;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        // Step 2: ALWAYS update the camera's orthographic size every frame.
        // This ensures the camera will smoothly finish its movement to the last
        // 'zoom' target, even if you press 'X' in the middle of a zoom.
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, zoom, ref velocity, smoothTime);

        // -------------------- MODIFICATION END --------------------
    }
}