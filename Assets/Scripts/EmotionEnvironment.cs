
using UnityEngine;

public class EmotionEnvironment : MonoBehaviour
{
    public WorldGenerator worldGenerator;
    public float transitionSpeed = 0.8f;

    private Color targetSky;
    private Color targetGround;
    private Color targetFog;
    private float targetFogDensity;

    private Material terrainMaterial;
    private WorldGenerator.Emotion lastEmotion;

    void Start()
    {
        lastEmotion = worldGenerator.emotion;
        UpdateTargets();
    }

    void Update()
    {
        if (worldGenerator == null)
            return;

        if (lastEmotion != worldGenerator.emotion)
        {
            lastEmotion = worldGenerator.emotion;
            UpdateTargets();
        }

        float t = 1f - Mathf.Exp(
            -transitionSpeed * Time.deltaTime
        );

        if (Camera.main != null)
        {
            Camera.main.backgroundColor = Color.Lerp(
                Camera.main.backgroundColor,
                targetSky,
                t
            );
        }

        RenderSettings.ambientLight = Color.Lerp(
            RenderSettings.ambientLight,
            targetSky,
            t
        );

        RenderSettings.fog = true;

        RenderSettings.fogColor = Color.Lerp(
            RenderSettings.fogColor,
            targetFog,
            t
        );

        RenderSettings.fogDensity = Mathf.Lerp(
            RenderSettings.fogDensity,
            targetFogDensity,
            t
        );

        GameObject terrain = GameObject.Find("Terrain");

        if (terrain != null)
        {
            Renderer renderer = terrain.GetComponent<Renderer>();

            if (renderer != null)
            {
                if (terrainMaterial != renderer.material)
                    terrainMaterial = renderer.material;

                terrainMaterial.color = Color.Lerp(
                    terrainMaterial.color,
                    targetGround,
                    t
                );
            }
        }
    }

    void UpdateTargets()
    {
        switch (worldGenerator.emotion)
        {
            case WorldGenerator.Emotion.Joy:
                targetSky = new Color(0.4f, 0.8f, 1f);
                targetGround = new Color(0.3f, 0.8f, 0.2f);
                targetFog = targetSky;
                targetFogDensity = 0.002f;
                break;

            case WorldGenerator.Emotion.Sadness:
                targetSky = new Color(0.4f, 0.5f, 0.6f);
                targetGround = new Color(0.3f, 0.4f, 0.5f);
                targetFog = Color.gray;
                targetFogDensity = 0.025f;
                break;

            case WorldGenerator.Emotion.Stress:
                targetSky = new Color(0.3f, 0.2f, 0.3f);
                targetGround = new Color(0.4f, 0.25f, 0.25f);
                targetFog = new Color(0.3f, 0.2f, 0.3f);
                targetFogDensity = 0.04f;
                break;

            default:
                targetSky = new Color(0.5f, 0.8f, 0.9f);
                targetGround = new Color(0.3f, 0.7f, 0.4f);
                targetFog = targetSky;
                targetFogDensity = 0.001f;
                break;
        }
    }
}
