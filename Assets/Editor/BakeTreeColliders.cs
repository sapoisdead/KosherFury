using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class BakeTreeColliders
{
    private const string ParentName = "TreeCollidersNavmesh";

    [MenuItem("Tools/Create Tree Colliders for Navmesh")]
    public static void Create()
    {
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("Nessun terrain trovato."); return; }

        // Rimuovi eventuali collider precedenti
        GameObject existing = GameObject.Find(ParentName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
            Debug.Log("[BakeTreeColliders] Rimossi collider precedenti.");
        }

        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        GameObject parent = new GameObject(ParentName);
#pragma warning disable 618
        GameObjectUtility.SetStaticEditorFlags(parent, StaticEditorFlags.NavigationStatic);
#pragma warning restore 618

        foreach (TreeInstance tree in data.treeInstances)
        {
            Vector3 worldPos = new Vector3(
                terrainPos.x + tree.position.x * data.size.x,
                terrainPos.y + tree.position.y * data.size.y,
                terrainPos.z + tree.position.z * data.size.z
            );

            GameObject proxy = new GameObject("TreeColliderProxy");
            proxy.transform.position = worldPos;
            proxy.transform.SetParent(parent.transform);
#pragma warning disable 618
            GameObjectUtility.SetStaticEditorFlags(proxy, StaticEditorFlags.NavigationStatic);
#pragma warning restore 618

            CapsuleCollider col = proxy.AddComponent<CapsuleCollider>();
            col.radius = 0.25f;
            col.height = 3.5f;
            col.center = new Vector3(0, 1.75f, 0);
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[BakeTreeColliders] Creati {data.treeInstanceCount} proxy. Ora baka il NavMesh, poi esegui Remove.");
    }

    [MenuItem("Tools/Remove Tree Colliders for Navmesh")]
    public static void Remove()
    {
        GameObject existing = GameObject.Find(ParentName);
        if (existing == null) { Debug.Log("[BakeTreeColliders] Nessun collider da rimuovere."); return; }

        Object.DestroyImmediate(existing);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[BakeTreeColliders] Collider rimossi.");
    }
}
