using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateWeaponRegistry
{
    public static void Execute()
    {
        // Load existing WeaponData assets
        WeaponData knife = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/ScriptableObjects/Weapons/AbramKnifeData.asset");
        WeaponData izmel = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/ScriptableObjects/Weapons/IzmelData.asset");

        // Create WeaponRegistry
        WeaponRegistry registry = ScriptableObject.CreateInstance<WeaponRegistry>();
        if (knife != null) registry.allWeapons.Add(knife);
        if (izmel != null) registry.allWeapons.Add(izmel);

        AssetDatabase.CreateAsset(registry, "Assets/ScriptableObjects/Weapons/WeaponRegistry.asset");
        AssetDatabase.SaveAssets();

        // Now assign registry to WeaponManager in Player prefab
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
        SerializedProperty registryProp = so.FindProperty("registry");
        registryProp.objectReferenceValue = registry;
        so.ApplyModifiedProperties();

        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log($"WeaponRegistry created with {registry.allWeapons.Count} weapons and assigned to WeaponManager. knife={knife}, izmel={izmel}");
    }
}
