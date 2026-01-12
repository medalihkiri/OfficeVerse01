using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Improved whiteboard controller with shape tools:
/// - Pen, Highlighter, Eraser, Text
/// - Rectangle, Ellipse (circle-like, supports aspect ratio), ShapeLine
/// - Shapes are started by clicking a shape button then clicking/dragging on the board
/// - Shapes use same color/width as pen
/// - Ellipse supports Shift-constrain to perfect circle
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WhiteboardController : MonoBehaviour
{
    [Header("References")]
    public RectTransform boardRect;                 // assign WhiteboardPanel RectTransform
    public Transform strokesParent;                 // assign WhiteboardContent transform (world-space parent for line objects)
    public Camera uiCamera;                         // optional: camera used for exporting
    public GameObject lineRendererPrefab;           // prefab with LineRenderer component
    public GameObject textPrefab;                   // prefab with TMP Input field for text elements
    public Material penMaterial;
    public Material highlighterMaterial;
    public Material eraserMaterial;                 // optional separate material
    public Canvas canvas;                           // main canvas (used for coordinate conversion)
    public GraphicRaycaster graphicRaycaster;       // optional: for advanced UI queries. If null, will try to get from canvas.

    [Header("Cursor Feedback")]
    public RectTransform penCursor;
    public RectTransform highlighterCursor;
    public RectTransform eraserCursor;   // already existed, reused
    public RectTransform textCursor;
    public RectTransform rectangleCursor;
    public RectTransform ellipseCursor;
    public RectTransform lineCursor;

    private Dictionary<Tool, RectTransform> toolCursors;
    private RectTransform activeCursor;

    [Header("Brush")]
    public Color currentColor = Color.black;
    public float brushSize = 8f;
    public Tool currentTool = Tool.Pen;
    [Range(0f, 1f)]
    public float highlighterAlpha = 0.35f;

    [Header("UX Options")]
    public bool blockDrawIfPointerOverUI = true;    // don't create strokes when clicking UI outside the board
    public KeyCode blockDrawWhileHolding = KeyCode.None; // optionally block drawing while holding a key (set to None to disable)
    //public RectTransform eraserCursor;              // optional UI element for eraser cursor (assign in inspector)
    public Color eraserCursorColor = new Color(1f, 1f, 1f, 0.25f);
    public Color eraserHighlightColor = Color.yellow; // color to highlight stroke under eraser

    [Header("Smoothing (Pen)")]
    public bool smoothingEnabled = true;
    [Tooltip("Number of subdivisions between computed midpoints. 0 = just midpoints, 2-5 = smoother")]
    [Range(0, 6)]
    public int smoothingSubdivisions = 2;
    [Tooltip("min pixel distance (screen space) between raw samples")]
    public float minDistance = 4f; // pixels
    public int maxPointsPerLine = 8192;

    [Header("Shapes")]
    [Tooltip("Segments used to approximate ellipses/circles")]
    public int ellipseSegments = 64;
    [Tooltip("Whether shapes should be filled (not implemented) - reserved")]
    public bool shapesFilled = false; // reserved for future
    public bool shapeSmoothing = false; // unused (shapes drawn by geometry)

    // pooling & undo/redo
    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();
    private List<GameObject> activeStrokes = new List<GameObject>();

    // current stroke state
    private LineRenderer currentLine;
    private List<Vector3> currentRenderedPoints = new List<Vector3>(); // points actually set into LineRenderer
    private List<Vector3> currentRawPoints = new List<Vector3>(); // raw captured points (board world)
    private GameObject currentStrokeGO;

    // shape state
    private Vector3 shapeStartWorld;
    private bool isDrawing = false;

    // eraser hover
    private StrokeData highlightedStroke = null;

    void Awake()
    {
        if (strokesParent == null) strokesParent = transform;
        if (canvas == null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null) canvas = c;
        }
        if (graphicRaycaster == null && canvas != null)
        {
            graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
        }

        // hide eraser cursor at start
        if (eraserCursor != null) eraserCursor.gameObject.SetActive(false);

        toolCursors = new Dictionary<Tool, RectTransform>
{
            { Tool.Pen, penCursor },
            { Tool.Highlighter, highlighterCursor },
            { Tool.Eraser, eraserCursor },
            { Tool.Text, textCursor },
            { Tool.Rectangle, rectangleCursor },
            { Tool.Ellipse, ellipseCursor },
            { Tool.ShapeLine, lineCursor }
        };

        // Hide all cursors at start
        foreach (var kv in toolCursors)
        {
            if (kv.Value != null) kv.Value.gameObject.SetActive(false);
        }

    }

    void Update()
    {
        HandleInput();

        // update eraser hover visualizer when not drawing and tool is eraser
        if (currentTool == Tool.Eraser)
        {
            Vector2 pointer = Input.mousePosition;
            bool pointerOverBoard = IsPointerOverBoard(pointer);
            if (eraserCursor != null)
            {
                eraserCursor.gameObject.SetActive(pointerOverBoard);
                if (pointerOverBoard)
                {
                    float px = brushSize; // interpret brushSize as pixels for cursor sizing on UI
                    eraserCursor.position = pointer;
                    eraserCursor.sizeDelta = new Vector2(px, px);
                    Image img = eraserCursor.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = eraserCursorColor;
                    }
                }
            }

            if (!isDrawing && pointerOverBoard)
            {
                UpdateEraserHover(pointer);
            }
            else
            {
                ClearHighlight();
            }
        }
        else
        {
            if (eraserCursor != null && eraserCursor.gameObject.activeSelf)
                eraserCursor.gameObject.SetActive(false);
            ClearHighlight();
        }
        UpdateCursor();


    }

    #region Input
    void HandleInput()
    {
        // unify mouse/touch single-touch handling

        // Mouse down
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 sp = Input.mousePosition;

            // blocking key
            if (blockDrawWhileHolding != KeyCode.None && Input.GetKey(blockDrawWhileHolding))
            {
                return;
            }

            // pointer must be over board
            if (!IsPointerOverBoard(sp))
            {
                return;
            }

            // shape / text / eraser / pen branching
            if (currentTool == Tool.Text)
            {
                CreateTextAt(sp);
            }
            else if (currentTool == Tool.Eraser)
            {
                StartEraser(sp); // starts erasing immediately
            }
            else if (currentTool == Tool.Rectangle || currentTool == Tool.Ellipse || currentTool == Tool.ShapeLine)
            {
                StartShape(sp);
            }
            else
            {
                StartStroke(sp);
            }
        }
        // Mouse hold (continue)
        else if (Input.GetMouseButton(0) && isDrawing)
        {
            Vector2 sp = Input.mousePosition;
            if (currentTool == Tool.Eraser)
            {
                EraseAt(sp);
            }
            else if (currentTool == Tool.Rectangle || currentTool == Tool.Ellipse || currentTool == Tool.ShapeLine)
            {
                ContinueShape(sp);
            }
            else
            {
                ContinueStroke(sp);
            }
        }
        else if (Input.GetMouseButtonUp(0) && isDrawing)
        {
            if (currentTool == Tool.Rectangle || currentTool == Tool.Ellipse || currentTool == Tool.ShapeLine)
                EndShape();
            else
                EndStroke();
        }

        // Touch fallback (single touch)
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            Vector2 sp = t.position;
            if (t.phase == TouchPhase.Began)
            {
                if (!IsPointerOverBoard(sp)) return;
                if (currentTool == Tool.Text) CreateTextAt(sp);
                else if (currentTool == Tool.Eraser) StartEraser(sp);
                else if (currentTool == Tool.Rectangle || currentTool == Tool.Ellipse || currentTool == Tool.ShapeLine) StartShape(sp);
                else StartStroke(sp);
            }
            else if ((t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) && isDrawing)
            {
                if (currentTool == Tool.Eraser) EraseAt(sp);
                else if (currentTool == Tool.Rectangle || currentTool == Tool.Ellipse || currentTool == Tool.ShapeLine) ContinueShape(sp);
                else ContinueStroke(sp);
            }
            else if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) && isDrawing)
            {
                if (currentTool == Tool.Rectangle || currentTool == Tool.Ellipse || currentTool == Tool.ShapeLine) EndShape();
                else EndStroke();
            }
        }
    }

    void UpdateCursor()
    {
        Vector2 pointer = Input.mousePosition;
        bool pointerOverBoard = IsPointerOverBoard(pointer);

        // deactivate previous cursor
        if (activeCursor != null) activeCursor.gameObject.SetActive(false);
        activeCursor = null;

        if (!pointerOverBoard) return; // hide if outside board

        if (toolCursors.TryGetValue(currentTool, out RectTransform cursor) && cursor != null)
        {
            activeCursor = cursor;
            activeCursor.gameObject.SetActive(true);
            activeCursor.position = pointer;

            // Adjust size & color depending on tool
            float px = brushSize;

            if (currentTool == Tool.Pen || currentTool == Tool.Highlighter)
            {
                activeCursor.sizeDelta = new Vector2(px, px);
                var img = activeCursor.GetComponent<Image>();
                if (img != null) img.color = currentColor;
            }
            else if (currentTool == Tool.Eraser)
            {
                activeCursor.sizeDelta = new Vector2(px, px);
                var img = activeCursor.GetComponent<Image>();
                if (img != null) img.color = eraserCursorColor;
            }
            else if (currentTool == Tool.Text)
            {
                activeCursor.sizeDelta = new Vector2(20, 20); // fixed caret-like size
                var img = activeCursor.GetComponent<Image>();
                if (img != null) img.color = Color.black;
            }
            else if (currentTool == Tool.Rectangle || currentTool == Tool.Ellipse || currentTool == Tool.ShapeLine)
            {
                activeCursor.sizeDelta = new Vector2(24, 24); // small icon, fixed
                var img = activeCursor.GetComponent<Image>();
                if (img != null) img.color = currentColor;
            }
        }
    }


    bool IsPointerOverBoard(Vector2 screenPos)
    {
        if (boardRect == null || canvas == null) return false;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out local))
        {
            Rect r = boardRect.rect;
            if (local.x >= r.xMin && local.x <= r.xMax && local.y >= r.yMin && local.y <= r.yMax) return true;
        }
        return false;
    }
    #endregion

    #region Pen/Highlighter lifecycle & smoothing
    void StartStroke(Vector2 screenPos)
    {
        isDrawing = true;
        currentStrokeGO = Instantiate(lineRendererPrefab, strokesParent);
        currentLine = currentStrokeGO.GetComponent<LineRenderer>();
        if (currentLine == null)
        {
            Debug.LogError("lineRendererPrefab must contain a LineRenderer component.");
            isDrawing = false;
            return;
        }

        currentLine.positionCount = 0;
        currentLine.startWidth = brushSize * 0.01f;
        currentLine.endWidth = brushSize * 0.01f;
        currentLine.numCapVertices = 8;
        currentLine.numCornerVertices = 8;

        Color strokeColor = currentColor;
        if (currentTool == Tool.Pen)
        {
            currentLine.material = penMaterial != null ? penMaterial : currentLine.material;
            strokeColor.a = 1f;
        }
        else if (currentTool == Tool.Highlighter)
        {
            currentLine.material = highlighterMaterial != null ? highlighterMaterial : currentLine.material;
            Color c = strokeColor;
            c.a = highlighterAlpha;
            strokeColor = c;
        }
        else if (currentTool == Tool.Eraser)
        {
            currentLine.material = eraserMaterial != null ? eraserMaterial : currentLine.material;
        }

        currentRenderedPoints.Clear();
        currentRawPoints.Clear();

        AddRawPoint(ScreenToBoardWorld(screenPos), force: true);

        var meta = currentStrokeGO.AddComponent<StrokeData>();
        meta.Initialize(currentLine, strokeColor);
    }

    void ContinueStroke(Vector2 screenPos)
    {
        if (currentLine == null) return;
        AddRawPoint(ScreenToBoardWorld(screenPos), force: false);
    }

    void EndStroke()
    {
        if (currentLine == null) { isDrawing = false; return; }

        if (currentRenderedPoints.Count < 1)
        {
            Vector3 p = currentRawPoints.Count > 0 ? currentRawPoints[currentRawPoints.Count - 1] : Vector3.zero;
            currentRenderedPoints.Add(p);
            currentLine.positionCount = currentRenderedPoints.Count;
            currentLine.SetPosition(currentRenderedPoints.Count - 1, p);
        }

        undoStack.Push(currentStrokeGO);
        redoStack.Clear();
        activeStrokes.Add(currentStrokeGO);

        currentLine = null;
        currentStrokeGO = null;
        currentRawPoints.Clear();
        currentRenderedPoints.Clear();
        isDrawing = false;
    }

    void AddRawPoint(Vector3 worldPoint, bool force)
    {
        if (!force && currentRawPoints.Count > 0)
        {
            Vector2 prevScreen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, currentRawPoints[currentRawPoints.Count - 1]);
            Vector2 curScreen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, worldPoint);
            if (Vector2.Distance(prevScreen, curScreen) < minDistance) return;
        }

        currentRawPoints.Add(worldPoint);

        if (!smoothingEnabled || currentRawPoints.Count < 2)
        {
            currentRenderedPoints.Add(worldPoint);
        }
        else
        {
            int n = currentRawPoints.Count;
            Vector3 pPrev = currentRawPoints[n - 2];
            Vector3 pCurr = currentRawPoints[n - 1];
            Vector3 mid = (pPrev + pCurr) * 0.5f;

            if (currentRenderedPoints.Count == 0)
            {
                currentRenderedPoints.Add(mid);
            }
            else
            {
                Vector3 lastRendered = currentRenderedPoints[currentRenderedPoints.Count - 1];
                int subs = Mathf.Max(0, smoothingSubdivisions);
                if (subs == 0)
                {
                    currentRenderedPoints.Add(mid);
                }
                else
                {
                    for (int s = 1; s <= subs; s++)
                    {
                        float t = (float)s / (subs + 1);
                        Vector3 interp = Vector3.Lerp(lastRendered, mid, t);
                        currentRenderedPoints.Add(interp);
                    }
                    currentRenderedPoints.Add(mid);
                }
            }
        }

        if (currentRenderedPoints.Count > maxPointsPerLine)
        {
            int remove = currentRenderedPoints.Count - maxPointsPerLine;
            currentRenderedPoints.RemoveRange(0, remove);
        }

        currentLine.positionCount = currentRenderedPoints.Count;
        for (int i = 0; i < currentRenderedPoints.Count; i++)
        {
            currentLine.SetPosition(i, currentRenderedPoints[i]);
        }

        if (currentStrokeGO != null)
        {
            StrokeData meta = currentStrokeGO.GetComponent<StrokeData>();
            if (meta != null)
            {
                meta.ApplyColorToRenderer();
            }
        }
    }
    #endregion

    #region Shape drawing
    void StartShape(Vector2 screenPos)
    {
        isDrawing = true;
        currentStrokeGO = Instantiate(lineRendererPrefab, strokesParent);
        currentLine = currentStrokeGO.GetComponent<LineRenderer>();
        if (currentLine == null)
        {
            Debug.LogError("lineRendererPrefab must contain a LineRenderer component.");
            isDrawing = false;
            return;
        }

        currentLine.positionCount = 0;
        currentLine.startWidth = brushSize * 0.01f;
        currentLine.endWidth = brushSize * 0.01f;
        currentLine.numCapVertices = 0;
        currentLine.numCornerVertices = 0;
        currentLine.loop = false; // for rectangles we will close manually by repeating first vertex

        // use pen/highlighter material depending on mode; shapes use same color/alpha as pen/highlighter
        Color strokeColor = currentColor;
        if (currentTool == Tool.Ellipse || currentTool == Tool.Rectangle || currentTool == Tool.ShapeLine)
        {
            // shapes use pen color and width
            currentLine.material = penMaterial != null ? penMaterial : currentLine.material;
            strokeColor.a = 1f;
        }

        var meta = currentStrokeGO.AddComponent<StrokeData>();
        meta.Initialize(currentLine, strokeColor);

        shapeStartWorld = ScreenToBoardWorld(screenPos);

        // initialize with a tiny 2-point line so renderer updates smoothly
        currentRenderedPoints.Clear();
        currentRawPoints.Clear();
        currentRenderedPoints.Add(shapeStartWorld);
        currentRenderedPoints.Add(shapeStartWorld + Vector3.right * 0.001f);
        currentLine.positionCount = currentRenderedPoints.Count;
        currentLine.SetPosition(0, currentRenderedPoints[0]);
        currentLine.SetPosition(1, currentRenderedPoints[1]);
    }

    void ContinueShape(Vector2 screenPos)
    {
        if (currentLine == null) return;
        Vector3 currentWorld = ScreenToBoardWorld(screenPos);

        if (currentTool == Tool.Rectangle)
        {
            UpdateRectangle(shapeStartWorld, currentWorld);
        }
        else if (currentTool == Tool.Ellipse)
        {
            bool constrainCircle = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            UpdateEllipse(shapeStartWorld, currentWorld, constrainCircle);
        }
        else if (currentTool == Tool.ShapeLine)
        {
            UpdateStraightLine(shapeStartWorld, currentWorld);
        }
    }

    void EndShape()
    {
        if (currentLine == null) { isDrawing = false; return; }

        // finalize: push to undo/redo and active list
        undoStack.Push(currentStrokeGO);
        redoStack.Clear();
        activeStrokes.Add(currentStrokeGO);

        currentLine = null;
        currentStrokeGO = null;
        currentRenderedPoints.Clear();
        currentRawPoints.Clear();
        isDrawing = false;
    }

    void UpdateStraightLine(Vector3 a, Vector3 b)
    {
        currentRenderedPoints.Clear();
        currentRenderedPoints.Add(a);
        currentRenderedPoints.Add(b);
        currentLine.loop = false;
        currentLine.positionCount = currentRenderedPoints.Count;
        currentLine.SetPosition(0, a);
        currentLine.SetPosition(1, b);
    }

    void UpdateRectangle(Vector3 aWorld, Vector3 bWorld)
    {
        // compute rect corners in board local space to ensure axis-aligned rect relative to board
        Vector2 aLocal = WorldToBoardLocal(aWorld);
        Vector2 bLocal = WorldToBoardLocal(bWorld);

        float xMin = Mathf.Min(aLocal.x, bLocal.x);
        float xMax = Mathf.Max(aLocal.x, bLocal.x);
        float yMin = Mathf.Min(aLocal.y, bLocal.y);
        float yMax = Mathf.Max(aLocal.y, bLocal.y);

        // corners
        Vector3 bl = BoardLocalToWorld(new Vector2(xMin, yMin));
        Vector3 br = BoardLocalToWorld(new Vector2(xMax, yMin));
        Vector3 tr = BoardLocalToWorld(new Vector2(xMax, yMax));
        Vector3 tl = BoardLocalToWorld(new Vector2(xMin, yMax));

        // rectangle: 5 points to close (repeat first)
        currentRenderedPoints.Clear();
        currentRenderedPoints.Add(bl);
        currentRenderedPoints.Add(br);
        currentRenderedPoints.Add(tr);
        currentRenderedPoints.Add(tl);
        currentRenderedPoints.Add(bl); // close loop
        currentLine.loop = false; // closed by repeating first point
        currentLine.positionCount = currentRenderedPoints.Count;
        for (int i = 0; i < currentRenderedPoints.Count; i++)
            currentLine.SetPosition(i, currentRenderedPoints[i]);
    }

    void UpdateEllipse(Vector3 aWorld, Vector3 bWorld, bool constrainToCircle)
    {
        Vector2 aLocal = WorldToBoardLocal(aWorld);
        Vector2 bLocal = WorldToBoardLocal(bWorld);

        float xMin = Mathf.Min(aLocal.x, bLocal.x);
        float xMax = Mathf.Max(aLocal.x, bLocal.x);
        float yMin = Mathf.Min(aLocal.y, bLocal.y);
        float yMax = Mathf.Max(aLocal.y, bLocal.y);

        float width = xMax - xMin;
        float height = yMax - yMin;

        if (constrainToCircle)
        {
            float size = Mathf.Max(width, height);
            // recenter to maintain drag anchor as start corner
            if (bLocal.x < aLocal.x) xMin = aLocal.x - size;
            else xMax = aLocal.x + size;
            if (bLocal.y < aLocal.y) yMin = aLocal.y - size;
            else yMax = aLocal.y + size;

            width = xMax - xMin;
            height = yMax - yMin;
        }

        Vector2 centerLocal = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        Vector3 centerWorld = BoardLocalToWorld(centerLocal);
        float aRadius = width * 0.5f;
        float bRadius = height * 0.5f;

        int segs = Mathf.Max(12, ellipseSegments);

        currentRenderedPoints.Clear();
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs * Mathf.PI * 2f;
            float x = Mathf.Cos(t) * aRadius;
            float y = Mathf.Sin(t) * bRadius;
            Vector2 lp = centerLocal + new Vector2(x, y);
            Vector3 wp = BoardLocalToWorld(lp);
            currentRenderedPoints.Add(wp);
        }
        currentLine.loop = false; // closed by repeating first/last
        currentLine.positionCount = currentRenderedPoints.Count;
        for (int i = 0; i < currentRenderedPoints.Count; i++)
            currentLine.SetPosition(i, currentRenderedPoints[i]);
    }

    Vector2 WorldToBoardLocal(Vector3 world)
    {
        // inverse of ScreenToBoardWorld: take world -> local coordinates relative to strokesParent
        Vector3 localInParent = strokesParent.InverseTransformPoint(world);
        return new Vector2(localInParent.x, localInParent.y);
    }

    Vector3 BoardLocalToWorld(Vector2 local)
    {
        return strokesParent.TransformPoint(new Vector3(local.x, local.y, 0f));
    }
    #endregion

    #region Eraser & Text
    void StartEraser(Vector2 screenPos)
    {
        isDrawing = true;
        EraseAt(screenPos);
    }

    void EraseAt(Vector2 screenPos)
    {
        Vector3 world = ScreenToBoardWorld(screenPos);
        float radius = brushSize * 0.02f;

        for (int i = activeStrokes.Count - 1; i >= 0; i--)
        {
            GameObject go = activeStrokes[i];
            if (go == null) { activeStrokes.RemoveAt(i); continue; }
            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null) continue;
            int pc = lr.positionCount;
            for (int p = 0; p < pc; p++)
            {
                if (Vector3.Distance(world, lr.GetPosition(p)) <= radius)
                {
                    undoStack.Push(go);
                    activeStrokes.RemoveAt(i);
                    go.SetActive(false);
                    redoStack.Clear();
                    ClearHighlightIf(go);
                    return;
                }
            }
        }
        for (int i = strokesParent.childCount - 1; i >= 0; i--)
        {
            Transform t = strokesParent.GetChild(i);
            TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Vector3 pos = t.position;
                if (Vector3.Distance(world, pos) <= radius)
                {
                    undoStack.Push(t.gameObject);
                    t.gameObject.SetActive(false);
                    redoStack.Clear();
                    ClearHighlightIf(t.gameObject);
                    return;
                }
            }
        }
    }

    void UpdateEraserHover(Vector2 screenPos)
    {
        Vector3 world = ScreenToBoardWorld(screenPos);
        float radius = brushSize * 0.02f;
        StrokeData nearest = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < activeStrokes.Count; i++)
        {
            GameObject go = activeStrokes[i];
            if (go == null) continue;
            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null) continue;
            int pc = lr.positionCount;
            for (int p = 0; p < pc; p++)
            {
                float d = Vector3.Distance(world, lr.GetPosition(p));
                if (d <= radius && d < bestDist)
                {
                    bestDist = d;
                    nearest = go.GetComponent<StrokeData>();
                }
            }
        }

        if (nearest != null) SetHighlight(nearest);
        else ClearHighlight();
    }

    void SetHighlight(StrokeData s)
    {
        if (highlightedStroke == s) return;
        ClearHighlight();
        highlightedStroke = s;
        if (highlightedStroke != null) highlightedStroke.SetHighlight(true, eraserHighlightColor);
    }

    void ClearHighlight()
    {
        if (highlightedStroke != null)
        {
            highlightedStroke.SetHighlight(false, Color.clear);
            highlightedStroke = null;
        }
    }

    void ClearHighlightIf(GameObject go)
    {
        if (highlightedStroke != null && highlightedStroke.gameObject == go)
        {
            ClearHighlight();
        }
    }

    void CreateTextAt(Vector2 screenPos)
    {
        Vector3 world = ScreenToBoardWorld(screenPos);
        GameObject go = Instantiate(textPrefab, strokesParent);
        go.transform.position = world;
        go.transform.localScale = Vector3.one;
        TMP_InputField input = go.GetComponentInChildren<TMP_InputField>();
        if (input != null)
        {
            input.ActivateInputField();
        }
        undoStack.Push(go);
        redoStack.Clear();
        activeStrokes.Add(go);
    }
    #endregion

    #region Undo/Redo/Clear
    public void Undo()
    {
        if (undoStack.Count == 0) return;
        GameObject go = undoStack.Pop();
        if (go == null) return;
        go.SetActive(false);
        redoStack.Push(go);
        if (activeStrokes.Contains(go)) activeStrokes.Remove(go);
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;
        GameObject go = redoStack.Pop();
        if (go == null) return;
        go.SetActive(true);
        undoStack.Push(go);
        if (!activeStrokes.Contains(go)) activeStrokes.Add(go);
    }

    public void ClearBoard()
    {
        foreach (Transform t in strokesParent)
        {
            Destroy(t.gameObject);
        }
        undoStack.Clear();
        redoStack.Clear();
        activeStrokes.Clear();
    }
    #endregion

    #region Export
    public IEnumerator ExportToPNG(string path)
    {
        if (uiCamera == null)
        {
            Debug.LogWarning("No export camera set. Can't export.");
            yield break;
        }

        int w = (int)boardRect.rect.width;
        int h = (int)boardRect.rect.height;
        RenderTexture rt = new RenderTexture(w, h, 24);
        uiCamera.targetTexture = rt;
        uiCamera.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        uiCamera.targetTexture = null;
        Destroy(rt);

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Destroy(tex);
        Debug.Log($"Whiteboard exported to {path}");
        yield return null;
    }
    #endregion

    #region Utilities
    Vector3 ScreenToBoardWorld(Vector2 screenPos)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint);
        Vector3 world = strokesParent.TransformPoint(localPoint);
        return world;
    }
    #endregion

    #region Public API for UI
    public void SetColor(Color c) { currentColor = c; }
    public void SetBrushSize(float s) { brushSize = s; }
    public void SetTool(Tool t) { currentTool = t; }
    public void SetSmoothingEnabled(bool enabled) { smoothingEnabled = enabled; }
    public void SetSmoothingSubdivisions(int subs) { smoothingSubdivisions = subs; }
    public void SetEraserCursor(RectTransform rt)
    {
        eraserCursor = rt;
        if (eraserCursor != null) eraserCursor.gameObject.SetActive(false);
    }
    #endregion
}

/// <summary>
/// Small helper component attached to each stroke (LineRenderer) so we can
/// change colors, restore them, and toggle a highlight without losing originals.
/// </summary>
public class StrokeData : MonoBehaviour
{
    private LineRenderer lr;
    private Color baseColor = Color.black;
    private bool highlighted = false;
    private Color originalStartColor;
    private Color originalEndColor;

    public void Initialize(LineRenderer lineRenderer, Color strokeColor)
    {
        lr = lineRenderer;
        baseColor = strokeColor;
        ApplyColorToRenderer();
    }

    public void ApplyColorToRenderer()
    {
        if (lr == null) return;
        lr.startColor = baseColor;
        lr.endColor = baseColor;
    }

    public void SetHighlight(bool enable, Color highlightColor)
    {
        if (lr == null) return;
        if (enable && !highlighted)
        {
            originalStartColor = lr.startColor;
            originalEndColor = lr.endColor;
            lr.startColor = highlightColor;
            lr.endColor = highlightColor;
            highlighted = true;
        }
        else if (!enable && highlighted)
        {
            lr.startColor = originalStartColor;
            lr.endColor = originalEndColor;
            highlighted = false;
        }
    }
}

public enum Tool
{
    Pen = 0,
    Highlighter = 1,
    Eraser = 2,
    Text = 3,
    Rectangle = 4,
    Ellipse = 5,
    ShapeLine = 6
}
