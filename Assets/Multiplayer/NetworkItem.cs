// NetworkItem.cs — MP Faz 2: Ağa senkron item
// Görev: PickupItem prefablarına eklenir (NetworkManagerGenerator'ın prefab
//   hazırlığı NetworkObject + NetworkTransform ile birlikte kurar).
//   - Tip: server yazar, client item rengini/kimliğini uygular
//   - Taşıyıcı: server yazar; item pozisyonunu server holdPoint'e sürer,
//     NetworkTransform tüm client'lara taşır
//   - Auto-spawn: istasyonlar Instantiate ettiğinde server otomatik Spawn
//     eder — istasyon kodlarına dokunmak gerekmez
// Offline: NGO dinlemiyorken tamamen pasif — yerel akış birebir korunur.

using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PickupItem))]
public class NetworkItem : NetworkBehaviour
{
    private readonly NetworkVariable<int> typeNv =
        new(-1, NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<NetworkObjectReference> holderNv =
        new(default, NetworkVariableReadPermission.Everyone,
                     NetworkVariableWritePermission.Server);

    private PickupItem pickup;
    private Rigidbody  rb;

    /// <summary>NGO oturumu aktif mi? (host veya client)</summary>
    public static bool IsMp =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    /// <summary>Bu item'ı şu an biri taşıyor mu? (server yazar, herkes okur)</summary>
    public bool IsHeld => holderNv.Value.TryGet(out _);

    private void Awake()
    {
        pickup = GetComponent<PickupItem>();
        rb     = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Auto-spawn: server tarafında Instantiate edilen her item ağa girer.
        // (Instantiate + SetType aynı karede biter; Start bir sonraki karede
        // koşar — typeNv OnNetworkSpawn'da doğru tipi okur.)
        if (IsMp && NetworkManager.Singleton.IsServer &&
            TryGetComponent<NetworkObject>(out NetworkObject no) && !no.IsSpawned)
            no.Spawn(destroyWithScene: true);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            typeNv.Value = (int)pickup.Type;
        }
        else
        {
            ApplyType(typeNv.Value);

            // Client fizik simüle etmez — pozisyon NetworkTransform'dan gelir
            if (rb != null) rb.isKinematic = true;
        }

        typeNv.OnValueChanged += OnTypeChanged;
    }

    public override void OnNetworkDespawn()
    {
        typeNv.OnValueChanged -= OnTypeChanged;
        base.OnNetworkDespawn();
    }

    private void OnTypeChanged(int oldValue, int newValue) => ApplyType(newValue);

    private void ApplyType(int value)
    {
        if (value < 0) return;

        pickup.SetType((ItemType)value);

        // Rengi tipe göre uygula (ItemVisual Start'ta koşmuş olabilir —
        // tip ağdan sonra geldiyse tekrar boyanmalı)
        VisualThemeManager theme = FindAnyObjectByType<VisualThemeManager>();
        if (theme != null) theme.ApplyItemColor(pickup);
    }

    // ── Taşıma (server-authoritative) ────────────────────────────────────

    /// <summary>Server: taşıyıcı ata (null = serbest bırak).</summary>
    public void SetHolder(NetworkObject holder)
    {
        if (!IsServer) return;

        holderNv.Value = holder != null
            ? new NetworkObjectReference(holder)
            : default;

        if (rb != null)
        {
            rb.isKinematic = holder != null;
            rb.useGravity  = holder == null;
            if (holder == null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (TryGetComponent<Collider>(out Collider col))
            col.isTrigger = holder != null;
    }

    /// <summary>Server: tip sonradan değiştiyse ağa yaz (SetType sonrası).</summary>
    public void SyncType()
    {
        if (IsServer && IsSpawned) typeNv.Value = (int)pickup.Type;
    }

    private void Update()
    {
        // Taşıma takibi yalnız server'da — NetworkTransform yayınlar
        if (!IsSpawned || !IsServer) return;
        if (!holderNv.Value.TryGet(out NetworkObject holder)) return;

        Vector3 target = holder.TryGetComponent<PlayerInteraction>(
                             out PlayerInteraction pi)
            ? pi.HoldPointPosition
            : holder.transform.position + Vector3.up * 1.2f;

        transform.position = Vector3.Lerp(
            transform.position, target, Time.deltaTime * 20f);
        transform.rotation = Quaternion.identity;
    }

    // ── İstasyon sergi yardımcısı ────────────────────────────────────────

    /// <summary>
    /// İstasyonların "item'ı sergi noktasına koy" işlemi. Offline'da
    /// parent'lar (eski davranış); MP'de NetworkObject sahne child
    /// transform'una parent'lanamaz — pozisyona sabitlenir (kinematik,
    /// NetworkTransform client'lara taşır).
    /// </summary>
    public static void PlaceAtAnchor(GameObject item, Transform anchor,
        Vector3 localOffset)
    {
        if (item == null || anchor == null) return;

        if (IsMp)
        {
            item.transform.position = anchor.position + localOffset;
            item.transform.rotation = Quaternion.identity;
            return;
        }

        item.transform.SetParent(anchor);
        item.transform.localPosition = localOffset;
        item.transform.localRotation = Quaternion.identity;
    }

    /// <summary>Server: Instantiate edilmiş ama henüz ağa girmemiş item'ı spawn et.</summary>
    public static void EnsureSpawned(GameObject item)
    {
        if (!IsMp || !NetworkManager.Singleton.IsServer || item == null) return;

        if (item.TryGetComponent<NetworkObject>(out NetworkObject no) &&
            !no.IsSpawned)
            no.Spawn(destroyWithScene: true);
    }
}
