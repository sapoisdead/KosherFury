using UnityEngine;

public class EnemyWeaponManager : MonoBehaviour, IWeaponHolder
{
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private WeaponData equippedWeaponData;

    private GameObject equippedWeaponInstance;

    public WeaponData CurrentWeapon { get; private set; }
    public Hitbox CurrentHitbox { get; private set; }

    private void Awake()
    {
        Equip(equippedWeaponData);
    }

    public void Equip(WeaponData data)
    {
        if (equippedWeaponInstance != null)
            Destroy(equippedWeaponInstance);

        CurrentWeapon = data;
        CurrentHitbox = null;

        if (data == null || data.handPrefab == null || rightHandSocket == null) return;

        equippedWeaponInstance = Instantiate(data.handPrefab, rightHandSocket);
        equippedWeaponInstance.transform.localPosition = data.positionOffset;
        equippedWeaponInstance.transform.localRotation = Quaternion.Euler(data.rotationOffset);
        CurrentHitbox = equippedWeaponInstance.GetComponentInChildren<Hitbox>();
    }
}
