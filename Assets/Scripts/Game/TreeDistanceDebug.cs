using UnityEngine;

public class TreeDistanceDebug : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private float logInterval = 1f;

    private float timer;

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = logInterval;

        if (terrain == null || PlayerManager.Instance == null) return;

        Vector3 playerPos = PlayerManager.Instance.PlayerTransform.position;
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        float minDistance = float.MaxValue;

        foreach (TreeInstance tree in data.treeInstances)
        {
            Vector3 worldPos = new Vector3(
                terrainPos.x + tree.position.x * data.size.x,
                terrainPos.y + tree.position.y * data.size.y,
                terrainPos.z + tree.position.z * data.size.z
            );

            float dist = Vector3.Distance(playerPos, worldPos);
            if (dist < minDistance)
                minDistance = dist;
        }

        Debug.Log($"[TreeDebug] Albero più vicino: {minDistance:F1}m");
    }
}
