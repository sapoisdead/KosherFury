using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "KosherFury/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public GameObject handPrefab;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public float damage = 50f;
    public Sprite iconSprite;
    public bool isFists;
    [Tooltip("Le mosse di quest'arma per il sistema d'azioni nuovo. Vuoto = l'arma usa ancora il sistema vecchio.")]
    public Moveset moveset;

}
