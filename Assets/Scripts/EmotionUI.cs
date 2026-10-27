
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmotionUI : MonoBehaviour
{
    [Header("References")]
    public EmotionSimulator simulator;
    public WorldGenerator worldGenerator;

    [Header("Existing Text")]
    public TMP_Text stressText;
    public TMP_Text engagementText;
    public TMP_Text relaxationText;
    public TMP_Text emotionText;

    [Header("Progress Bars")]
    public Image stressBar;
    public Image engagementBar;
    public Image relaxationBar;

    [Header("Additional UI")]
    public TMP_Text titleText;
    public TMP_Text statusText;
    public Image panelBackground;

    private Color stressColor =
        new Color(0.95f, 0.35f, 0.42f);

    private Color engagementColor =
        new Color(0.98f, 0.73f, 0.31f);

    private Color relaxationColor =
        new Color(0.25f, 0.83f, 0.65f);

    void Start()
    {
        if (titleText != null)
            titleText.text = "EMOTION WORLD AI";

        if (statusText != null)
            statusText.text =
                "SIMULATION | CVAE v2 ACTIVE";

        if (panelBackground != null)
        {
            panelBackground.color =
                new Color(0.07f, 0.11f, 0.18f, 0.92f);
        }
    }

    void Update()
    {
        if (simulator == null || worldGenerator == null)
            return;

        UpdateMetric(
            stressText,
            stressBar,
            "STRESS",
            simulator.stress,
            stressColor
        );

        UpdateMetric(
            engagementText,
            engagementBar,
            "ENGAGEMENT",
            simulator.engagement,
            engagementColor
        );

        UpdateMetric(
            relaxationText,
            relaxationBar,
            "RELAXATION",
            simulator.relaxation,
            relaxationColor
        );

        UpdateEmotion();
    }

    void UpdateMetric(
        TMP_Text label,
        Image bar,
        string metricName,
        float value,
        Color color)
    {
        value = Mathf.Clamp01(value);

        if (label != null)
        {
            label.text =
                metricName + ": " +
                Mathf.RoundToInt(value * 100f) + "%";

            label.color = Color.white;
        }

        if (bar != null)
        {
            bar.fillAmount = value;
            bar.color = color;
        }
    }

    void UpdateEmotion()
    {
        if (emotionText == null)
            return;

        switch (worldGenerator.emotion)
        {
            case WorldGenerator.Emotion.Joy:
                emotionText.text = "РАДОСТЬ";
                emotionText.color =
                    new Color(1f, 0.85f, 0.35f);
                break;

            case WorldGenerator.Emotion.Sadness:
                emotionText.text = "ГРУСТЬ";
                emotionText.color =
                    new Color(0.48f, 0.72f, 1f);
                break;

            case WorldGenerator.Emotion.Stress:
                emotionText.text = "СТРЕСС";
                emotionText.color =
                    new Color(1f, 0.42f, 0.48f);
                break;

            case WorldGenerator.Emotion.Calm:
                emotionText.text = "СПОКОЙСТВИЕ";
                emotionText.color =
                    new Color(0.38f, 0.90f, 0.68f);
                break;
        }
    }
}
