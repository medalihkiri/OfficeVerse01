using NUnit.Framework;
using System.Collections;
using System.Xml.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraDrag : MonoBehaviour
{
    [Header("Camera Dragging")]
    private Vector3 dragOrigin;
    public bool isDragging;
    public bool endDrag = false;

    [Header("Bounds")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -5f;
    public float maxY = 5f;

    [Header("Cursor Settings")]
    public Texture2D grabCursor;

    [Header("Smoothing")]
    public float smoothSpeed = 13f;
    public float smoothAutoSpeed = 13f;
    private Vector3 targetPosition;

    public RectTransform rightPanel;

    void Update()
    {
        HandleInput();
        UpdatePosition();
    }

    void UpdatePosition()
    {
        if (Camera.main.orthographicSize <= 6.5)
        {
            if (rp)
            {
                maxX = MapValue(Camera.main.orthographicSize, 8f, 3.35f);
            }
            else
            {
                maxX = MapValue(Camera.main.orthographicSize, 6.5f, -1.5f);
            }

            minX = MapValue(Camera.main.orthographicSize, -7.48f, -1.5f);
            maxY = MapValue(Camera.main.orthographicSize, 21f, 16.6f);
            minY = MapValue(Camera.main.orthographicSize, -6.1f, -2.5f);
        }
        else
        {
            maxX = -1.5f;
            minX = -1.5f;
            maxY = 16.6f;
            minY = -2.5f;
        }
    }

    bool p = false;
    bool c = false;
    bool fromDrag = false;

    bool rp = false;

    public void PartCamera()
    {
        if(p == false)
        {
            rp = true;
            //UpdatePosition();
            if (transform.position.x > (maxX - 0.2)) 
            {
                StartCoroutine(SmoothMove(MapValue(Camera.main.orthographicSize, 8f, 3.35f))); 
            }
        }
        else { BackCamera(); }

        p = !p;
    }

    public void ChatCamera()
    {
        if(c == false)
        {
            rp = true;
            //UpdatePosition();
            if (transform.position.x > (maxX - 0.2)) { StartCoroutine(SmoothMove(MapValue(Camera.main.orthographicSize, 8f, 3.35f))); }
        }
        else { BackCamera(); }

        c = !c;
    }

    public void BackCamera()
    {
        rp = false;
        UpdatePosition();
        if (transform.position.x > maxX) { StartCoroutine(SmoothMove(MapValue(Camera.main.orthographicSize, 6.5f, -1.5f))); }
    }

    float MapValue(float input, float min, float max)
    {
        return Mathf.Lerp(min, max, (input - 2) / (6.5f - 2));
    }



    IEnumerator SmoothMove(float targetX)
    {
        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            transform.position = new Vector3(
                Mathf.Lerp(transform.position.x, targetX, Time.deltaTime * smoothAutoSpeed),
                transform.position.y,
                transform.position.z
            );
            yield return null;
        }

        transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
    }
    IEnumerator SmoothMoveY(float targetY)
    {
        while (Mathf.Abs(transform.position.y - targetY) > 0.01f)
        {
            transform.position = new Vector3(
                transform.position.x,
                Mathf.Lerp(transform.position.y, targetY, Time.deltaTime * smoothAutoSpeed),
                transform.position.z
            );
            yield return null;
        }

        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
    }

    bool set = false;
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUIElement())
        {
            StartDragging();
        }

        if (Input.GetMouseButtonUp(0))
        {
            StopDragging();
        }

        if (isDragging)
        {
            DragCamera();
        }
    }

    IEnumerator SetBool()
    {
        yield return new WaitForSeconds(0.2f);
        if (isDragging)
            fromDrag = true;
    }

    private void StartDragging()
    {
        isDragging = true;
        dragOrigin = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        dragOrigin.z = transform.position.z;
        Cursor.SetCursor(grabCursor, Vector2.zero, CursorMode.Auto);

        if (set == false)
            StartCoroutine(SetBool());
    }

    private void StopDragging()
    {
        isDragging = false;
        endDrag = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (fromDrag)
        {
            if (targetPosition.x > maxX)
            {
                if (rp)
                {
                    StartCoroutine(SmoothMove(MapValue(Camera.main.orthographicSize, 8f, 3.35f)));
                }
                else
                {
                    StartCoroutine(SmoothMove(MapValue(Camera.main.orthographicSize, 6.5f, -1.5f)));
                }
            }
            else if (targetPosition.x < minX)
            {
                StartCoroutine(SmoothMove(MapValue(Camera.main.orthographicSize, -7.48f, -1.5f)));
            }

            if (targetPosition.y > maxY)
            {
                StartCoroutine(SmoothMoveY(MapValue(Camera.main.orthographicSize, 21f, 16.6f)));
            }
            else if (targetPosition.y < minY)
            {
                StartCoroutine(SmoothMoveY(MapValue(Camera.main.orthographicSize, -6.1f, -2.5f)));
            }
        }

        fromDrag = false;
        set = false;
    }

    private void DragCamera()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 offset = mousePosition - dragOrigin;

        targetPosition = transform.position - new Vector3(offset.x, offset.y, 0);
        if(minX <= targetPosition.x && targetPosition.x <= maxX && minY <= targetPosition.y && targetPosition.y <= maxY)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
    }


    private bool IsPointerOverUIElement()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
