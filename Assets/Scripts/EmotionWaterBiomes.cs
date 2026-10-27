using UnityEngine;
using System.Collections.Generic;


public class EmotionWaterBiomes : MonoBehaviour
{
    public WorldGenerator worldGenerator;
    [Header("Features")]
    public bool showRiver = true;
    public bool showLake = true;
    public bool showIsland = true;
    public bool showBiomeMarkers = true;
    [Range(1f, 8f)] public float riverWidth = 2.8f;
    [Range(3f, 20f)] public float lakeRadius = 10f;
    [Range(0.1f, 3f)] public float updateInterval = 0.5f;
    [Range(0f, 0.7f)] public float waterOffset = 0.16f;

    private MeshFilter groundFilter;
    private Transform groundTransform;
    private Transform root;
    private Mesh riverMesh;
    private Mesh lakeMesh;
    private Mesh shoreMesh;
    private Material waterMaterial;
    private Material shoreMaterial;
    private Material forestMaterial;
    private Material mountainMaterial;
    private Material valleyMaterial;
    private GameObject island;
    private readonly List<GameObject> biomeObjects = new List<GameObject>();
    private float timer;
    private int lastEmotion = -1;
    private int lastMeshId = -1;
    private float size;
    private const int RiverSegments = 45;
    private const int LakeSegments = 48;
    private Vector2 lakeCenter;

    void Start()
    {
        if (worldGenerator == null) worldGenerator = GetComponent<WorldGenerator>();
        waterMaterial = MakeMaterial(new Color(.16f, .55f, .83f));
        shoreMaterial = MakeMaterial(new Color(.75f, .68f, .48f));
        forestMaterial = MakeMaterial(new Color(.20f, .55f, .26f));
        mountainMaterial = MakeMaterial(new Color(.46f, .46f, .48f));
        valleyMaterial = MakeMaterial(new Color(.69f, .73f, .37f));
        FindTerrain();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < Mathf.Max(.1f, updateInterval)) return;
        timer = 0f;
        FindTerrain();
        if (groundFilter == null || root == null) return;
        int e = worldGenerator != null ? (int)worldGenerator.emotion : 0;
        if (e != lastEmotion) { lastEmotion = e; ApplyEmotionColor(e); }
        RebuildGeometry();
    }

    void FindTerrain()
    {
        GameObject world = GameObject.Find("Generated World");
        if (world == null) return;
        Transform terrain = world.transform.Find("Terrain");
        if (terrain == null) return;
        MeshFilter candidate = terrain.GetComponent<MeshFilter>();
        if (candidate == null || candidate.sharedMesh == null) return;
        int id = candidate.sharedMesh.GetInstanceID();
        if (candidate == groundFilter && id == lastMeshId && root != null) return;
        if (root != null) Destroy(root.gameObject);
        groundFilter = candidate;
        groundTransform = terrain;
        lastMeshId = id;
        size = worldGenerator != null ? worldGenerator.worldSize : 100f;
        lakeCenter = new Vector2(size * .76f, size * .68f);
        root = new GameObject("Water and Biome Details").transform;
        root.SetParent(world.transform, false);
        riverMesh = NewMeshObject("River Ribbon", waterMaterial);
        lakeMesh = NewMeshObject("Lake Surface", waterMaterial);
        shoreMesh = NewMeshObject("Lake Shore", shoreMaterial);
        CreateIsland();
        CreateBiomeMarkers();
        lastEmotion = -1;
    }

    Mesh NewMeshObject(string name, Material mat)
    {
        GameObject o = new GameObject(name);
        o.transform.SetParent(root, false);
        MeshFilter f = o.AddComponent<MeshFilter>();
        MeshRenderer r = o.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        Mesh mesh = new Mesh(); mesh.name = name;
        f.sharedMesh = mesh;
        return mesh;
    }

    Material MakeMaterial(Color color)
    {
        Shader s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Diffuse");
        Material m = new Material(s); m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .55f);
        return m;
    }

    float Height(float x, float z)
    {
        Mesh mesh = groundFilter.sharedMesh;
        Vector3[] v = mesh.vertices;
        int n = Mathf.RoundToInt(Mathf.Sqrt(v.Length));
        if (n < 2 || n * n != v.Length) return 0f;
        float u = Mathf.Clamp(x / size * (n - 1), 0, n - 1.0001f);
        float w = Mathf.Clamp(z / size * (n - 1), 0, n - 1.0001f);
        int ix = Mathf.FloorToInt(u), iz = Mathf.FloorToInt(w);
        float a = u - ix, b = w - iz;
        float h00 = v[iz * n + ix].y;
        float h10 = v[iz * n + ix + 1].y;
        float h01 = v[(iz + 1) * n + ix].y;
        float h11 = v[(iz + 1) * n + ix + 1].y;
        float h = a + b <= 1f ? h00 + a * (h10 - h00) + b * (h01 - h00)
             : h11 + (1-a) * (h01-h11) + (1-b) * (h10-h11);
        return h + groundTransform.position.y;
    }

    Vector2 RiverPoint(float t)
    {
        return new Vector2(Mathf.Lerp(3f, size - 3f, t),
            size * (.32f + .13f * Mathf.Sin(t * Mathf.PI * 2.2f)));
    }

    void RebuildGeometry()
    {
        if (showRiver) BuildRiver(); else riverMesh.Clear();
        if (showLake) BuildLake(); else { lakeMesh.Clear(); shoreMesh.Clear(); }
        if (island != null) island.SetActive(showIsland && showLake);
        UpdateBiomeMarkers();
    }

    void BuildRiver()
    {
        Vector3[] v = new Vector3[(RiverSegments + 1) * 2];
        int[] triangles = new int[RiverSegments * 6];
        for (int i = 0; i <= RiverSegments; i++)
        {
            float t = i / (float)RiverSegments;
            Vector2 p = RiverPoint(t);
            Vector2 prev = RiverPoint(Mathf.Max(0, t - .01f));
            Vector2 next = RiverPoint(Mathf.Min(1, t + .01f));
            Vector2 dir = (next-prev).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            for (int side = 0; side < 2; side++)
            {
                Vector2 q = p + normal * (side == 0 ? -riverWidth * .5f : riverWidth * .5f);
                v[i*2 + side] = new Vector3(q.x, Height(q.x,q.y) + waterOffset, q.y);
            }
            if (i == RiverSegments) continue;
            int k = i*6, j = i*2;
            triangles[k] = j; triangles[k+1] = j+2; triangles[k+2] = j+1;
            triangles[k+3] = j+1; triangles[k+4] = j+2; triangles[k+5] = j+3;
        }
        AssignMesh(riverMesh, v, triangles);
    }

    void BuildLake()
    {
        float centerHeight = Height(lakeCenter.x, lakeCenter.y);
        // Decorative lake is slightly above central terrain. It does not carve terrain.
        float lakeY = centerHeight + waterOffset + .1f;
        Vector3[] disk = new Vector3[LakeSegments + 1];
        int[] faces = new int[LakeSegments * 3];
        disk[0] = new Vector3(lakeCenter.x, lakeY, lakeCenter.y);
        for (int i = 0; i < LakeSegments; i++)
        {
            float a = i * Mathf.PI * 2 / LakeSegments;
            float x = lakeCenter.x + Mathf.Cos(a) * lakeRadius;
            float z = lakeCenter.y + Mathf.Sin(a) * lakeRadius;
            disk[i+1] = new Vector3(x, lakeY, z);
            faces[i*3] = 0; faces[i*3+1] = (i+1)%LakeSegments+1; faces[i*3+2] = i+1;
        }
        AssignMesh(lakeMesh, disk, faces);
        Vector3[] bank = new Vector3[LakeSegments*2];
        int[] bankFaces = new int[LakeSegments*6];
        for (int i=0; i<LakeSegments; i++)
        {
            float a = i * Mathf.PI * 2 / LakeSegments;
            for (int k=0;k<2;k++)
            {
                float radius = lakeRadius + (k == 0 ? 0f : 1.15f);
                float x = lakeCenter.x + Mathf.Cos(a)*radius;
                float z = lakeCenter.y + Mathf.Sin(a)*radius;
                bank[i*2+k] = new Vector3(x, Height(x,z)+waterOffset*.5f, z);
            }
            int q=i*2, next=((i+1)%LakeSegments)*2, j=i*6;
            bankFaces[j]=q; bankFaces[j+1]=next; bankFaces[j+2]=q+1;
            bankFaces[j+3]=q+1; bankFaces[j+4]=next; bankFaces[j+5]=next+1;
        }
        AssignMesh(shoreMesh, bank, bankFaces);
        if (island != null)
            island.transform.position = new Vector3(lakeCenter.x, lakeY-.45f, lakeCenter.y);
    }

    void AssignMesh(Mesh mesh, Vector3[] vertices, int[] tris)
    {
        mesh.Clear(); mesh.vertices = vertices; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }

    void CreateIsland()
    {
        island = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        island.name = "Lake Island";
        island.transform.SetParent(root,false);
        island.transform.localScale = new Vector3(4.5f, 1.6f, 4.5f);
        island.GetComponent<Renderer>().sharedMaterial = valleyMaterial;
        Destroy(island.GetComponent<Collider>());
    }

    void CreateBiomeMarkers()
    {
        biomeObjects.Clear();
        for (int i=0;i<3;i++)
        {
            GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            o.name = i==0 ? "Forest Zone Marker" : (i==1 ? "Mountain Zone Marker" : "Valley Zone Marker");
            o.transform.SetParent(root,false);
            o.transform.localScale = new Vector3(4f, .06f, 4f);
            o.GetComponent<Renderer>().sharedMaterial = i==0 ? forestMaterial : i==1 ? mountainMaterial : valleyMaterial;
            Destroy(o.GetComponent<Collider>());
            biomeObjects.Add(o);
        }
    }

    void UpdateBiomeMarkers()
    {
        Vector2[] pts = {new Vector2(.18f,.70f), new Vector2(.56f,.85f), new Vector2(.35f,.17f)};
        for (int i=0;i<biomeObjects.Count;i++)
        {
            biomeObjects[i].SetActive(showBiomeMarkers);
            Vector2 p = pts[i]*size;
            biomeObjects[i].transform.position = new Vector3(p.x, Height(p.x,p.y)+.03f, p.y);
        }
    }

    void ApplyEmotionColor(int e)
    {
        switch(e)
        {
            case 0: waterMaterial.color = new Color(.22f,.65f,.95f); break;
            case 1: waterMaterial.color = new Color(.29f,.44f,.62f); break;
            case 2: waterMaterial.color = new Color(.36f,.38f,.53f); break;
            default: waterMaterial.color = new Color(.20f,.62f,.80f); break;
        }
    }

    void OnDestroy()
    {
        if (waterMaterial != null) Destroy(waterMaterial);
        if (shoreMaterial != null) Destroy(shoreMaterial);
        if (forestMaterial != null) Destroy(forestMaterial);
        if (mountainMaterial != null) Destroy(mountainMaterial);
        if (valleyMaterial != null) Destroy(valleyMaterial);
        if (riverMesh != null) Destroy(riverMesh);
        if (lakeMesh != null) Destroy(lakeMesh);
        if (shoreMesh != null) Destroy(shoreMesh);
    }
}
