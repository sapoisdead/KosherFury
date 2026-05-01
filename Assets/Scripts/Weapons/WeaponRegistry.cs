using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "WeaponRegistry", menuName = "KosherFury/WeaponRegistry")]
public class WeaponRegistry : ScriptableObject
{
    public List<WeaponData> allWeapons = new();

    public WeaponData GetByName(string weaponName)
    {
        return allWeapons.Find(w => w.weaponName == weaponName);
    }
}
