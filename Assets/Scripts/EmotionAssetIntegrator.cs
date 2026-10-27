using System.Collections.Generic;
using UnityEngine;

public class EmotionAssetIntegrator : MonoBehaviour
{
    [Header("FBX collections (auto-fill from Tools menu)")]
    public GameObject[] livingTrees;
    public GameObject[] autumnTrees;
    public GameObject[] deadTrees;
    public GameObject[] snowyTrees;
    public GameObject[] regularRocks;
    public GameObject[] mossyRocks;
    public GameObject[] snowyRocks;
    public GameObject[] bushes;
    public GameObject[] plants;

    [Header("References")]
    public WorldGenerator worldGenerator;

    [Header("Replace existing primitive objects")]
    public bool replaceTrees = true;
    public bool replaceRocks = true;
    [Range(0.5f, 12f)] public float treeHeight = 4.5f;
    [Range(0.2f, 5f)] public float rockHeight = 1.2f;

    [Header("FBX axis correction (degrees)")]
    [Tooltip("Most sideways FBX trees need X=-90. Try X=90 if they point down.")]
    public Vector3 treeRotationCorrection = new Vector3(-90f, 0f, 0f);
    public Vector3 plantRotationCorrection = new Vector3(-90f, 0f, 0f);
    public Vector3 rockRotationCorrection = Vector3.zero;

    [Header("Additional vegetation")]
    public bool addGroundPlants = true;
    [Range(0, 250)] public int extraPlantCount = 65;
    public float decorationScale = 1.0f;
    public float rescanInterval = 0.7f;

    private GameObject lastWorld;
    private int lastEmotion = -1;
    private float nextScan;
    private readonly List<Transform> additionalPlants = new List<Transform>();
    private readonly List<Vector2> extraLocations = new List<Vector2>();
    private MeshCollider terrainCollider;
    private int cachedWorldSize = 100;

    private void Start()
    {
        if (worldGenerator == null)
            worldGenerator = GetComponent<WorldGenerator>();
    }

    private void LateUpdate()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + Mathf.Max(0.2f, rescanInterval);

        GameObject root = GameObject.Find("Generated World");
        if (root == null) return;
        int emotion = worldGenerator != null ? (int)worldGenerator.emotion : 3;

        // Handle generator rebuilding the world or switching emotion in place.
        if (lastWorld != root || lastEmotion != emotion)
        {
            lastWorld = root;
            lastEmotion = emotion;
            foreach (Transform plant in additionalPlants)
                if (plant != null) Destroy(plant.gameObject);
            additionalPlants.Clear();
            extraLocations.Clear();
            Rebuild(root, emotion);
        }
        else
        {
            // Проверяем, не появились ли новые деревья или камни.
            ReplaceBaseObjects(root.transform, emotion);
            UpdateGroundPlants();
        }
    }

    private void Rebuild(GameObject root, int emotion)
    {
        Transform terrain = root.transform.Find("Terrain");
        terrainCollider = terrain != null ? terrain.GetComponent<MeshCollider>() : null;
        cachedWorldSize = worldGenerator != null ? worldGenerator.worldSize : 100;
        ReplaceBaseObjects(root.transform, emotion);
        if (addGroundPlants) SpawnGroundPlants(root.transform, emotion);
        Debug.Log("EMOTION FBX | Replaced primitive models for emotion " + emotion);
    }

    private void ReplaceBaseObjects(Transform root, int emotion)
    {
        GameObject[] treeSet = ChooseTreeSet(emotion);
        GameObject[] rockSet = ChooseRockSet(emotion);
        // Создаём копию списка дочерних объектов, чтобы избежать его изменения во время создания моделей.
        Transform[] children = new Transform[root.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = root.GetChild(i);

        foreach (Transform item in children)
        {
            if (item == null) continue;
            bool tree = replaceTrees && item.name == "Tree";
            bool rock = replaceRocks && item.name == "Rock";
            if (!tree && !rock) continue;

            EmotionAssetReplacementMarker marker = item.GetComponent<EmotionAssetReplacementMarker>();
            if (marker == null) marker = item.gameObject.AddComponent<EmotionAssetReplacementMarker>();
            if (marker.emotion == emotion && marker.visual != null) continue;

            if (marker.visual != null) Destroy(marker.visual);
            GameObject[] set = tree ? treeSet : rockSet;
            GameObject model = Pick(set);
            if (model == null) continue;

            // Скрываем стандартные примитивы Unity, но сохраняем объекты, отвечающие за их расположение.
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

            GameObject visual = Instantiate(model, item);
            visual.name = "ImportedFBX_Visual";
            FitModel(
                visual,
                tree ? treeHeight : rockHeight,
                tree ? treeRotationCorrection : rockRotationCorrection,
                Random.Range(0f, 360f)
            );
            marker.visual = visual;
            marker.emotion = emotion;
        }
    }

    private GameObject[] ChooseTreeSet(int emotion)
    {
        if (emotion == 1) return FirstNonEmpty(autumnTrees, livingTrees);
        if (emotion == 2) return FirstNonEmpty(deadTrees, autumnTrees, livingTrees);
        return FirstNonEmpty(livingTrees, autumnTrees);
    }

    private GameObject[] ChooseRockSet(int emotion)
    {
        if (emotion == 2) return FirstNonEmpty(regularRocks, mossyRocks);
        return FirstNonEmpty(mossyRocks, regularRocks, snowyRocks);
    }

    private static GameObject[] FirstNonEmpty(params GameObject[][] groups)
    {
        foreach (GameObject[] group in groups) if (group != null && group.Length > 0) return group;
        return null;
    }

    private GameObject Pick(GameObject[] list)
    {
        if (list == null || list.Length == 0) return null;
        return list[Random.Range(0, list.Length)];
    }

    // исправляем ориентацию осей FBX-модели, затем измеряем её высоту.
    private static void FitModel(
        GameObject instance,
        float targetHeight,
        Vector3 axisCorrection,
        float randomYaw)
    {
        Transform tr = instance.transform;
        tr.localPosition = Vector3.zero;
        tr.localScale = Vector3.one;

        tr.localRotation = Quaternion.Euler(0f, randomYaw, 0f) *
                           Quaternion.Euler(axisCorrection);

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float height = Mathf.Max(0.001f, bounds.size.y);
        float factor = Mathf.Clamp(targetHeight / height, 0.01f, 200f);
        tr.localScale = Vector3.one * factor;


        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        tr.position += Vector3.up * (tr.parent.position.y - bounds.min.y);
    }

    private void SpawnGroundPlants(Transform root, int emotion)
    {
        GameObject[] choices = FirstNonEmpty(plants, bushes);
        if (choices == null || terrainCollider == null) return;
        int count = Mathf.Clamp(extraPlantCount, 0, 250);
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(2f, Mathf.Max(3f, cachedWorldSize - 2f));
            float z = Random.Range(2f, Mathf.Max(3f, cachedWorldSize - 2f));
            RaycastHit hit;
            if (!terrainCollider.Raycast(new Ray(new Vector3(x, 500, z), Vector3.down), out hit, 1000f)) continue;
            GameObject prefab = Pick(choices);
            if (prefab == null) continue;
            GameObject holder = new GameObject("FBX_Plant");
            holder.transform.SetParent(root, true);
            holder.transform.position = hit.point;
            GameObject visual = Instantiate(prefab, holder.transform);
            FitModel(
                visual,
                Random.Range(0.35f, 0.85f) * decorationScale,
                plantRotationCorrection,
                Random.Range(0f, 360f)
            );
            additionalPlants.Add(holder.transform);
            extraLocations.Add(new Vector2(x, z));
        }
    }

    private void UpdateGroundPlants()
    {
        if (terrainCollider == null) return;
        for (int i = 0; i < additionalPlants.Count; i++)
        {
            Transform plant = additionalPlants[i];
            if (plant == null) continue;
            Vector2 p = extraLocations[i];
            RaycastHit hit;
            if (terrainCollider.Raycast(new Ray(new Vector3(p.x, 500, p.y), Vector3.down), out hit, 1000f))
                plant.position = hit.point;
        }
    }
}

public class EmotionAssetReplacementMarker : MonoBehaviour
{
    [HideInInspector] public int emotion = -1;
    [HideInInspector] public GameObject visual;
}
