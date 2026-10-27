
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EmotionGraphsUI : MonoBehaviour
{
    [Header("References")]
    public EmotionSimulator simulator;
    public Canvas targetCanvas;

    [Header("Graph Settings")]
    public float sampleInterval = 1f;
    [Range(30, 300)]
    public int maxSamples = 120;

    [Header("Panel Settings")]
    public int graphWidth = 480;
    public int graphHeight = 220;

    private List<float> stressHistory = new List<float>();
    private List<float> engagementHistory = new List<float>();
    private List<float> relaxationHistory = new List<float>();

    private GameObject panel;
    private RawImage graphImage;
    private Texture2D graphTexture;
    private Color32[] pixels;

    private Text stressLabel;
    private Text engagementLabel;
    private Text relaxationLabel;
    private Text emotionLabel;

    private Font font;
    private float timer;

    private readonly Color32 background =
        new Color32(19, 30, 47, 255);

    private readonly Color32 gridColor =
        new Color32(50, 66, 85, 255);

    private readonly Color32 stressColor =
        new Color32(255, 110, 110, 255);

    private readonly Color32 engagementColor =
        new Color32(245, 195, 95, 255);

    private readonly Color32 relaxationColor =
        new Color32(75, 225, 165, 255);

    void Start()
    {
        if (simulator == null)
            simulator = FindObjectOfType<EmotionSimulator>();

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (simulator == null || targetCanvas == null)
        {
            Debug.LogError(
                "EmotionGraphsUI: Simulator or Canvas not found!"
            );
            enabled = false;
            return;
        }

        maxSamples = Mathf.Max(2, maxSamples);
        graphWidth = Mathf.Max(64, graphWidth);
        graphHeight = Mathf.Max(64, graphHeight);
        sampleInterval = Mathf.Max(0.1f, sampleInterval);

        font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        CreateInterface();
        CreateGraphTexture();

        AddSample();
        RefreshGraph();
        RefreshLabels();
    }

    void Update()
    {
        if (simulator == null)
            return;

        timer += Time.deltaTime;

        if (timer >= sampleInterval)
        {
            timer = 0f;

            AddSample();
            RefreshGraph();
        }

        RefreshLabels();
    }

    void CreateInterface()
    {
        panel = new GameObject(
            "EmotionGraphsPanel",
            typeof(RectTransform),
            typeof(Image)
        );

        panel.transform.SetParent(
            targetCanvas.transform, false
        );

        RectTransform rect =
            panel.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);

        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(540f, 385f);

        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.10f, 0.16f, 0.94f);

        bg.raycastTarget = false;

        CreateText(
            "Title",
            "EMOTIV | LIVE METRICS",
            new Vector2(20, -15),
            new Vector2(500, 30),
            23,
            Color.white
        );

        CreateText(
            "Mode",
            "SIMULATION  |  CVAE v2",
            new Vector2(20, -48),
            new Vector2(500, 24),
            16,
            new Color(0.55f, 0.80f, 1f)
        );

        GameObject graphObject = new GameObject(
            "LiveGraph",
            typeof(RectTransform),
            typeof(RawImage)
        );

        graphObject.transform.SetParent(
            panel.transform, false
        );

        RectTransform graphRect =
            graphObject.GetComponent<RectTransform>();

        graphRect.anchorMin = new Vector2(0, 1);
        graphRect.anchorMax = new Vector2(0, 1);
        graphRect.pivot = new Vector2(0, 1);

        graphRect.anchoredPosition =
            new Vector2(30, -85);

        graphRect.sizeDelta =
            new Vector2(480, 220);

        graphImage = graphObject.GetComponent<RawImage>();
        graphImage.raycastTarget = false;

        stressLabel = CreateText(
            "StressValue",
            "STRESS: 0%",
            new Vector2(20, -315),
            new Vector2(165, 25),
            17,
            stressColor
        );

        engagementLabel = CreateText(
            "EngagementValue",
            "ENGAGEMENT: 0%",
            new Vector2(185, -315),
            new Vector2(200, 25),
            17,
            engagementColor
        );

        relaxationLabel = CreateText(
            "RelaxationValue",
            "RELAXATION: 0%",
            new Vector2(370, -315),
            new Vector2(165, 25),
            17,
            relaxationColor
        );

        emotionLabel = CreateText(
            "EmotionStatus",
            "STATE: WAITING",
            new Vector2(20, -350),
            new Vector2(500, 25),
            17,
            Color.white
        );
    }

    Text CreateText(
        string objectName,
        string content,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Text)
        );

        obj.transform.SetParent(panel.transform, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);

        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text label = obj.GetComponent<Text>();
        label.font = font;
        label.fontSize = fontSize;
        label.text = content;
        label.color = color;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;

        return label;
    }

    void CreateGraphTexture()
    {
        graphTexture = new Texture2D(
            graphWidth,
            graphHeight,
            TextureFormat.RGBA32,
            false
        );

        graphTexture.filterMode = FilterMode.Bilinear;
        graphTexture.wrapMode = TextureWrapMode.Clamp;

        pixels = new Color32[graphWidth * graphHeight];

        graphImage.texture = graphTexture;
    }

    void AddSample()
    {
        AddValue(stressHistory, simulator.stress);
        AddValue(engagementHistory, simulator.engagement);
        AddValue(relaxationHistory, simulator.relaxation);
    }

    void AddValue(List<float> history, float value)
    {
        history.Add(Mathf.Clamp01(value));

        if (history.Count > maxSamples)
            history.RemoveAt(0);
    }

    void RefreshGraph()
    {
        ClearGraph();
        DrawGrid();

        DrawHistory(stressHistory, stressColor);
        DrawHistory(engagementHistory, engagementColor);
        DrawHistory(relaxationHistory, relaxationColor);

        graphTexture.SetPixels32(pixels);
        graphTexture.Apply(false);
    }

    void ClearGraph()
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = background;
    }

    void DrawGrid()
    {
        for (int y = 0; y < graphHeight; y +=
            Mathf.Max(1, graphHeight / 4))
        {
            for (int x = 0; x < graphWidth; x++)
                SetPixelSafe(x, y, gridColor);
        }

        for (int x = 0; x < graphWidth; x +=
            Mathf.Max(1, graphWidth / 6))
        {
            for (int y = 0; y < graphHeight; y++)
                SetPixelSafe(x, y, gridColor);
        }
    }

    void DrawHistory(List<float> history, Color32 color)
    {
        if (history.Count < 2)
            return;

        for (int i = 1; i < history.Count; i++)
        {
            int x0 = Mathf.RoundToInt(
                (i - 1f) / (maxSamples - 1) *
                (graphWidth - 1)
            );

            int x1 = Mathf.RoundToInt(
                (float)i / (maxSamples - 1) *
                (graphWidth - 1)
            );

            int y0 = Mathf.RoundToInt(
                history[i - 1] * (graphHeight - 1)
            );

            int y1 = Mathf.RoundToInt(
                history[i] * (graphHeight - 1)
            );

            DrawLine(x0, y0, x1, y1, color);
        }
    }

    void DrawLine(
        int x0, int y0,
        int x1, int y1,
        Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int error = dx - dy;

        while (true)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oy = -1; oy <= 1; oy++)
                    SetPixelSafe(x0 + ox, y0 + oy, color);
            }

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = error * 2;

            if (e2 > -dy)
            {
                error -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    void SetPixelSafe(int x, int y, Color32 color)
    {
        if (x < 0 || x >= graphWidth ||
            y < 0 || y >= graphHeight)
            return;

        pixels[y * graphWidth + x] = color;
    }

    void RefreshLabels()
    {
        stressLabel.text =
            "STRESS: " +
            Mathf.RoundToInt(simulator.stress * 100f) + "%";

        engagementLabel.text =
            "ENGAGEMENT: " +
            Mathf.RoundToInt(simulator.engagement * 100f) + "%";

        relaxationLabel.text =
            "RELAXATION: " +
            Mathf.RoundToInt(simulator.relaxation * 100f) + "%";

        if (simulator.stress >= 0.65f)
            emotionLabel.text = "STATE: STRESS";
        else if (simulator.relaxation >= 0.65f)
            emotionLabel.text = "STATE: CALM";
        else if (simulator.engagement >= 0.65f)
            emotionLabel.text = "STATE: JOY";
        else
            emotionLabel.text = "STATE: SADNESS";
    }

    void OnDestroy()
    {
        if (graphTexture != null)
            Destroy(graphTexture);

        if (panel != null)
            Destroy(panel);
    }
}
