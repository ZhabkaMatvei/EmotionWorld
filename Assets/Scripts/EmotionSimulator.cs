
using UnityEngine;
using System;

public class EmotionSimulator : MonoBehaviour
{
    [Range(0f, 1f)]
    public float stress = 0.2f;

    [Range(0f, 1f)]
    public float engagement = 0.5f;

    [Range(0f, 1f)]
    public float relaxation = 0.8f;

    public bool automaticSimulation = true;
    public float updateInterval = 3f;

    public event Action<float, float, float> OnMetricsUpdated;

    private float timer;

    void Start()
    {
        SendMetrics();
    }

    void Update()
    {
        if (!automaticSimulation)
            return;

        timer += Time.deltaTime;

        if (timer >= updateInterval)
        {
            timer = 0f;

            stress = Mathf.Clamp01(
                stress + UnityEngine.Random.Range(-0.4f, 0.4f)
            );

            engagement = Mathf.Clamp01(
                engagement + UnityEngine.Random.Range(-0.4f, 0.4f)
            );

            relaxation = Mathf.Clamp01(
                relaxation + UnityEngine.Random.Range(-0.4f, 0.4f)
            );

            SendMetrics();
        }
    }

    public void SetMetrics(float s, float e, float r)
    {
        stress = Mathf.Clamp01(s);
        engagement = Mathf.Clamp01(e);
        relaxation = Mathf.Clamp01(r);

        SendMetrics();
    }

    void SendMetrics()
    {
        Debug.Log(
            $"Stress: {stress:F2}, " +
            $"Engagement: {engagement:F2}, " +
            $"Relaxation: {relaxation:F2}"
        );

        OnMetricsUpdated?.Invoke(
            stress, engagement, relaxation
        );
    }
}
