using UnityEngine;
using UnityEditor;

public class CountTerrainTrees
{
    [MenuItem("Tools/Count Terrain Trees")]
    public static void Execute()
    {
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("Nessun terrain trovato."); return; }

        int count = terrain.terrainData.treeInstanceCount;
        Debug.Log($"[CountTerrainTrees] Alberi totali nel terrain: {count}");
    }
}
