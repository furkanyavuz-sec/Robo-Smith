// PlayerInteraction.cs — BaseStation algılama eklendi
// Değişiklikler:
//   + HeldObject public property → istasyonlar kontrol edebilsin
//   + PickupFromStation() → istasyon item'ı doğrudan ele verir
//   + ForceDropFromStation() → istasyon item'ı alır/yok eder
//   + TryInteractStation() → önde istasyon var mı, varsa Interact() çağır

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Etkileşim Ayarları")]
    [SerializeField] private float pickupRadius    = 2f;
    [SerializeField] private float stationRadius   = 1.8f;  // İstasyon algılama mesafesi
    [SerializeField] private LayerMask pickupLayer;
    [SerializeField] private LayerMask stationLayer;         // Inspector'da "Station" layer
    [SerializeField] private Transform holdPoint;
    [Header("Debug")]
    [SerializeField] private string nearbyStationName = ""; // Inspector'da durum takibi için

    [Header("Zırh Seçimi")]
    [SerializeField] private ArmorSelectUI armorSelectUI;
    // ── Public API (istasyonların eriştiği alanlar) ──
    public GameObject HeldObject => heldObject;

    // Dahili durum
    private GameObject heldObject;
    private Rigidbody  heldRb;

    private void Update()
{
    if (Keyboard.current == null) return;

    // E tuşu → normal etkileşim
    if (Keyboard.current.eKey.wasPressedThisFrame)
        HandleInteract(RobotChassis.InteractMode.AddToArmor);

    // Q tuşu → silah upgrade
    if (Keyboard.current.qKey.wasPressedThisFrame)
        HandleInteractUpgrade();

    // Tab → zırh geçişi (ArmorSelectUI hallediyor)
    // F → zırh seçimi (ArmorSelectUI hallediyor)

    if (heldObject != null)
        LockToHoldPoint();
}

    // ── Ana etkileşim akışı ──
    // Önce istasyona bak, sonra yerdeki nesneye.
    // Böylece masanın önünde item da varsa öncelik masaya gider.
    private void HandleInteract(RobotChassis.InteractMode mode)
    {
        if (TryInteractStation(mode)) return;
        if (heldObject == null) TryPickup();
        else                    Drop();
    }

    private void HandleInteractUpgrade()
{
    // Q tuşu sadece RobotChassis ile çalışır
    Collider[] hits = Physics.OverlapSphere(
        transform.position, stationRadius, stationLayer
    );

    foreach (Collider col in hits)
    {
        if (!col.TryGetComponent<RobotChassis>(out RobotChassis chassis)) continue;
        if (!chassis.CanInteractUpgrade(this)) continue;

        chassis.InteractWithMode(this, RobotChassis.InteractMode.UpgradeWeapon);
        return;
    }

    Debug.Log("[PlayerInteraction] Q: Upgrade için uygun silah veya malzeme yok.");
}
    private bool TryInteractStation(RobotChassis.InteractMode chassisMode)
{
    Collider[] hits = Physics.OverlapSphere(
        transform.position, stationRadius, stationLayer
    );

    if (hits.Length == 0) return false;

    BaseStation closest     = null;
    float       closestDist = float.MaxValue;

    foreach (Collider col in hits)
    {
        if (!col.TryGetComponent<BaseStation>(out BaseStation station)) continue;

        // RobotChassis ise moda göre kontrol et
        if (station is RobotChassis chassis)
        {
            if (!chassis.CanInteractArmor(this)) continue;
        }
        else
        {
            if (!station.CanInteract(this)) continue;
        }

        float dist = Vector3.Distance(transform.position, col.transform.position);
        if (dist < closestDist) { closestDist = dist; closest = station; }
    }

    if (closest == null) return false;

    // RobotChassis ise mod ile çağır
    if (closest is RobotChassis rc)
        rc.InteractWithMode(this, chassisMode);
    else
        closest.Interact(this);

    return true;
}
    // ── İstasyonların çağırdığı public metodlar ──

    /// <summary>SupplyBin çağırır: spawn edilen nesneyi doğrudan ele al.</summary>
    public void PickupFromStation(GameObject target)
    {
        if (heldObject != null) return; // Güvenlik kontrolü

        heldObject = target;
        heldRb     = target.GetComponent<Rigidbody>();

        if (heldRb != null)
        {
            heldRb.isKinematic = true;
            heldRb.useGravity  = false;
        }

        if (heldObject.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = true;

        heldObject.transform.SetParent(holdPoint);
    }

    /// <summary>TrashBin / Processor çağırır: nesneyi elden bırak ama yok etme.</summary>
    public void ForceDropFromStation()
    {
        if (heldObject == null) return;

        if (heldRb != null)
        {
            heldRb.isKinematic = false;
            heldRb.useGravity  = true;
        }

        if (heldObject.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = false;

        heldObject.transform.SetParent(null);
        heldObject = null;
        heldRb     = null;
    }

    // ── Yerdeki nesne alma/bırakma (önceki hafta) ──

    private void TryPickup()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, pickupRadius, pickupLayer,
            QueryTriggerInteraction.Collide
        );

        if (hits.Length == 0) return;

        GameObject closest     = null;
        float      closestDist = float.MaxValue;

        foreach (Collider col in hits)
        {
            if (!col.TryGetComponent<PickupItem>(out _)) continue;
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDist) { closestDist = dist; closest = col.gameObject; }
        }

        if (closest != null) PickupFromStation(closest);
    }

    private void Drop()
    {
        ForceDropFromStation();
    }

    private void LockToHoldPoint()
    {
        heldObject.transform.position = Vector3.Lerp(
            heldObject.transform.position,
            holdPoint.position,
            Time.deltaTime * 20f
        );
        heldObject.transform.rotation = Quaternion.identity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, stationRadius);
    }
}

