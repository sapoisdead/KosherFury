using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private WeaponRegistry registry;
    [SerializeField] private WeaponData fistsData;

    private readonly List<WeaponData> inventory = new();
    private int currentIndex = -1;
    private GameObject equippedWeapon;

    public WeaponData CurrentWeapon { get; private set; }
    public WeaponHitbox CurrentHitbox { get; private set; }
    public IReadOnlyList<WeaponData> Inventory => inventory;

    public event System.Action OnWeaponChanged;

    private void Start()
    {
        if (fistsData != null)
        {
            inventory.Insert(0, fistsData);
            EquipAt(0);
        }
    }

    public void AddWeapon(WeaponData data)
    {
        if (data == null || inventory.Contains(data)) return;
        inventory.Add(data);
        if (inventory.Count == 1)
            EquipAt(0);
    }

    private void OnScrollWeapon(InputValue value)
    {
        if (inventory.Count < 2) return;
        float scroll = value.Get<float>();
        if (scroll == 0f) return;

        int dir = scroll > 0 ? -1 : 1;
        EquipAt((currentIndex + dir + inventory.Count) % inventory.Count);
    }

    private void EquipAt(int index)
    {
        currentIndex = index;
        Equip(inventory[index]);
    }

    public void Equip(WeaponData data)
    {
        if (equippedWeapon != null)
            Destroy(equippedWeapon);

        CurrentWeapon = data;

        if (data == null || data.handPrefab == null || rightHandSocket == null) return;

        equippedWeapon = Instantiate(data.handPrefab, rightHandSocket);
        equippedWeapon.transform.localPosition = data.positionOffset;
        equippedWeapon.transform.localRotation = Quaternion.Euler(data.rotationOffset);
        CurrentHitbox = equippedWeapon.GetComponentInChildren<WeaponHitbox>();

        OnWeaponChanged?.Invoke();
    }

    public void Unequip()
    {
        if (equippedWeapon != null)
            Destroy(equippedWeapon);

        equippedWeapon = null;
        CurrentWeapon = null;
        currentIndex = -1;
    }

    public bool HasWeapon(string weaponName) =>
        inventory.Exists(w => w.weaponName == weaponName);

    // Chiamato dal PersistenceManager al load
    public void LoadFromSave(List<string> weaponNames, string equippedName)
    {
        if (registry == null) return;

        // Rimuovi tutto tranne le fists (che Start() ha già inserito)
        inventory.RemoveAll(w => w != fistsData);
        currentIndex = fistsData != null ? 0 : -1;

        foreach (string name in weaponNames)
        {
            WeaponData data = registry.GetByName(name);
            if (data != null && !inventory.Contains(data)) inventory.Add(data);
        }

        int idx = inventory.FindIndex(w => w.weaponName == equippedName);
        if (idx >= 0) EquipAt(idx);
        else if (fistsData != null) EquipAt(0);
    }

    // Chiamato dal PersistenceManager al save
    public (List<string> names, string equipped) GetSaveData()
    {
        List<string> names = new();
        foreach (var w in inventory)
        {
            if (w != null && !w.isFists) names.Add(w.weaponName);
        }
        string equipped = (CurrentWeapon != null && !CurrentWeapon.isFists) ? CurrentWeapon.weaponName : "";
        return (names, equipped);
    }
}
