// --- START OF FILE ObjectMoverTool.cs ---
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Player-side tool for entering "delete mode" and deleting owned objects.
/// </summary>
public class ObjectMoverTool : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The camera used for raycasting mouse position.")]
    [SerializeField] private Camera mainCamera;

    [Header("UI Feedback")]
    [Tooltip("Assign a TextMeshPro component to display status (e.g., 'Deleting...', 'Not Owner').")]
    [SerializeField] private TMP_Text statusMessageText;
    [SerializeField] private float messageDuration = 2.0f;

    [Header("Delete Mode Settings")]
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private Texture2D deleteCursorIcon;

    private bool _isDeleteModeActive = false;
    private readonly Vector2 _cursorHotspot = Vector2.zero;

    private float _nextInputTime = 0f;
    private const float INPUT_DELAY = 0.2f;

    private Coroutine _messageCoroutine;
    private bool _isProcessingDelete = false; // Lock input while waiting for server

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (statusMessageText != null) statusMessageText.text = "";
    }

    void Update()
    {
        if (!_isDeleteModeActive) return;

        // --- CURSOR FIX ---
        // Force the cursor every frame to prevent Editor/Browser resets
        Cursor.SetCursor(deleteCursorIcon, _cursorHotspot, CursorMode.Auto);
        // ------------------

        // Debug visual
        if (Application.isEditor)
        {
            Vector3 worldPos = GetMouseWorldPosition();
            Debug.DrawLine(worldPos + Vector3.left * 0.5f, worldPos + Vector3.right * 0.5f, Color.red);
            Debug.DrawLine(worldPos + Vector3.up * 0.5f, worldPos + Vector3.down * 0.5f, Color.red);
        }

        // 1. Logic Checks
        if (Time.time < _nextInputTime) return;
        if (_isProcessingDelete) return; // Wait for previous delete to finish
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Left-click to attempt deletion
        if (Input.GetMouseButtonDown(0))
        {
            TryDeleteAtMouse();
        }

        // Right-click to cancel mode
        if (Input.GetMouseButtonDown(1))
        {
            SetDeleteMode(false);
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        worldPos.z = 0f;
        return worldPos;
    }

    public void TryDeleteAtMouse()
    {
        Vector3 clickPos = GetMouseWorldPosition();
        RaycastHit2D hit = Physics2D.Raycast(clickPos, Vector2.zero, 0f, interactableMask);

        if (hit.collider != null)
        {
            var ni = hit.collider.GetComponentInParent<NetworkedInteractable>();

            if (ni != null)
            {
                if (ni.IsOwnedByLocalPlayer())
                {
                    // OWNER FLOW: Start Async Delete
                    StartCoroutine(HandleAsyncDelete(ni));
                }
                else
                {
                    // NOT OWNER FLOW
                    ShowFeedback("Cannot delete: Not Owner", Color.red);
                }
            }
        }
    }

    private IEnumerator HandleAsyncDelete(NetworkedInteractable ni)
    {
        _isProcessingDelete = true;
        ShowFeedback("Deleting...", Color.yellow);

        bool deleteFinished = false;
        bool deleteSuccess = false;

        // Call the async method
        ni.RequestDestroy((success) =>
        {
            deleteSuccess = success;
            deleteFinished = true;
        });

        // Wait until callback fires
        while (!deleteFinished)
        {
            yield return null;
        }

        if (deleteSuccess)
        {
            ShowFeedback("Deleted", Color.green);
        }
        else
        {
            ShowFeedback("Can't delete: You're not the owner", Color.red);
        }

        _isProcessingDelete = false;
    }

    private void ShowFeedback(string message, Color color)
    {
        if (statusMessageText == null) return;

        statusMessageText.color = color;
        statusMessageText.text = message;
        statusMessageText.gameObject.SetActive(true);

        if (_messageCoroutine != null) StopCoroutine(_messageCoroutine);
        _messageCoroutine = StartCoroutine(ClearMessageAfterDelay());
    }

    private void ShowFeedback(string message) => ShowFeedback(message, Color.white);

    private IEnumerator ClearMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDuration);
        if (statusMessageText != null)
        {
            statusMessageText.text = "";
            statusMessageText.gameObject.SetActive(false);
        }
    }

    public void SetDeleteMode(bool isEnabled)
    {
        _isDeleteModeActive = isEnabled;

        if (_isDeleteModeActive)
        {
            _nextInputTime = Time.time + INPUT_DELAY;
        }
        else
        {
            // Reset cursor when exiting mode
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        if (statusMessageText != null) statusMessageText.text = "";

        Debug.Log($"[ObjectMoverTool] Delete mode set to: {_isDeleteModeActive}");
        Texture2D cursor = _isDeleteModeActive ? deleteCursorIcon : null;
        Cursor.SetCursor(cursor, _cursorHotspot, CursorMode.Auto);
    }
}
// --- END OF FILE ObjectMoverTool.cs ---