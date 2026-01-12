using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class EyeFollow : MonoBehaviour
{
    [Header("Eye Transforms (containers)")]
    public RectTransform leftEyeContainer;   // For UI: RectTransform. For world-space, assign the Transform's rect in inspector by using a RectTransform or use a dummy
    public RectTransform rightEyeContainer;

    [Header("Pupils (objects that move inside container)")]
    public RectTransform leftPupil;
    public RectTransform rightPupil;

    [Header("Movement")]
    public float maxPupilDistance = 12f;     // max pixels (UI) or units (world). Tune to your art
    public float smoothTime = 0.06f;         // lower = snappier
    public bool useWorldSpace = false;       // set true if your eyes/pupils are world-space Transforms

    [Header("Optional cosmetics")]
    public bool enableBlink = false;
    public float blinkIntervalMin = 3f;
    public float blinkIntervalMax = 8f;
    public Canvas canvas;                    // needed if using Screen Space - Camera or for RectTransform utilities

    // internal state
    Vector2 leftVelocity;
    Vector2 rightVelocity;

    Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;

        // Safety checks
        if (!leftEyeContainer || !rightEyeContainer || !leftPupil || !rightPupil)
            Debug.LogWarning("EyeFollow: assign all eye and pupil transforms in inspector.");

        if (!canvas && !useWorldSpace)
        {
            // try find a canvas
            canvas = GetComponentInParent<Canvas>();
            if (!canvas) canvas = FindObjectOfType<Canvas>();
        }

        if (enableBlink) StartCoroutine(BlinkRoutine());
    }

    void Update()
    {
        Vector2 pointerScreenPos = GetPointerScreenPosition();

        // For each eye: get pointer in eye-local coords
        UpdatePupil(leftEyeContainer, leftPupil, ref leftVelocity, pointerScreenPos);
        UpdatePupil(rightEyeContainer, rightPupil, ref rightVelocity, pointerScreenPos);
    }

    Vector2 GetPointerScreenPosition()
    {
        // Touch priority on mobile
        if (Input.touchCount > 0)
            return Input.touches[0].position;
        return (Vector2)Input.mousePosition;
    }

    void UpdatePupil(RectTransform eyeContainer, RectTransform pupil, ref Vector2 velocity, Vector2 pointerScreenPos)
    {
        if (!eyeContainer || !pupil) return;

        if (useWorldSpace)
        {
            // If using world-space transforms, convert screen point to world point and then to local point
            Vector3 worldPoint = mainCam.ScreenToWorldPoint(new Vector3(pointerScreenPos.x, pointerScreenPos.y, Mathf.Abs(mainCam.transform.position.z - eyeContainer.position.z)));
            Vector3 localPoint = eyeContainer.InverseTransformPoint(worldPoint);
            Vector2 desired = new Vector2(localPoint.x, localPoint.y);

            // Clamp to max distance
            Vector2 clamped = Vector2.ClampMagnitude(desired, maxPupilDistance);
            Vector2 current = pupil.anchoredPosition; // For world-space you might use localPosition. Use anchoredPosition if RectTransform still used.
            Vector2 next = Vector2.SmoothDamp(current, clamped, ref velocity, smoothTime);
            pupil.anchoredPosition = next;
        }
        else
        {
            // UI / Screen Space approach - convert screen point to local point in the eye container
            Vector2 localPoint;
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                eyeContainer, pointerScreenPos, canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, out localPoint);

            // If conversion fails, just use center
            if (!success) localPoint = Vector2.zero;

            // localPoint is in same units as anchoredPosition; clamp and lerp
            Vector2 clamped = Vector2.ClampMagnitude(localPoint, maxPupilDistance);
            Vector2 current = pupil.anchoredPosition;
            Vector2 next = Vector2.SmoothDamp(current, clamped, ref velocity, smoothTime);
            pupil.anchoredPosition = next;
        }
    }

    IEnumerator BlinkRoutine()
    {
        // Example blink: animate eye containers scale Y to 0 quickly and open
        while (true)
        {
            float wait = Random.Range(blinkIntervalMin, blinkIntervalMax);
            yield return new WaitForSeconds(wait);
            yield return BlinkOnce();
        }
    }

    IEnumerator BlinkOnce()
    {
        float duration = 0.08f;
        float t = 0f;
        // closing
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 0.08f, t / duration);
            SetEyeScaleY(s);
            yield return null;
        }
        // tiny hold
        yield return new WaitForSeconds(0.04f);
        // opening
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0.08f, 1f, t / duration);
            SetEyeScaleY(s);
            yield return null;
        }
        SetEyeScaleY(1f);
    }

    void SetEyeScaleY(float y)
    {
        if (leftEyeContainer) leftEyeContainer.localScale = new Vector3(leftEyeContainer.localScale.x, y, leftEyeContainer.localScale.z);
        if (rightEyeContainer) rightEyeContainer.localScale = new Vector3(rightEyeContainer.localScale.x, y, rightEyeContainer.localScale.z);
    }
}
