using System.Collections.Generic;
using UnityEngine;

public class LandscapeDecorator : MonoBehaviour
{
    [Header("References (optional)")]
    public WorldGenerator worldGenerator;

    [Header("Details")]
    [Range(0, 400)] public int flowerCount = 130;
    [Range(0, 120)] public int bushCount = 35;
    [Range(0, 80)] public int pineCount = 24;
    [Range(0, 100)] public int extraRockCount = 25;
    [Range(0.2f, 5f)] public float scanInterval = 0.5f;
    [Range(0.1f, 5f)] public float groundFollowInterval = 1f;
    public bool trackMorphingTerrain = true;
    public int randomSeed = 2026;

    private Transform currentTerrain;
    private Transform decorationRoot;
    private MeshCollider terrainCollider;
    private float scanTimer;
    private float followTimer;
    private readonly List<Transform> decorations = new List<Transform>();
    private readonly List<Vector2> positions = new List<Vector2>();
    private readonly List<float> groundOffsets = new List<float>();
    private Material trunkMaterial;
    private Material pineMaterial;
    private Material bushMaterial;
    private Material rockMaterial;
    private Material[] flowerMaterials;

    void Start()
    {
        if (worldGenerator == null) worldGenerator = GetComponent<WorldGenerator>();
        CreateMaterials();
        FindAndDecorate();
    }

    void Update()
    {
        scanTimer += Time.deltaTime;
        followTimer += Time.deltaTime;
        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;
            FindAndDecorate();
        }
        if (trackMorphingTerrain && followTimer >= groundFollowInterval)
        {
            followTimer = 0f;
            FollowTerrain();
        }
    }

    void FindAndDecorate()
    {
        // WorldGenerator uses a root named "Generated World" with a child "Terrain".
        GameObject root = GameObject.Find("Generated World");
        if (root == null || !root.activeInHierarchy) return;
        Transform terrain = root.transform.Find("Terrain");
        if (terrain == null) return;

        if (terrain == currentTerrain && decorationRoot != null) return;
        currentTerrain = terrain;
        terrainCollider = terrain.GetComponent<MeshCollider>();
        if (terrainCollider == null) return;

        decorationRoot = new GameObject("Landscape Details").transform;
        decorationRoot.SetParent(root.transform, false);
        decorations.Clear();
        positions.Clear();
        groundOffsets.Clear();

        Bounds bounds = terrainCollider.bounds;
        System.Random random = new System.Random(randomSeed + (worldGenerator != null ? (int)worldGenerator.emotion * 101 : 0));
        AddScatter(flowerCount, bounds, random, 0);
        AddScatter(bushCount, bounds, random, 1);
        AddScatter(pineCount, bounds, random, 2);
        AddScatter(extraRockCount, bounds, random, 3);
        Debug.Log("LANDSCAPE DETAILS | Flowers " + flowerCount +
                  " | Bushes " + bushCount + " | Pines " + pineCount +
                  " | Rocks " + extraRockCount);
    }

    void AddScatter(int count, Bounds bounds, System.Random random, int kind)
    {
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(bounds.min.x + 3f, bounds.max.x - 3f, (float)random.NextDouble());
            float z = Mathf.Lerp(bounds.min.z + 3f, bounds.max.z - 3f, (float)random.NextDouble());
            float y;
            if (!GetGroundY(x, z, out y)) continue;
            // Avoid obvious deep-water/low-lying locations when water is present.
            float waterY = GetWaterHeight();
            if (y < waterY + 0.15f) continue;

            float scale = Mathf.Lerp(0.75f, 1.35f, (float)random.NextDouble());
            GameObject item = null;
            if (kind == 0) item = CreateFlower(i, scale, random);
            if (kind == 1) item = CreateBush(i, scale);
            if (kind == 2) item = CreatePine(i, scale);
            if (kind == 3) item = CreateRock(i, scale, random);
            if (item == null) continue;
            item.transform.SetParent(decorationRoot, false);
            item.transform.position = new Vector3(x, y, z);
            item.transform.Rotate(Vector3.up, (float)random.NextDouble() * 360f);
            decorations.Add(item.transform);
            positions.Add(new Vector2(x, z));
            groundOffsets.Add(0f);
        }
    }

    float GetWaterHeight()
    {
        if (currentTerrain == null || currentTerrain.parent == null) return -9999f;
        Transform water = currentTerrain.parent.Find("Water");
        return water != null ? water.position.y + water.lossyScale.y * 0.5f : -9999f;
    }

    bool GetGroundY(float x, float z, out float y)
    {
        y = 0f;
        if (terrainCollider == null) return false;
        Bounds b = terrainCollider.bounds;
        Ray ray = new Ray(new Vector3(x, b.max.y + 200f, z), Vector3.down);
        RaycastHit hit;
        if (!terrainCollider.Raycast(ray, out hit, b.size.y + 400f)) return false;
        y = hit.point.y;
        return true;
    }

    void FollowTerrain()
    {
        if (currentTerrain == null || decorationRoot == null) return;
        // A smooth-generator may deform its mesh. Refresh the collider temporarily
        // so detail objects can follow the current surface.
        MeshFilter filter = currentTerrain.GetComponent<MeshFilter>();
        if (filter != null && terrainCollider != null && terrainCollider.sharedMesh != filter.sharedMesh)
            terrainCollider.sharedMesh = filter.sharedMesh;
        for (int i = 0; i < decorations.Count; i++)
        {
            if (decorations[i] == null) continue;
            float y;
            if (GetGroundY(positions[i].x, positions[i].y, out y))
            {
                Vector3 p = decorations[i].position;
                p.y = Mathf.Lerp(p.y, y + groundOffsets[i], 0.65f);
                decorations[i].position = p;
            }
        }
    }

    GameObject CreateFlower(int index, float s, System.Random random)
    {
        GameObject root = new GameObject("Flower " + index);
        AddPrimitive(root.transform, PrimitiveType.Cylinder, "Stem", new Vector3(0f, .23f * s, 0f),
            new Vector3(.035f * s, .23f * s, .035f * s), bushMaterial);
        Material petal = flowerMaterials[random.Next(flowerMaterials.Length)];
        for (int p = 0; p < 5; p++)
        {
            float a = p * Mathf.PI * 2f / 5f;
            AddPrimitive(root.transform, PrimitiveType.Sphere, "Petal",
                new Vector3(Mathf.Cos(a) * .105f * s, .49f * s, Mathf.Sin(a) * .105f * s),
                new Vector3(.16f, .10f, .16f) * s, petal);
        }
        AddPrimitive(root.transform, PrimitiveType.Sphere, "Center", new Vector3(0f, .50f * s, 0f),
            Vector3.one * .13f * s, flowerMaterials[0]);
        return root;
    }

    GameObject CreateBush(int index, float s)
    {
        GameObject root = new GameObject("Bush " + index);
        AddPrimitive(root.transform, PrimitiveType.Sphere, "Leaf A", new Vector3(0f, .46f * s, 0f),
            new Vector3(1.1f, .8f, 1f) * s, bushMaterial);
        AddPrimitive(root.transform, PrimitiveType.Sphere, "Leaf B", new Vector3(.40f * s, .52f * s, .05f),
            new Vector3(.75f, .72f, .82f) * s, bushMaterial);
        return root;
    }

    GameObject CreatePine(int index, float s)
    {
        GameObject root = new GameObject("Pine " + index);
        AddPrimitive(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.35f * s, 0f),
            new Vector3(.22f, 1.35f, .22f) * s, trunkMaterial);
        for (int i = 0; i < 3; i++)
        {
            float y = (1.65f + i * .7f) * s;
            float w = (2.0f - i * .45f) * s;
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "Needles", new Vector3(0f, y, 0f),
                new Vector3(w * .5f, .75f * s, w * .5f), pineMaterial);
        }
        return root;
    }

    GameObject CreateRock(int index, float s, System.Random random)
    {
        GameObject root = new GameObject("Detail Rock " + index);
        GameObject rock = AddPrimitive(root.transform, PrimitiveType.Cube, "Stone",
            new Vector3(0f, .24f * s, 0f),
            new Vector3(1.1f, .55f, .85f) * s, rockMaterial);
        rock.transform.localRotation = Quaternion.Euler(15f, (float)random.NextDouble() * 180f, 12f);
        return root;
    }

    GameObject AddPrimitive(Transform parent, PrimitiveType type, string objectName,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        return go;
    }

    void CreateMaterials()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        trunkMaterial = NewMaterial(shader, new Color(.35f, .21f, .13f));
        pineMaterial = NewMaterial(shader, new Color(.12f, .40f, .25f));
        bushMaterial = NewMaterial(shader, new Color(.25f, .66f, .29f));
        rockMaterial = NewMaterial(shader, new Color(.53f, .55f, .57f));
        flowerMaterials = new Material[] {
            NewMaterial(shader, new Color(1f, .83f, .25f)),
            NewMaterial(shader, new Color(1f, .48f, .62f)),
            NewMaterial(shader, new Color(.66f, .53f, 1f)),
            NewMaterial(shader, new Color(.96f, .96f, .89f))
        };
    }

    Material NewMaterial(Shader shader, Color color)
    {
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    void OnDestroy()
    {
        if (trunkMaterial != null) Destroy(trunkMaterial);
        if (pineMaterial != null) Destroy(pineMaterial);
        if (bushMaterial != null) Destroy(bushMaterial);
        if (rockMaterial != null) Destroy(rockMaterial);
        if (flowerMaterials != null)
            foreach (Material m in flowerMaterials) if (m != null) Destroy(m);
    }
}
