using UnityEngine;

/// <summary>
/// Aggiunge capsule collider a runtime per tutti gli alberi del terrain,
/// ignorando il limite di 65 del sistema interno di Unity.
/// </summary>
public class TerrainTreeColliders : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private float colliderRadius = 0.25f;
    [SerializeField] private float colliderHeight = 3.5f;
    [SerializeField] private float colliderCenterY = 1.75f;

    private void Start()
    {
        if (terrain == null)
            terrain = Terrain.activeTerrain;

        if (terrain == null)
        {
            Debug.LogError("[TerrainTreeColliders] Nessun terrain trovato.");
            return;
        }

        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        foreach (TreeInstance tree in data.treeInstances)
        {
            Vector3 worldPos = new Vector3(
                terrainPos.x + tree.position.x * data.size.x,
                terrainPos.y + tree.position.y * data.size.y,
                terrainPos.z + tree.position.z * data.size.z
            );

            GameObject proxy = new GameObject("TreeCollider");
            proxy.transform.position = worldPos;
            proxy.transform.SetParent(transform);

            CapsuleCollider col = proxy.AddComponent<CapsuleCollider>();
            col.radius = colliderRadius;
            col.height = colliderHeight;
            col.center = new Vector3(0, colliderCenterY, 0);
        }

    }
}
