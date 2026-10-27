#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EmotionFBXAutoAssign
{
    [MenuItem("Tools/Emotion World/Auto Assign FBX Models")]
    public static void Assign()
    {
        EmotionAssetIntegrator integrator = Object.FindObjectOfType<EmotionAssetIntegrator>();
        if (integrator == null)
        {
            EditorUtility.DisplayDialog("Emotion World", "First attach EmotionAssetIntegrator to WorldManager.", "OK");
            return;
        }
        var living = new List<GameObject>();
        var autumn = new List<GameObject>();
        var dead = new List<GameObject>();
        var snow = new List<GameObject>();
        var rocks = new List<GameObject>();
        var moss = new List<GameObject>();
        var snowRocks = new List<GameObject>();
        var bushes = new List<GameObject>();
        var plants = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (name.StartsWith("rock"))
            {
                if (name.Contains("snow")) snowRocks.Add(prefab);
                else if (name.Contains("moss")) moss.Add(prefab);
                else rocks.Add(prefab);
            }
            else if (name.Contains("tree") || name.StartsWith("willow"))
            {
                if (name.Contains("dead")) dead.Add(prefab);
                else if (name.Contains("snow")) snow.Add(prefab);
                else if (name.Contains("autumn")) autumn.Add(prefab);
                else living.Add(prefab);
            }
            else if (name.StartsWith("bush")) bushes.Add(prefab);
            else if (name.StartsWith("plant") || name.StartsWith("grass") || name.StartsWith("cactusflowers")) plants.Add(prefab);
        }
        Undo.RecordObject(integrator, "Auto Assign Emotion FBX Models");
        integrator.livingTrees = living.ToArray();
        integrator.autumnTrees = autumn.ToArray();
        integrator.deadTrees = dead.ToArray();
        integrator.snowyTrees = snow.ToArray();
        integrator.regularRocks = rocks.ToArray();
        integrator.mossyRocks = moss.ToArray();
        integrator.snowyRocks = snowRocks.ToArray();
        integrator.bushes = bushes.ToArray();
        integrator.plants = plants.ToArray();
        EditorUtility.SetDirty(integrator);
        Debug.Log(string.Format("EMOTION FBX ASSIGNED | trees {0}, autumn {1}, dead {2}, rocks {3}, plants {4}", living.Count, autumn.Count, dead.Count, rocks.Count + moss.Count, plants.Count));
        EditorUtility.DisplayDialog("Emotion World", "FBX models assigned. Press Play to preview.", "OK");
    }
}
#endif
