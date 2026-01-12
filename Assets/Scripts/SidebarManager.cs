using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class SidebarManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Serializable]
    public class PanelEntry
    {
        public string id = ""; // helpful label
        [Tooltip("Either assign a Button (recommended) or a Toggle to act as the control.")]
        public Button button;
        public Toggle toggle;

        [Tooltip("RectTransform of the panel container. The 'open' anchoredPosition should be where it rests when visible.")]
        public RectTransform panel;

        [Tooltip("If set true this panel is 'exclusive' — used only if manager.exclusiveMode = true (can be used by some panels only).")]
        public bool exclusive = true;

        [Header("Behavior (panel-level)")]
        public bool startOpen = false;
        [Tooltip("Amount of panel visible when 'peek' is active (in pixels).")]
        public float peekPixels = 20f;

        [HideInInspector] public bool isOpen = false;
        [HideInInspector] public Vector2 openAnchoredPos;
        [HideInInspector] public Vector2 closedAnchoredPos;
        [HideInInspector] public Coroutine runningCoroutine = null;
    }

    public enum Side { Left, Right }
    public enum AnimationMode { Ease, Spring }

    [Header("Panels")]
    public List<PanelEntry> panels = new List<PanelEntry>();

    [Header("Layout")]
    public Side side = Side.Left;
    [Tooltip("If true only one panel can be open at a time.")]
    public bool exclusiveMode = true;

    [Header("Animation")]
    [Tooltip("Ease uses AnimationCurve; Spring uses a critically-damped spring (velocity-based).")]
    public AnimationMode animationMode = AnimationMode.Spring;
    [Tooltip("Duration used by Ease mode.")]
    public float animationDuration = 0.28f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Spring damping (higher = snappier).")]
    public float springDamping = 14f;
    [Tooltip("Spring mass-like factor (smaller = stiffer).")]
    public float springStiffness = 1f;
    [Tooltip("Minimal threshold (px) to snap to target.")]
    public float snapThreshold = 0.5f;

    [Header("Input")]
    [Tooltip("Allow close all on Escape key")]
    public bool closeOnEscape = true;
    [Tooltip("Enable keyboard navigation (Tab, Enter/Space)")]
    public bool keyboardNavigation = true;
    [Tooltip("Enable touch swipe gestures (open/close)")]
    public bool enableTouchSwipe = true;
    [Tooltip("Minimum normalized swipe distance (0..1 of screen width) to trigger a swipe.")]
    [Range(0.01f, 0.5f)]
    public float swipeThresholdNormalized = 0.08f;
    [Tooltip("Swipe must be faster than this (units/screenWidth) to count.")]
    public float swipeSpeedThreshold = 0.4f;

    [Header("Peek & Hover")]
    [Tooltip("Enable peek-on-hover on desktop (disabled when touch present).")]
    public bool enablePeekOnHover = true;
    [Tooltip("Peek is disabled for screens narrower than this pixel width (useful for mobile). Set 0 to always allow).")]
    public int peekMinScreenWidth = 450;

    [Header("Button Visuals")]
    [Tooltip("If using plain Buttons (not toggles), we will tint the Button Image when active.")]
    public Color buttonUntoggledColor = Color.white;
    public Color buttonToggledColor = new Color(0.85f, 0.92f, 1f, 1f);

    [Header("Events")]
    public UnityEvent<string> onPanelOpened;   // passes panel id
    public UnityEvent<string> onPanelClosed;   // passes panel id

    // internal state
    Canvas _rootCanvas;
    bool _pointerOver = false;
    int _focusedControlIndex = -1;

    void Awake()
    {
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
        {
            Debug.LogWarning("[SidebarManager] No Canvas found in parents. Responsiveness depends on CanvasScaler.");
        }

        // Initialize panels
        for (int i = 0; i < panels.Count; i++)
        {
            SetupPanel(i, init: true);
        }
    }

    void OnEnable()
    {
        // recalc positions (in case layout changed)
        RecalculateAllPanelPositions();
    }

    void Update()
    {
        HandleKeyboard();
        HandleTouchSwipe();
    }

    #region Setup & Recalculation
    void SetupPanel(int index, bool init)
    {
        if (!ValidIndex(index)) return;
        var e = panels[index];
        if (e.panel == null)
        {
            Debug.LogWarning($"[SidebarManager] Panel {index} has no RectTransform assigned.");
            return;
        }

        // Determine open anchored position from current RectTransform (designer sets it visually in scene)
        e.openAnchoredPos = e.panel.anchoredPosition;

        // compute closed pos based on panel width and side and canvas scale/rect
        float width = Mathf.Abs(GetWorldWidth(e.panel));
        Vector2 closed = e.openAnchoredPos;
        float offset = width + e.peekPixels;

        if (side == Side.Left)
            closed.x = e.openAnchoredPos.x - offset;
        else
            closed.x = e.openAnchoredPos.x + offset;

        e.closedAnchoredPos = closed;

        // initial state
        e.isOpen = e.startOpen;
        e.panel.anchoredPosition = e.isOpen ? e.openAnchoredPos : e.closedAnchoredPos;

        // wire inputs
        WireControl(index);

        if (init)
        {
            ApplyButtonVisual(e, e.isOpen);
        }
    }

    /// <summary>
    /// Recalculate positions for all panels (call after layout changes or screen resize).
    /// Keeps current open/closed state but recomputes anchors.
    /// </summary>
    public void RecalculateAllPanelPositions()
    {
        for (int i = 0; i < panels.Count; i++)
        {
            // re-evaluate open pos: keep current anchoredPosition as open pos if it's currently open;
            // otherwise keep design-time openAnchoredPos but recompute closed pos from width
            var e = panels[i];
            if (e.panel == null) continue;

            // prefer the inspector-specified "open" anchored position: assume this is the desired open position
            // if panel is currently open, use its current anchoredPosition as openAnchoredPos
            if (e.isOpen)
                e.openAnchoredPos = e.panel.anchoredPosition;

            float width = Mathf.Abs(GetWorldWidth(e.panel));
            float offset = width + e.peekPixels;
            Vector2 closed = e.openAnchoredPos;
            closed.x = (side == Side.Left) ? e.openAnchoredPos.x - offset : e.openAnchoredPos.x + offset;
            e.closedAnchoredPos = closed;

            // snap panel to correct position immediately to avoid jump
            e.panel.anchoredPosition = e.isOpen ? e.openAnchoredPos : e.closedAnchoredPos;
        }
    }

    float GetWorldWidth(RectTransform rt)
    {
        // Convert rect width (local) to scaled width using lossyScale or parent scale from Canvas
        // We return width in anchoredPosition-space units (which is local pixels in Canvas)
        if (rt == null) return 0f;
        return rt.rect.width;
    }

    void WireControl(int index)
    {
        if (!ValidIndex(index)) return;
        var e = panels[index];

        // Clear old listeners safely
        if (e.button != null)
        {
            e.button.onClick.RemoveAllListeners();
            e.button.onClick.AddListener(() => TogglePanel(index));
            // ensure the button has an Image if we want to tint it
        }
        if (e.toggle != null)
        {
            e.toggle.onValueChanged.RemoveAllListeners();
            e.toggle.isOn = e.isOpen;
            e.toggle.onValueChanged.AddListener((val) =>
            {
                if (val) OpenPanel(index);
                else ClosePanel(index);
            });
        }
    }
    #endregion

    #region Public API
    public void TogglePanel(int index)
    {
        if (!ValidIndex(index)) return;
        if (panels[index].isOpen) ClosePanel(index);
        else OpenPanel(index);
    }

    public void OpenPanel(int index)
    {
        if (!ValidIndex(index)) return;
        var e = panels[index];

        // Exclusive logic
        if (exclusiveMode)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (i == index) continue;
                if (panels[i].isOpen)
                {
                    panels[i].isOpen = false;
                    StartPanelAnimation(i, false);
                    UpdateControlVisuals(i, false);
                }
            }
        }

        if (!e.isOpen)
        {
            e.isOpen = true;
            StartPanelAnimation(index, true);
            UpdateControlVisuals(index, true);
            if (e.toggle != null) e.toggle.isOn = true;
            onPanelOpened?.Invoke(e.id);
        }
    }

    public void ClosePanel(int index)
    {
        if (!ValidIndex(index)) return;
        var e = panels[index];
        if (e.isOpen)
        {
            e.isOpen = false;
            StartPanelAnimation(index, false);
            UpdateControlVisuals(index, false);
            if (e.toggle != null) e.toggle.isOn = false;
            onPanelClosed?.Invoke(e.id);
        }
    }

    public void CloseAll()
    {
        for (int i = 0; i < panels.Count; i++)
        {
            ClosePanel(i);
        }
    }
    #endregion

    #region Animation
    void StartPanelAnimation(int index, bool open)
    {
        if (!ValidIndex(index)) return;
        var e = panels[index];

        // stop previous
        if (e.runningCoroutine != null) StopCoroutine(e.runningCoroutine);

        e.runningCoroutine = StartCoroutine(AnimatePanelCoroutine(e, open));
    }

    IEnumerator AnimatePanelCoroutine(PanelEntry entry, bool open)
    {
        if (entry.panel == null) yield break;

        Vector2 from = entry.panel.anchoredPosition;
        Vector2 to = open ? entry.openAnchoredPos : entry.closedAnchoredPos;

        // snap if already close
        if (Vector2.SqrMagnitude(from - to) < snapThreshold * snapThreshold)
        {
            entry.panel.anchoredPosition = to;
            entry.runningCoroutine = null;
            yield break;
        }

        if (animationMode == AnimationMode.Ease)
        {
            float elapsed = 0f;
            float dur = Mathf.Max(0.0001f, animationDuration);
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float eased = easeCurve.Evaluate(t);
                entry.panel.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                yield return null;
            }
            entry.panel.anchoredPosition = to;
            entry.runningCoroutine = null;
            yield break;
        }
        else // Spring
        {
            // Implemented as critically-damped spring using SmoothDamp-like behaviour for Vector2
            Vector2 velocity = Vector2.zero;
            float dt;
            // We treat springStiffness and springDamping as tunables for response speed
            float omega = springStiffness * 10f; // scale to make sensible by default
            float damping = springDamping;

            for (int iter = 0; iter < 10000; iter++)
            {
                dt = Time.unscaledDeltaTime;
                // simple damped spring integrator (semi-implicit Euler)
                // acceleration = -omega^2 * (x - target) - 2 * damping * omega * v
                Vector2 diff = entry.panel.anchoredPosition - to;
                Vector2 accel = (-omega * omega) * diff - (2f * damping * omega) * velocity;
                velocity += accel * dt;
                entry.panel.anchoredPosition += velocity * dt;

                if ((entry.panel.anchoredPosition - to).sqrMagnitude < snapThreshold * snapThreshold && velocity.sqrMagnitude < 0.01f)
                {
                    entry.panel.anchoredPosition = to;
                    break;
                }
                yield return null;
            }
            entry.runningCoroutine = null;
            yield break;
        }
    }
    #endregion

    #region Input Handling
    void HandleKeyboard()
    {
        if (!keyboardNavigation) return;

        // Escape closes all
        if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseAll();
        }

        // Tab focus handling: cycle through buttons/toggles
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            CycleFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
        }

        // Activate focused button with Enter or Space
        if (_focusedControlIndex >= 0 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
        {
            TogglePanel(_focusedControlIndex);
        }
    }

    void CycleFocus(int dir)
    {
        // gather controls that exist
        List<int> controls = new List<int>();
        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i].button != null || panels[i].toggle != null) controls.Add(i);
        }
        if (controls.Count == 0) return;

        int idx = controls.IndexOf(_focusedControlIndex);
        if (idx < 0) idx = (dir > 0) ? -1 : 0;
        idx = (idx + dir + controls.Count) % controls.Count;
        _focusedControlIndex = controls[idx];

        // move Unity's EventSystem selection for visual focus (if available)
        var es = EventSystem.current;
        if (es != null)
        {
            Selectable s = panels[_focusedControlIndex].button as Selectable ?? (Selectable)panels[_focusedControlIndex].toggle;
            if (s != null) s.Select();
        }
    }

    // Simple single-finger swipe detection for panels.
    Vector2 _swipeStartPos;
    float _swipeStartTime;
    bool _maybeSwiping = false;

    void HandleTouchSwipe()
    {
        if (!enableTouchSwipe) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        // allow mouse drag as a swipe in editor/standalone to test
        if (Input.GetMouseButtonDown(0))
        {
            _maybeSwiping = true;
            _swipeStartPos = Input.mousePosition;
            _swipeStartTime = Time.unscaledTime;
        }
        else if (Input.GetMouseButtonUp(0) && _maybeSwiping)
        {
            Vector2 delta = (Vector2)Input.mousePosition - _swipeStartPos;
            float duration = Time.unscaledTime - _swipeStartTime;
            EvaluateSwipe(delta, duration);
            _maybeSwiping = false;
        }
#endif

        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                _maybeSwiping = true;
                _swipeStartPos = t.position;
                _swipeStartTime = Time.unscaledTime;
            }
            else if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) && _maybeSwiping)
            {
                Vector2 delta = t.position - _swipeStartPos;
                float duration = Time.unscaledTime - _swipeStartTime;
                EvaluateSwipe(delta, duration);
                _maybeSwiping = false;
            }
        }
    }

    void EvaluateSwipe(Vector2 deltaPixels, float duration)
    {
        if (duration <= 0f) return;
        float screenW = (float)Screen.width;
        float deltaX = deltaPixels.x;
        float norm = Mathf.Abs(deltaX) / screenW;
        float speed = Mathf.Abs(deltaX) / screenW / duration; // normalized speed

        if (norm < swipeThresholdNormalized) return;
        if (speed < swipeSpeedThreshold) return;

        // Determine direction
        bool leftSwipe = deltaX < 0f;
        // For side Left: left swipe = close, right swipe = open
        // For side Right: left swipe = open, right swipe = close
        if (side == Side.Left)
        {
            if (leftSwipe) CloseAll(); else OpenFirstAvailable();
        }
        else
        {
            if (leftSwipe) OpenFirstAvailable(); else CloseAll();
        }
    }

    void OpenFirstAvailable()
    {
        // prefer first non-exclusive? open first panel found
        for (int i = 0; i < panels.Count; i++)
        {
            OpenPanel(i);
            break;
        }
    }
    #endregion

    #region Hover Peek
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        _pointerOver = true;
        if (ShouldAllowPeek()) StartPeek();
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        _pointerOver = false;
        if (ShouldAllowPeek()) EndPeek();
    }

    bool ShouldAllowPeek()
    {
        if (!enablePeekOnHover) return false;
        if (Input.touchSupported) return false;
        if (peekMinScreenWidth > 0 && Screen.width < peekMinScreenWidth) return false;
        return true;
    }

    void StartPeek()
    {
        for (int i = 0; i < panels.Count; i++)
        {
            var e = panels[i];
            if (!e.isOpen)
            {
                // show a partial peek by moving to openPos - peekPixels
                Vector2 target = e.closedAnchoredPos;
                // compute slight move toward open
                Vector2 dir = (e.openAnchoredPos - e.closedAnchoredPos).normalized;
                target = e.closedAnchoredPos + dir * e.peekPixels;
                // stop running coroutine
                if (e.runningCoroutine != null) StopCoroutine(e.runningCoroutine);
                e.runningCoroutine = StartCoroutine(QuickMoveTo(e, target));
            }
        }
    }

    void EndPeek()
    {
        for (int i = 0; i < panels.Count; i++)
        {
            var e = panels[i];
            if (!e.isOpen)
            {
                if (e.runningCoroutine != null) StopCoroutine(e.runningCoroutine);
                e.runningCoroutine = StartCoroutine(QuickMoveTo(e, e.closedAnchoredPos));
            }
        }
    }

    IEnumerator QuickMoveTo(PanelEntry entry, Vector2 target)
    {
        Vector2 from = entry.panel.anchoredPosition;
        float dur = 0.12f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            entry.panel.anchoredPosition = Vector2.LerpUnclamped(from, target, t);
            yield return null;
        }
        entry.panel.anchoredPosition = target;
        entry.runningCoroutine = null;
    }
    #endregion

    #region Visuals & Utilities
    void UpdateControlVisuals(int index, bool on)
    {
        var e = panels[index];
        ApplyButtonVisual(e, on);
    }

    void ApplyButtonVisual(PanelEntry e, bool on)
    {
        if (e.toggle != null) return; // toggle handles itself
        if (e.button == null) return;
        var img = e.button.GetComponent<Image>();
        if (img != null)
        {
            img.color = on ? buttonToggledColor : buttonUntoggledColor;
        }
    }

    bool ValidIndex(int idx) => idx >= 0 && idx < panels.Count;
    #endregion

    #region Editor & Utility Helpers
    // Optionally expose a public inspector button to recalc positions:
    [ContextMenu("Recalculate Panel Positions")]
    public void RecalculatePositionsContext()
    {
        RecalculateAllPanelPositions();
    }

    // IPointerEnter/Exit implemented above for peek
    #endregion
}