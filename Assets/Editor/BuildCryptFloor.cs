using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class BuildCryptFloor
{
    [MenuItem("Tools/Build Crypt Floor Grid")]
    public static void Execute()
    {
        GameObject floorObj = GameObject.Find("Floor");
        if (floorObj == null) { Debug.LogError("Floor not found in scene."); return; }

        Transform existingTile = floorObj.transform.Find("Tile");
        if (existingTile == null) { Debug.LogError("Tile not found under Floor."); return; }

        const int cols = 13;
        const int rows = 25;
        const float tileSize = 2f;

        // Existing tile is center of front row → col=6, row=0 → local pos (0,0,0)
        Vector3 basePos   = existingTile.localPosition;
        Quaternion baseRot = existingTile.localRotation;
        Vector3 baseScale  = existingTile.localScale;

        int created = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // Skip the existing tile (col=6, row=0)
                if (col == 6 && row == 0) continue;

                float localX = (col - 6) * tileSize;
                float localZ = row * tileSize;

                GameObject clone = Object.Instantiate(existingTile.gameObject);
                clone.name = "Tile";
                clone.transform.SetParent(floorObj.transform, false);
                clone.transform.localPosition = new Vector3(localX, basePos.y, localZ);
                clone.transform.localRotation = baseRot;
                clone.transform.localScale    = baseScale;
                created++;
            }
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"Crypt floor grid built: {created} tiles created (+ 1 existing = {cols * rows} total).");
    }
}
