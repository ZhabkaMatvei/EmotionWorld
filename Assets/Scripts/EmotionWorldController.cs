
using UnityEngine;

public class EmotionWorldController : MonoBehaviour
{
    [Header("References")]
    public EmotionSimulator simulator;
    public WorldGenerator worldGenerator;
    public NeuralWorldGenerator neuralGenerator;

    [Header("Settings")]
    public float regenerationCooldown = 3f;

    private WorldGenerator.Emotion lastEmotion;
    private bool initialized = false;
    private float lastGenerationTime = -999f;

    private void Start()
    {
        if (simulator == null ||
            worldGenerator == null ||
            neuralGenerator == null)
        {
            Debug.LogError(
                "EmotionWorldController: " +
                "Assign all references in Inspector!"
            );

            enabled = false;
            return;
        }

        simulator.OnMetricsUpdated += HandleMetrics;

        HandleMetrics(
            simulator.stress,
            simulator.engagement,
            simulator.relaxation
        );
    }

    private void HandleMetrics(
        float stress,
        float engagement,
        float relaxation)
    {
        WorldGenerator.Emotion detectedEmotion;

        if (stress >= 0.65f)
        {
            detectedEmotion =
                WorldGenerator.Emotion.Stress;
        }
        else if (relaxation >= 0.65f)
        {
            detectedEmotion =
                WorldGenerator.Emotion.Calm;
        }
        else if (engagement >= 0.65f)
        {
            detectedEmotion =
                WorldGenerator.Emotion.Joy;
        }
        else
        {
            detectedEmotion =
                WorldGenerator.Emotion.Sadness;
        }

        // Не перестраиваем мир при той же эмоции
        if (initialized && detectedEmotion == lastEmotion)
            return;

        // Ограничение частоты генерации
        if (Time.time - lastGenerationTime <
            regenerationCooldown)
            return;

        initialized = true;
        lastEmotion = detectedEmotion;
        lastGenerationTime = Time.time;

        worldGenerator.emotion = detectedEmotion;

        Debug.Log(
            "Detected emotion: " + detectedEmotion
        );

        // Запускаем нейросетевую генерацию
        neuralGenerator.GenerateFromEmotion(
            (int)detectedEmotion
        );
    }

    private void OnDestroy()
    {
        if (simulator != null)
        {
            simulator.OnMetricsUpdated -= HandleMetrics;
        }
    }
}
