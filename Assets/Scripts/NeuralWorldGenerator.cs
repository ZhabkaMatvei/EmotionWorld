using UnityEngine;
using Unity.Barracuda;

public class NeuralWorldGenerator : MonoBehaviour
{
    [Header("CVAE v2 ONNX")]
    public NNModel modelAsset;
    public WorldGenerator worldGenerator;
    public bool useRandomLatentVector = true;

    private IWorker worker;

    private void Awake()
    {
        if (modelAsset == null || worldGenerator == null)
        {
            Debug.LogError("NeuralWorldGenerator: set Model Asset and World Generator.");
            enabled = false;
            return;
        }
        try
        {
            Model model = ModelLoader.Load(modelAsset);
            worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
            Debug.Log("CVAE v2 initialized successfully!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("CVAE init error: " + ex.Message);
            enabled = false;
        }
    }

    public void GenerateFromEmotion(int emotionIndex)
    {
        if (worker == null) return;
        emotionIndex = Mathf.Clamp(emotionIndex, 0, 3);
        float[] inputVector = new float[12];
        for (int i = 0; i < 8; i++)
        {
            
            inputVector[i] = useRandomLatentVector
                ? Mathf.Clamp((Random.value + Random.value + Random.value +
                               Random.value + Random.value + Random.value - 3f), -2.5f, 2.5f)
                : 0f;
        }
        inputVector[8 + emotionIndex] = 1f;

        try
        {
            using (Tensor input = new Tensor(1, 12, inputVector))
            {
                worker.Execute(input);
                using (Tensor output = worker.PeekOutput().DeepCopy())
                {
                    if (output.length != 12)
                    {
                        Debug.LogError("Expected 12 CVAE outputs; got " + output.length);
                        return;
                    }
                    float[] parameters = new float[12];
                    for (int i = 0; i < 12; i++)
                    {
                        float v = output[i];
                        if (float.IsNaN(v) || float.IsInfinity(v))
                        {
                            Debug.LogError("CVAE produced an invalid parameter at index " + i);
                            return;
                        }
                        parameters[i] = Mathf.Clamp01(v);
                    }
                    Debug.Log(string.Format(
                        "AI WORLD v3 | Emotion {0} | Height {1:F2} | Frequency {2:F3} | Trees {3}",
                        emotionIndex, Mathf.Lerp(2f, 25f, parameters[0]),
                        Mathf.Lerp(.01f, .12f, parameters[1]),
                        Mathf.RoundToInt(Mathf.Lerp(10f, 150f, parameters[2]))));
                    worldGenerator.ApplyNeuralParameters(parameters);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("CVAE inference error: " + ex.Message);
        }
    }

    private void OnDestroy()
    {
        if (worker != null) worker.Dispose();
        worker = null;
    }
}
