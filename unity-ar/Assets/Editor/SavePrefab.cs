using UnityEditor;
using UnityEngine;

public static class SavePrefab
{
    [MenuItem("Tools/Save Selected As Prefab")]
    static void Save()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.Log("Select an object in the Hierarchy first."); return; }
        AssetDatabase.CreateFolder("Assets", "Prefabs"); // ignored if it already exists
        PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/" + go.name + ".prefab");
        Debug.Log("Saved prefab for " + go.name);
    }
}