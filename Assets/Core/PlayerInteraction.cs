// PlayerInteraction.cs — BaseStation algılama eklendi
// Değişiklikler:
//   + HeldObject public property → istasyonlar kontrol edebilsin
//   + PickupFromStation() → istasyon item'ı doğrudan ele verir
//   + ForceDropFromStation() → istasyon item'ı alır/yok eder
//   + TryInteractStation() → önde istasyon var mı, varsa Interact() çağır

using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Etkileşim Ayarları")]
    [SerializeField] private float pickupRadius    = 2f;
    [SerializeField] private float stationRadius   = 1.8f;  // İstasyon algılama mesafesi
    [SerializeField] private LayerMask pickupLayer;
    [SerializeField] private LayerMask stationLayer;         // Inspector'da "Station" layer
    [SerializeField] private Transform holdPoint;
    [Header("Debug")]

    [Header("Zırh Seçimi")]
    [SerializeField] private ArmorSelectUI armorSelectUI;
    // ── Public API (istasyonların eriştiği alanlar) ──
    public GameObject HeldObject => heldObject;

    /// <summary>NetworkItem taşıma takibi için elin dünya konumu.</summary>
    public Vector3 HoldPointPosition => holdPoint != null
        ? holdPoint.position
        : transform.position + Vector3.up * 1.2f;

    // Dahili durum
    private GameObject heldObject;
    private Rigidbody  heldRb;

    // ── MP Faz 2: el durumu ağ senkronu ──────────────────────────────────
    // Server yazar; tüm kopyalar (client UI/CanInteract kontrolleri dahil)
    // heldObject işaretçisini buradan günceller.
    private readonly NetworkVariable<NetworkObjectReference> heldNv =
        new(default, NetworkVariableReadPermission.Everyone,
                     NetworkVariableWritePermission.Server);

    /// <summary>NGO oturumu aktif mi? (MP yol ayrımı)</summary>
    private bool IsMp =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    /// <summary>
    /// Bu kopya yerel oyuncunun mu? UI'lar (zırh paneli, ipuçları) MP'de
    /// yanlışlıkla rakibin kopyasına kilitlenmesin diye bunu kullanır.
    /// (new: NetworkBehaviour.IsLocalPlayer'ı bilinçli gizler — offline'da
    /// da true dönmesi gerekiyor, taban üye yalnız ağda anlamlı.)
    /// </summary>
    public new bool IsLocalPlayer => !IsMp || (IsSpawned && IsOwner);

    public override void OnNetworkSpawn()
    {
        heldNv.OnValueChanged += OnHeldChanged;
        OnHeldChanged(default, heldNv.Value);   // Geç katılan kopyalar için
    }

    public override void OnNetworkDespawn()
    {
        heldNv.OnValueChanged -= OnHeldChanged;
        base.OnNetworkDespawn();
    }

    private void OnHeldChanged(NetworkObjectReference oldRef,
        NetworkObjectReference newRef)
    {
        heldObject = newRef.TryGet(out NetworkObject no) ? no.gameObject : null;
    }

    /// <summary>
    /// Offline modda her zaman true; multiplayer'da sadece objenin sahibi.
    /// Diğer oyuncuların avatarları bizim klavyemizi okuyamaz.
    /// </summary>
    private bool HasControl
    {
        get
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;
            return IsSpawned && IsOwner;
        }
    }

    private void Update()
{
    if (!HasControl) return;
    if (Keyboard.current == null) return;

    // E tuşu → normal etkileşim
    if (Keyboard.current.eKey.wasPressedThisFrame)
        HandleInteract(RobotChassis.InteractMode.AddToArmor);

    // Q tuşu → silah upgrade
    if (Keyboard.current.qKey.wasPressedThisFrame)
        HandleInteractUpgrade();

    // Tab → zırh geçişi (ArmorSelectUI hallediyor)
    // F → zırh seçimi (ArmorSelectUI hallediyor)

    // MP'de item parent'lanmaz — pozisyonu NetworkItem/NetworkTransform sürer
    if (heldObject != null && !IsMp)
        LockToHoldPoint();
}

    // ── Ana etkileşim akışı ──
    // Önce istasyona bak, sonra yerdeki nesneye.
    // Böylece masanın önünde item da varsa öncelik masaya gider.
    private void HandleInteract(RobotChassis.InteractMode mode)
    {
        if (IsMp) { HandleInteractMp(mode); return; }

        if (TryInteractStation(mode)) return;
        if (heldObject == null) TryPickup();
        else                    Drop();
    }

    /// <summary>
    /// MP: kararları server verir. Client yalnız EN YAKIN hedefi bulup
    /// RPC atar — istasyon durumları (cooldown, işleme aşaması) client'ta
    /// bayat olduğundan CanInteract ön elemesi yapılmaz.
    /// </summary>
    private void HandleInteractMp(RobotChassis.InteractMode mode)
    {
        BaseStation station = FindClosestStation();
        if (station != null &&
            station.TryGetComponent<NetworkObject>(out NetworkObject sNo))
        {
            InteractStationServerRpc(sNo, (int)mode);
            return;
        }

        if (heldObject == null)
        {
            GameObject item = FindClosestPickup();
            if (item != null &&
                item.TryGetComponent<NetworkObject>(out NetworkObject iNo))
                PickupItemServerRpc(iNo);
        }
        else
        {
            DropServerRpc();
        }
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

        if (IsMp)
        {
            // Uygunluk kararı server'da — sadece hedefi bildir
            if (chassis.TryGetComponent<NetworkObject>(out NetworkObject cNo))
            {
                InteractStationServerRpc(cNo,
                    (int)RobotChassis.InteractMode.UpgradeWeapon);
                return;
            }
            continue;
        }

        if (!chassis.CanInteractUpgrade(this)) continue;

        chassis.InteractWithMode(this, RobotChassis.InteractMode.UpgradeWeapon);
        return;
    }

    if (!IsMp)
        Debug.Log("[PlayerInteraction] Q: Upgrade için uygun silah veya malzeme yok.");
}

    // ── MP yardımcıları ──────────────────────────────────────────────────

    private BaseStation FindClosestStation()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, stationRadius, stationLayer);

        BaseStation closest     = null;
        float       closestDist = float.MaxValue;

        foreach (Collider col in hits)
        {
            if (!col.TryGetComponent<BaseStation>(out BaseStation station)) continue;

            float dist = Vector3.Distance(
                transform.position, col.transform.position);
            if (dist < closestDist) { closestDist = dist; closest = station; }
        }
        return closest;
    }

    private GameObject FindClosestPickup()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, pickupRadius, pickupLayer,
            QueryTriggerInteraction.Collide);

        GameObject closest     = null;
        float      closestDist = float.MaxValue;

        foreach (Collider col in hits)
        {
            if (!col.TryGetComponent<PickupItem>(out _)) continue;

            // Başkasının taşıdığı veya depoda kilitli item alınamaz
            if (col.TryGetComponent<NetworkItem>(out NetworkItem ni) &&
                (ni.IsHeld || ni.Locked)) continue;

            float dist = Vector3.Distance(
                transform.position, col.transform.position);
            if (dist < closestDist) { closestDist = dist; closest = col.gameObject; }
        }
        return closest;
    }

    // ── ServerRpc'ler — tüm ekonomi kararları server'da ──────────────────

    [ServerRpc]
    private void InteractStationServerRpc(NetworkObjectReference stationRef,
        int mode)
    {
        if (!stationRef.TryGet(out NetworkObject stationNo)) return;
        if (!stationNo.TryGetComponent<BaseStation>(out BaseStation station))
            return;

        // Mesafe doğrulaması — istemci ne iddia ederse etsin
        if (Vector3.Distance(transform.position, stationNo.transform.position)
            > stationRadius + 2f) return;

        if (station is RobotChassis chassis)
        {
            var m = (RobotChassis.InteractMode)mode;

            if (m == RobotChassis.InteractMode.UpgradeWeapon)
            {
                if (chassis.CanInteractUpgrade(this))
                    chassis.InteractWithMode(this, m);
            }
            else if (chassis.CanInteractArmor(this))
            {
                chassis.InteractWithMode(this, m);
            }
            return;
        }

        if (station.CanInteract(this))
            station.Interact(this);
    }

    [ServerRpc]
    private void PickupItemServerRpc(NetworkObjectReference itemRef)
    {
        if (heldObject != null) return;
        if (!itemRef.TryGet(out NetworkObject itemNo)) return;

        if (Vector3.Distance(transform.position, itemNo.transform.position)
            > pickupRadius + 2f) return;

        // Aynı anda iki oyuncu kapmasın + depo kilidi — server'da son kontrol
        if (itemNo.TryGetComponent<NetworkItem>(out NetworkItem ni) &&
            (ni.IsHeld || ni.Locked))
            return;

        PickupFromStation(itemNo.gameObject);
    }

    [ServerRpc]
    private void DropServerRpc() => ForceDropFromStation();

    // ── Zırh seçimi (ArmorSelectUI çağırır) ──────────────────────────────

    /// <summary>Offline: doğrudan uygular; MP: server'a iletir (ChassisSync
    /// aynası seçimi client'lara geri yayınlar).</summary>
    public void RequestSetArmor(RobotChassis chassis, ArmorType armor)
    {
        if (chassis == null) return;

        if (!IsMp)
        {
            chassis.SetArmor(armor);
            return;
        }

        if (chassis.TryGetComponent<NetworkObject>(out NetworkObject no))
            SetArmorServerRpc(no, (int)armor);
    }

    [ServerRpc]
    private void SetArmorServerRpc(NetworkObjectReference chassisRef, int armor)
    {
        if (!chassisRef.TryGet(out NetworkObject chassisNo)) return;
        if (!chassisNo.TryGetComponent<RobotChassis>(out RobotChassis chassis))
            return;

        // Zırh paneli 4 birim menzilde açılıyor — payıyla doğrula
        if (Vector3.Distance(transform.position, chassisNo.transform.position)
            > 6f) return;

        chassis.SetArmor((ArmorType)armor);
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

        if (IsMp)
        {
            // Ekonomi server-authoritative: yalnız server el verir.
            // İstasyonlar zaten server'da koşar (ServerRpc üzerinden).
            if (!NetworkManager.Singleton.IsServer) return;

            NetworkItem.EnsureSpawned(target);

            heldObject = target;
            heldRb     = target.GetComponent<Rigidbody>();

            if (target.TryGetComponent<NetworkItem>(out NetworkItem ni))
            {
                ni.SetHolder(NetworkObject);   // Fizik/collider'ı da ayarlar
                ni.SyncType();                 // İstasyon SetType'ı ağa yansısın
            }

            heldNv.Value = new NetworkObjectReference(
                target.GetComponent<NetworkObject>());
            return;
        }

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

        if (IsMp)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            if (heldObject.TryGetComponent<NetworkItem>(out NetworkItem ni))
                ni.SetHolder(null);   // Fizik geri açılır, item yere düşer

            heldObject   = null;
            heldRb       = null;
            heldNv.Value = default;
            return;
        }

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

