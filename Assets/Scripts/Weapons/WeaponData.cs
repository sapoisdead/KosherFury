using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "KosherFury/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public GameObject handPrefab;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public float damage = 50f;
    public bool isFists;
}
