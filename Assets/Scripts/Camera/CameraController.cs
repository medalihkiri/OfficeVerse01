using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public GameObject player;
    public float minX, maxX, minY, maxY; // Define map boundaries
    public float zoomMultiplier = 4f;
    public float smoothMoveTime = 0.5f;
    public float smoothTime = 0.5f;
    public float minZoom = 2f;
    public float maxZoom = 20f;
    private float zoom;
    private float velocity = 0f;
    private Vector3 cameraVelocity = Vector3.zero;
    private Camera cam;
    float camHeight, camWidth;
    float horizontalOffset = -1f;
    private bool disableZoom = false;
    private bool isZoomingBlocked = false;

    void Start()
    {
        cam = Camera.main; // Ensure this script is attached to the main camera
        zoom = cam.orthographicSize;
    }

    [System.Obsolete]
    void Update()
    {
        if (!disableZoom)
        {
            // Check if cursor is over UI (excluding WorldSpace UI)
            if (IsPointerOverUIElement(out bool isWorldSpace))
            {
                if (!isWorldSpace)
                {
                    isZoomingBlocked = true;
                }
            }
            else
            {
                isZoomingBlocked = false;
            }

            // Zoom control
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            // If we are blocked by UI, don't apply zoom
            if (isZoomingBlocked)
            {
                scroll = 0;
            }

            // Check if the camera is at the upper and lower boundary
            if (cam.transform.position.y >= maxY - camHeight && cam.transform.position.y <= minY + camHeight)
            {
                // Disable zooming out when at the upper boundary
                if (scroll < 0)
                {
                    scroll = 0;
                }
            }

            zoom -= scroll * zoomMultiplier;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, zoom, ref velocity, smoothTime);

            // Ensure player is assigned and camera is available
            if (player == null || cam == null)
                return;

            // Adjust vertical boundaries based on zoom
            camHeight = cam.orthographicSize;
            camWidth = cam.orthographicSize * cam.aspect;
            float minYWithOffset = minY + camHeight;
            float maxYWithOffset = maxY - camHeight;

            // Calculate the target Y position for the camera
            float targetY = Mathf.Clamp(player.transform.position.y, minYWithOffset, maxYWithOffset);

            // Calculate the camera's Y position based on the player's position and boundaries
            float cameraY = targetY;

            // Check if the camera needs to stop at the lower boundary
            if (targetY <= minYWithOffset)
            {
                cameraY = minYWithOffset;
            }

            // Check if the camera needs to stop at the upper boundary
            if (targetY >= maxYWithOffset)
            {
                cameraY = maxYWithOffset;
            }

            // Adjust horizontal boundaries based on zoom
            float minXWithOffset = minX + camWidth;
            float maxXWithOffset = maxX - camWidth;

            // Calculate the target X position for the camera
            float targetX = Mathf.Clamp(player.transform.position.x, minXWithOffset, maxXWithOffset);

            // Calculate the camera's X position based on the player's position and boundaries
            float cameraX = targetX;

            // Check if the camera needs to stop at the left boundary
            if (targetX <= minXWithOffset)
            {
                cameraX = minXWithOffset;
            }

            // Check if the camera needs to stop at the right boundary
            if (targetX >= maxXWithOffset)
            {
                cameraX = maxXWithOffset;
            }

            // If both horizontal boundaries are hit, center the camera horizontally
            if (cameraX <= minXWithOffset && cameraX >= maxXWithOffset)
            {
                cameraX = ((minX + maxX) / 2) + horizontalOffset;
            }

            // Smoothly move the camera to the target position
            Vector3 targetPosition = new Vector3(cameraX, cameraY, transform.position.z);
            if (FindObjectOfType<CameraDrag>().endDrag == false)
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref cameraVelocity, smoothMoveTime);
                this.GetComponent<CameraDrag>().isDragging = false;
                this.GetComponent<CameraDrag>().endDrag = true;

                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
    }

    private void OnEnable()
        {
            EmojiChatButtonController.OnEmojiWindowStateChanged += OnEmojiWindowStateChanged;
        }

        private void OnDisable()
        {
            EmojiChatButtonController.OnEmojiWindowStateChanged -= OnEmojiWindowStateChanged;
        }


        private void OnEmojiWindowStateChanged(bool isOpen)
        {
            disableZoom = isOpen;
        }

        private bool IsPointerOverUIElement(out bool isWorldSpace)
        {
            isWorldSpace = false;
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            if (results.Count > 0)
            {
                foreach (RaycastResult result in results)
                {
                    // Check if the UI element is in World Space
                    Canvas canvas = result.gameObject.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                    {
                        isWorldSpace = true;
                        return true;
                    }
                    else if (canvas != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
} 
