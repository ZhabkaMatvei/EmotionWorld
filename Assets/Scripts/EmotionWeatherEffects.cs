using UnityEngine;

public class EmotionWeatherEffects : MonoBehaviour
{
    [Header("References")]
    public WorldGenerator worldGenerator;
    public Light directionalLight;
    public Transform followTarget;

    [Header("Follow Settings")]
    public float effectArea = 80f;
    public float followHeight = 18f;
    public float transitionSpeed = 2f;

    [Header("Lightning (Stress)")]
    public bool enableLightning = true;
    public float minLightningDelay = 2.5f;
    public float maxLightningDelay = 5.0f;

    private GameObject effectsRoot;

    private ParticleSystem rainPS;
    private ParticleSystem mistPS;
    private ParticleSystem pollenPS;
    private ParticleSystem stormDustPS;
    private ParticleSystem calmMotesPS;

    private Material particleMaterial;

    private float rainRate;
    private float mistRate;
    private float pollenRate;
    private float stormRate;
    private float calmRate;

    private float targetRainRate;
    private float targetMistRate;
    private float targetPollenRate;
    private float targetStormRate;
    private float targetCalmRate;

    private float baseLightIntensity = 1.3f;
    private Color baseLightColor = Color.white;

    private float lightningTimer;
    private float lightningFlashTime;
    private float lightningFlashStrength;

    private void Start()
    {
        if (worldGenerator == null)
            worldGenerator = FindObjectOfType<WorldGenerator>();

        if (directionalLight == null)
            directionalLight = FindObjectOfType<Light>();

        if (followTarget == null && Camera.main != null)
            followTarget = Camera.main.transform;

        if (directionalLight != null)
        {
            baseLightIntensity = directionalLight.intensity;
            baseLightColor = directionalLight.color;
        }

        CreateParticleMaterial();
        CreateEffects();

        lightningTimer = Random.Range(minLightningDelay, maxLightningDelay);
    }

    private void Update()
    {
        if (worldGenerator == null)
            return;

        UpdateRootPosition();
        UpdateTargetsByEmotion();
        UpdateEmissionRates();
        ApplyEmission(rainPS, rainRate);
        ApplyEmission(mistPS, mistRate);
        ApplyEmission(pollenPS, pollenRate);
        ApplyEmission(stormDustPS, stormRate);
        ApplyEmission(calmMotesPS, calmRate);

        UpdateLightMood();
        UpdateLightning();
    }

    private void CreateParticleMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        particleMaterial = new Material(shader);
    }

    private void CreateEffects()
    {
        effectsRoot = new GameObject("EmotionWeatherEffects_Root");
        effectsRoot.transform.SetParent(transform);

        rainPS = CreateRainSystem();
        mistPS = CreateMistSystem();
        pollenPS = CreatePollenSystem();
        stormDustPS = CreateStormDustSystem();
        calmMotesPS = CreateCalmMotesSystem();
    }

    private void UpdateRootPosition()
    {
        if (followTarget == null)
            return;

        effectsRoot.transform.position =
            new Vector3(
                followTarget.position.x,
                followTarget.position.y + followHeight,
                followTarget.position.z
            );
    }

    private void UpdateTargetsByEmotion()
    {
        switch (worldGenerator.emotion)
        {
            case WorldGenerator.Emotion.Joy:
                targetRainRate = 0f;
                targetMistRate = 4f;
                targetPollenRate = 35f;
                targetStormRate = 0f;
                targetCalmRate = 10f;
                break;

            case WorldGenerator.Emotion.Sadness:
                targetRainRate = 650f;
                targetMistRate = 16f;
                targetPollenRate = 0f;
                targetStormRate = 0f;
                targetCalmRate = 0f;
                break;

            case WorldGenerator.Emotion.Stress:
                targetRainRate = 120f;
                targetMistRate = 8f;
                targetPollenRate = 0f;
                targetStormRate = 50f;
                targetCalmRate = 0f;
                break;

            case WorldGenerator.Emotion.Calm:
                targetRainRate = 0f;
                targetMistRate = 10f;
                targetPollenRate = 6f;
                targetStormRate = 0f;
                targetCalmRate = 22f;
                break;
        }
    }

    private void UpdateEmissionRates()
    {
        float t = Time.deltaTime * transitionSpeed;

        rainRate = Mathf.Lerp(rainRate, targetRainRate, t);
        mistRate = Mathf.Lerp(mistRate, targetMistRate, t);
        pollenRate = Mathf.Lerp(pollenRate, targetPollenRate, t);
        stormRate = Mathf.Lerp(stormRate, targetStormRate, t);
        calmRate = Mathf.Lerp(calmRate, targetCalmRate, t);
    }

    private void ApplyEmission(ParticleSystem ps, float rate)
    {
        if (ps == null)
            return;

        var emission = ps.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
    }

    private void UpdateLightMood()
    {
        if (directionalLight == null)
            return;

        Color targetColor = baseLightColor;
        float targetIntensity = baseLightIntensity;

        switch (worldGenerator.emotion)
        {
            case WorldGenerator.Emotion.Joy:
                targetColor = new Color(1.0f, 0.96f, 0.88f);
                targetIntensity = baseLightIntensity * 1.15f;
                break;

            case WorldGenerator.Emotion.Sadness:
                targetColor = new Color(0.78f, 0.84f, 0.95f);
                targetIntensity = baseLightIntensity * 0.9f;
                break;

            case WorldGenerator.Emotion.Stress:
                targetColor = new Color(0.82f, 0.78f, 0.95f);
                targetIntensity = baseLightIntensity * 0.85f;
                break;

            case WorldGenerator.Emotion.Calm:
                targetColor = new Color(0.95f, 0.98f, 1.0f);
                targetIntensity = baseLightIntensity * 1.0f;
                break;
        }

        directionalLight.color = Color.Lerp(
            directionalLight.color,
            targetColor,
            Time.deltaTime * 1.5f
        );

        if (lightningFlashTime <= 0f)
        {
            directionalLight.intensity = Mathf.Lerp(
                directionalLight.intensity,
                targetIntensity,
                Time.deltaTime * 1.5f
            );
        }
    }

    private void UpdateLightning()
    {
        if (!enableLightning || directionalLight == null)
            return;

        if (worldGenerator.emotion != WorldGenerator.Emotion.Stress)
        {
            lightningFlashTime = 0f;
            return;
        }

        lightningTimer -= Time.deltaTime;

        if (lightningTimer <= 0f)
        {
            lightningTimer = Random.Range(
                minLightningDelay,
                maxLightningDelay
            );

            lightningFlashTime = Random.Range(0.08f, 0.16f);
            lightningFlashStrength = Random.Range(0.6f, 1.3f);
        }

        if (lightningFlashTime > 0f)
        {
            lightningFlashTime -= Time.deltaTime;

            float flash = Mathf.PingPong(
                Time.time * 35f,
                lightningFlashStrength
            );

            directionalLight.intensity =
                baseLightIntensity * 1.2f + flash;
        }
    }

    private ParticleSystem CreateBaseSystem(
        string systemName,
        Color color,
        float size,
        float lifetime,
        Vector3 boxSize)
    {
        GameObject go = new GameObject(systemName);
        go.transform.SetParent(effectsRoot.transform);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer =
            go.GetComponent<ParticleSystemRenderer>();

        renderer.material = particleMaterial;
        renderer.sortMode = ParticleSystemSortMode.Distance;

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = lifetime;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 5000;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;

        ps.Play();

        return ps;
    }

    private ParticleSystem CreateRainSystem()
    {
        ParticleSystem ps = CreateBaseSystem(
            "RainEffect",
            new Color(0.70f, 0.80f, 1f, 0.55f),
            0.08f,
            2.2f,
            new Vector3(effectArea, 1f, effectArea)
        );

        var main = ps.main;
        main.startSpeed = 0f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(-28f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.2f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 8f;
        renderer.velocityScale = 0.4f;

        return ps;
    }

    private ParticleSystem CreateMistSystem()
    {
        ParticleSystem ps = CreateBaseSystem(
            "MistEffect",
            new Color(0.85f, 0.90f, 0.95f, 0.12f),
            6f,
            8f,
            new Vector3(effectArea * 0.9f, 8f, effectArea * 0.9f)
        );

        var main = ps.main;
        main.startSpeed = 0.25f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.15f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 0.08f;
        noise.scrollSpeed = 0.1f;

        return ps;
    }

    private ParticleSystem CreatePollenSystem()
    {
        ParticleSystem ps = CreateBaseSystem(
            "PollenEffect",
            new Color(1f, 0.95f, 0.55f, 0.55f),
            0.14f,
            6f,
            new Vector3(effectArea * 0.85f, 10f, effectArea * 0.85f)
        );

        var main = ps.main;
        main.startSpeed = 0.45f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.0f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.2f;

        return ps;
    }

    private ParticleSystem CreateStormDustSystem()
    {
        ParticleSystem ps = CreateBaseSystem(
            "StormDustEffect",
            new Color(0.35f, 0.32f, 0.42f, 0.30f),
            0.45f,
            5f,
            new Vector3(effectArea * 0.9f, 12f, effectArea * 0.9f)
        );

        var main = ps.main;
        main.startSpeed = 0.7f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.8f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.5f;

        return ps;
    }

    private ParticleSystem CreateCalmMotesSystem()
    {
        ParticleSystem ps = CreateBaseSystem(
            "CalmMotesEffect",
            new Color(0.75f, 1f, 0.88f, 0.45f),
            0.10f,
            7f,
            new Vector3(effectArea * 0.8f, 8f, effectArea * 0.8f)
        );

        var main = ps.main;
        main.startSpeed = 0.25f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.18f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.1f;

        return ps;
    }
}