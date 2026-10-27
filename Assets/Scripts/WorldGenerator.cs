using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


public class WorldGenerator : MonoBehaviour
{
    public enum Emotion { Joy, Sadness, Stress, Calm }
    public Emotion emotion = Emotion.Calm;

    [Header("World - keep existing scene values")]
    [Range(20, 250)] public int worldSize = 100;
    [Range(16, 200)] public int resolution = 100;
    [Range(0, 200)] public int treeCount = 70;
    public bool useNeuralParameters = false;

    [Header("Neural Outputs (normalized 0..1)")]
    public float[] neuralParameters = new float[12];

    [Header("Smooth Transition")]
    [Min(0.2f)] public float transitionDuration = 5f;
    [Range(1, 10)] public int normalUpdateEveryNFrames = 3;
    public Light sun;
    public bool randomizeTerrainOnEmotionChange = true;

    private GameObject world;
    private Mesh terrainMesh;
    private MeshCollider terrainCollider;
    private Vector3[] vertices;
    private float[] fromY, toY;
    private Material groundMaterial, leafMaterial, waterMaterial, rockMaterial, trunkMaterial;
    private Transform water;
    private readonly List<Transform> trees = new List<Transform>();
    private readonly List<Transform> rocks = new List<Transform>();
    private readonly List<Vector2> treeXZ = new List<Vector2>();
    private readonly List<Vector2> rockXZ = new List<Vector2>();
    private readonly List<float> treeSizeSeed = new List<float>();
    private readonly List<float> rockSizeSeed = new List<float>();
    private readonly List<Material> materials = new List<Material>();
    private Coroutine transition;
    private float offsetX, offsetZ;
    private WorldSettings current;

    private struct WorldSettings
    {
        public float height, frequency, rough, detail, water, fog, bright, hue, vegetation, rocks, saturation;
        public int treeCount;
        public Color groundColor, skyColor, leafColor, fogColor;
        public float waterY;
    }

    private void Start()
    {
        if (world == null) GenerateWorld();
    }

    public void ApplyNeuralParameters(float[] parameters)
    {
        if (parameters == null || parameters.Length != 12)
        {
            Debug.LogError("WorldGenerator: expected 12 CVAE output values.");
            return;
        }
        neuralParameters = new float[12];
        for (int i = 0; i < 12; i++) neuralParameters[i] = Mathf.Clamp01(parameters[i]);
        useNeuralParameters = true;
        GenerateWorld();
    }

    // Compatibility with older NeuralWorldGenerator revisions.
    public void ApplyNeuralParameters(float height, float frequency, int treesCount)
    {
        float[] p = DefaultProfile(emotion);
        p[0] = Mathf.InverseLerp(2f, 25f, height);
        p[1] = Mathf.InverseLerp(.01f, .12f, frequency);
        p[2] = Mathf.InverseLerp(10f, 150f, treesCount);
        ApplyNeuralParameters(p);
    }

    // Compatibility with another v3 script revision.
    public void ApplyNeuralOutputs(float[] outputs) { ApplyNeuralParameters(outputs); }

    public void GenerateWorld()
    {
        WorldSettings target = SettingsFromParameters(
            useNeuralParameters && neuralParameters != null && neuralParameters.Length == 12
            ? neuralParameters : DefaultProfile(emotion));

        if (world == null)
        {
            CreateOnce();
            if (randomizeTerrainOnEmotionChange) NewNoiseOffsets();
            SetHeightsImmediate(target);
            ApplyEnvironment(target);
            PositionDecorations(target);
            current = target;
            Debug.Log("WORLD SMOOTH | initial world: " + emotion);
            return;
        }

        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(MorphTo(target));
    }

    private void CreateOnce()
    {
        world = new GameObject("Generated World");
        int n = Mathf.Clamp(resolution, 16, 200);
        vertices = new Vector3[n * n];
        fromY = new float[n * n];
        toY = new float[n * n];
        int[] triangles = new int[(n - 1) * (n - 1) * 6];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int i = z * n + x;
            vertices[i] = new Vector3((float)x / (n - 1) * worldSize, 0f,
                (float)z / (n - 1) * worldSize);
        }
        int t = 0;
        for (int z = 0; z < n - 1; z++)
        for (int x = 0; x < n - 1; x++)
        {
            int i = z * n + x;
            triangles[t++] = i; triangles[t++] = i + n; triangles[t++] = i + 1;
            triangles[t++] = i + 1; triangles[t++] = i + n; triangles[t++] = i + n + 1;
        }
        terrainMesh = new Mesh { name = "Smooth Terrain Mesh" };
        terrainMesh.vertices = vertices;
        terrainMesh.triangles = triangles;
        terrainMesh.RecalculateNormals();
        GameObject terrain = new GameObject("Terrain");
        terrain.transform.SetParent(world.transform);
        terrain.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
        groundMaterial = MakeMaterial(Color.green);
        terrain.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;
        terrainCollider = terrain.AddComponent<MeshCollider>();
        terrainCollider.sharedMesh = terrainMesh;

        GameObject waterObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        waterObj.name = "Water";
        waterObj.transform.SetParent(world.transform);
        waterObj.transform.localScale = new Vector3(worldSize, .1f, worldSize);
        waterMaterial = MakeMaterial(new Color(.16f, .45f, .7f));
        waterObj.GetComponent<Renderer>().sharedMaterial = waterMaterial;
        Destroy(waterObj.GetComponent<Collider>());
        water = waterObj.transform;

        trunkMaterial = MakeMaterial(new Color(.38f, .25f, .16f));
        leafMaterial = MakeMaterial(Color.green);
        rockMaterial = MakeMaterial(new Color(.45f, .45f, .48f));
        // Fixed positions and object pool prevent pop-in during transitions.
        Random.InitState(8241);
        for (int i = 0; i < 150; i++)
        {
            Vector2 pos = new Vector2(Random.Range(2f, worldSize - 2f),
                Random.Range(2f, worldSize - 2f));
            treeXZ.Add(pos);
            treeSizeSeed.Add(Random.Range(.8f, 1.2f));
            GameObject root = new GameObject("Tree");
            root.transform.SetParent(world.transform);
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(root.transform);
            trunk.transform.localPosition = new Vector3(0, 1f, 0);
            trunk.transform.localScale = new Vector3(.35f, 1f, .35f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMaterial;
            Destroy(trunk.GetComponent<Collider>());
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(root.transform);
            crown.transform.localPosition = new Vector3(0, 2.5f, 0);
            crown.transform.localScale = Vector3.one * 2f;
            crown.GetComponent<Renderer>().sharedMaterial = leafMaterial;
            Destroy(crown.GetComponent<Collider>());
            trees.Add(root.transform);
        }
        for (int i = 0; i < 85; i++)
        {
            rockXZ.Add(new Vector2(Random.Range(1f, worldSize - 1f),
                Random.Range(1f, worldSize - 1f)));
            rockSizeSeed.Add(Random.Range(.55f, 1.5f));
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(world.transform);
            rock.GetComponent<Renderer>().sharedMaterial = rockMaterial;
            Destroy(rock.GetComponent<Collider>());
            rocks.Add(rock.transform);
        }
    }

    private IEnumerator MorphTo(WorldSettings target)
    {
        float[] begin = new float[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) begin[i] = vertices[i].y;
        if (randomizeTerrainOnEmotionChange) NewNoiseOffsets();
        for (int i = 0; i < vertices.Length; i++)
            toY[i] = HeightAt(vertices[i].x, vertices[i].z, target);

        WorldSettings start = current;
        float duration = Mathf.Max(.2f, transitionDuration);
        float time = 0f;
        int frame = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
            for (int i = 0; i < vertices.Length; i++)
                vertices[i].y = Mathf.Lerp(begin[i], toY[i], alpha);
            terrainMesh.vertices = vertices;
            terrainMesh.RecalculateBounds();
            if (++frame % Mathf.Max(1, normalUpdateEveryNFrames) == 0)
                terrainMesh.RecalculateNormals();
            WorldSettings visual = Interpolate(start, target, alpha);
            ApplyEnvironment(visual);
            PositionDecorations(visual);
            current = visual;
            yield return null;
        }
        for (int i = 0; i < vertices.Length; i++) vertices[i].y = toY[i];
        terrainMesh.vertices = vertices;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();
        // Collision refresh once at end, not every frame.
        terrainCollider.sharedMesh = null;
        terrainCollider.sharedMesh = terrainMesh;
        ApplyEnvironment(target);
        PositionDecorations(target);
        current = target;
        transition = null;
        Debug.Log(string.Format("WORLD SMOOTH | {0} | Height {1:F1} | Trees {2} | transition complete",
            emotion, target.height, target.treeCount));
    }

    private void SetHeightsImmediate(WorldSettings target)
    {
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y = HeightAt(vertices[i].x, vertices[i].z, target);
        terrainMesh.vertices = vertices;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();
        terrainCollider.sharedMesh = null;
        terrainCollider.sharedMesh = terrainMesh;
    }

    private void PositionDecorations(WorldSettings w)
    {
        water.position = new Vector3(worldSize * .5f, w.waterY, worldSize * .5f);
        waterMaterial.color = Color.Lerp(new Color(.08f, .23f, .40f), w.skyColor, .38f);
        for (int i = 0; i < trees.Count; i++)
        {
            Vector2 p = treeXZ[i];
            float y = MeshHeight(p.x, p.y);
            trees[i].position = new Vector3(p.x, y, p.y);
            // Soft visibility change rather than enabling/disabling objects.
            float visibility = Mathf.Clamp01(w.treeCount - i);
            if (y <= w.waterY + .15f) visibility = 0f;
            float s = Mathf.Lerp(.65f, 1.55f, w.vegetation) * treeSizeSeed[i] * visibility;
            trees[i].localScale = Vector3.one * Mathf.Max(.0001f, s);
        }
        for (int i = 0; i < rocks.Count; i++)
        {
            Vector2 p = rockXZ[i];
            float y = MeshHeight(p.x, p.y);
            rocks[i].position = new Vector3(p.x, y + .15f, p.y);
            float visible = Mathf.Clamp01(w.rocks * 85f - i);
            float s = rockSizeSeed[i] * visible;
            rocks[i].localScale = new Vector3(1.5f * s, .8f * s, s);
        }
    }

    private float MeshHeight(float x, float z)
    {
        int n = Mathf.Clamp(resolution, 16, 200);
        float u = Mathf.Clamp(x / worldSize * (n - 1), 0f, n - 1.001f);
        float v = Mathf.Clamp(z / worldSize * (n - 1), 0f, n - 1.001f);
        int ix = Mathf.FloorToInt(u), iz = Mathf.FloorToInt(v);
        float a = u - ix, b = v - iz;
        // Follows the mesh's diagonal (bottom-left to top-right in grid notation).
        float h00 = vertices[iz * n + ix].y;
        float h10 = vertices[iz * n + ix + 1].y;
        float h01 = vertices[(iz + 1) * n + ix].y;
        float h11 = vertices[(iz + 1) * n + ix + 1].y;
        return a + b <= 1f
            ? h00 + a * (h10 - h00) + b * (h01 - h00)
            : h11 + (1f - a) * (h01 - h11) + (1f - b) * (h10 - h11);
    }

    private void ApplyEnvironment(WorldSettings w)
    {
        groundMaterial.color = w.groundColor;
        leafMaterial.color = w.leafColor;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(Color.gray, w.skyColor, .72f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = w.fog;
        RenderSettings.fogColor = w.fogColor;
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = w.skyColor;
        }
        if (sun == null) sun = FindObjectOfType<Light>();
        if (sun != null && sun.type == LightType.Directional)
            sun.intensity = Mathf.Lerp(.95f, 1.45f, w.bright);
    }

    private void NewNoiseOffsets()
    {
        offsetX = Random.Range(0f, 10000f);
        offsetZ = Random.Range(0f, 10000f);
    }

    private float HeightAt(float x, float z, WorldSettings w)
    {
        float baseNoise = Mathf.PerlinNoise(x * w.frequency + offsetX, z * w.frequency + offsetZ);
        float fine = Mathf.PerlinNoise(x * w.frequency * 3.2f + offsetX + 318f,
            z * w.frequency * 3.2f + offsetZ + 129f);
        float ridge = 1f - Mathf.Abs(2f * baseNoise - 1f);
        float value = Mathf.Lerp(baseNoise, 1f - ridge, w.rough * .55f);
        value += (fine - .5f) * w.detail * .35f;
        return Mathf.Max(0f, value * w.height);
    }

    private WorldSettings SettingsFromParameters(float[] p)
    {
        WorldSettings w = new WorldSettings();
        w.height = Mathf.Lerp(2f, 25f, p[0]);
        w.frequency = Mathf.Lerp(.01f, .12f, p[1]);
        w.treeCount = Mathf.RoundToInt(Mathf.Lerp(10f, 150f, p[2]));
        w.rough = p[3]; w.water = p[4];
        // Keep fog readable, even when the NN outputs a high value.
        w.fog = Mathf.Lerp(.0005f, .009f, p[5]);
        w.bright = Mathf.Lerp(.55f, 1f, p[6]);
        w.hue = p[7]; w.vegetation = p[8]; w.rocks = p[9];
        w.detail = p[10]; w.saturation = p[11];
        w.groundColor = Color.HSVToRGB(Mathf.Lerp(.04f, .38f, w.hue),
            Mathf.Lerp(.25f, .8f, w.saturation), Mathf.Lerp(.55f, .95f, w.bright));
        w.skyColor = Color.Lerp(new Color(.40f, .40f, .55f), new Color(.62f, .85f, 1f), w.bright);
        w.leafColor = Color.Lerp(w.groundColor, Color.green, .48f);
        w.fogColor = Color.Lerp(w.skyColor, w.groundColor, .18f);
        w.waterY = Mathf.Lerp(-1.5f, w.height * .38f, w.water);
        return w;
    }

    private static WorldSettings Interpolate(WorldSettings a, WorldSettings b, float t)
    {
        WorldSettings w = b;
        w.height = Mathf.Lerp(a.height, b.height, t);
        w.frequency = Mathf.Lerp(a.frequency, b.frequency, t);
        w.rough = Mathf.Lerp(a.rough, b.rough, t);
        w.detail = Mathf.Lerp(a.detail, b.detail, t);
        w.treeCount = Mathf.RoundToInt(Mathf.Lerp(a.treeCount, b.treeCount, t));
        w.waterY = Mathf.Lerp(a.waterY, b.waterY, t);
        w.vegetation = Mathf.Lerp(a.vegetation, b.vegetation, t);
        w.rocks = Mathf.Lerp(a.rocks, b.rocks, t);
        w.bright = Mathf.Lerp(a.bright, b.bright, t);
        w.fog = Mathf.Lerp(a.fog, b.fog, t);
        w.groundColor = Color.Lerp(a.groundColor, b.groundColor, t);
        w.skyColor = Color.Lerp(a.skyColor, b.skyColor, t);
        w.leafColor = Color.Lerp(a.leafColor, b.leafColor, t);
        w.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
        return w;
    }

    private Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material m = new Material(shader);
        m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .14f);
        materials.Add(m);
        return m;
    }

    private static float[] DefaultProfile(Emotion e)
    {
        switch (e)
        {
            case Emotion.Joy: return new float[] {.43f,.36f,.80f,.27f,.32f,.08f,.92f,.36f,.83f,.15f,.55f,.90f};
            case Emotion.Sadness: return new float[] {.23f,.20f,.30f,.24f,.42f,.75f,.32f,.63f,.35f,.22f,.30f,.26f};
            case Emotion.Stress: return new float[] {.85f,.78f,.16f,.86f,.12f,.65f,.26f,.08f,.22f,.88f,.88f,.40f};
            default: return new float[] {.27f,.18f,.65f,.12f,.53f,.12f,.75f,.46f,.72f,.12f,.22f,.56f};
        }
    }

    private void OnDestroy()
    {
        if (transition != null) StopCoroutine(transition);
        foreach (Material m in materials) if (m != null) Destroy(m);
        if (terrainMesh != null) Destroy(terrainMesh);
    }
}
