using UnityEngine;
using UnityEditor;

public class CreateFistsData
{
    public static void Execute()
    {
        // Create FistsData asset
        WeaponData fists = ScriptableObject.CreateInstance<WeaponData>();
        fists.weaponName = "Fists";
        fists.damage = 25f;
        fists.isFists = true;

        AssetDatabase.CreateAsset(fists, "Assets/ScriptableObjects/Weapons/FistsData.asset");
        AssetDatabase.SaveAssets();

        // Assign to WeaponManager in Player prefab
        string prefabPath = "Assets/Prefabs/Player/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("Player prefab not found at: " + prefabPath);
            return;
        }

        WeaponManager wm = prefab.GetComponentInChildren<WeaponManager>();
        if (wm == null)
        {
            Debug.LogError("WeaponManager not found in Player prefab");
            return;
        }

        SerializedObject so = new SerializedObject(wm);
        so.FindProperty("fistsData").objectReferenceValue = fists;
        so.ApplyModifiedProperties();

        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log($"FistsData created (damage={fists.damage}) and assigned to WeaponManager.fistsData");
    }
}
