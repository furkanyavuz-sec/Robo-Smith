// DroneSync.cs — MP Faz 3: Tedarik drone'unun ağ köprüsü
// Görev ve sorumluluk ayrımı:
//   - Sahiplik: mavi drone host'un; kırmızı drone bağlanan misafirin
//     (server bağlantıyı görünce sahipliği devreder — park hover'ı dahil
//     tüm hareket owner makinede simüle edilir, ClientNetworkTransform taşır)
//   - Mod: owner yazar (modeNv), diğer makineler SupplyDrone'a aynalar
//     (rotor hızı, zone çarpışma kontrolü server'da doğru okur)
//   - Kapma/teslim: SERVER karar verir (item'lar server-authoritative) —
//     yük NetworkItem.SetHolder ile drone'a asılır, pozisyonu NetworkItem
//     sürer, NetworkTransform yayınlar
//   - Pilot alma: DroneConsole.Interact server'da koşar (generic ServerRpc);
//     buradaki BeginPilotClientRpc pilotun MAKİNESİNDE lokal kilidi kurar
//   - Çarpışma/eve dönüş: server tetikler, owner makine sersemleme ve
//     dönüş simülasyonunu yerelde uygular
// Kurulum: MapGenerator drone objesine NetworkObject + ClientNetworkTransform
//   ile birlikte ekler, drone/console alanlarını SetField ile bağlar.

using Unity.Netcode;
using UnityEngine;

public class DroneSync : NetworkBehaviour
{
    [Header("Bağlantılar (MapGenerator bağlar)")]
    [SerializeField] private SupplyDrone  drone;
    [SerializeField] private DroneConsole console;

    // Owner yazar (pilot/simülasyon owner makinede), herkes okur
    private readonly NetworkVariable<int> modeNv =
        new((int)SupplyDrone.DroneMode.Docked,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    private PickupItem serverCarried;   // Yük kararı yalnız server'da

    public DroneConsole Console => console;

    public override void OnNetworkSpawn()
    {
        if (drone != null) drone.AttachSync(this);
    }

    private void Update()
    {
        if (!IsSpawned || drone == null) return;

        if (IsOwner)
        {
            // Owner gerçeği → ağ (mod her karede ucuz bir int yazımı değil;
            // NGO yalnız değişince yayınlar)
            modeNv.Value = (int)drone.Mode;
        }
        else
        {
            // Ağ → yerel ayna (rotor görseli + server'daki zone kontrolleri)
            drone.SetModeFromNetwork((SupplyDrone.DroneMode)modeNv.Value);
        }

        if (IsServer) ServerTick();
    }

    // ── Server: sahiplik + kapma/teslim ──────────────────────────────────

    private void ServerTick()
    {
        EnsureRedOwnership();

        DroneRaidZone zone = DroneRaidZone.Instance;
        if (zone == null) return;

        var mode = (SupplyDrone.DroneMode)(IsOwner
            ? (int)drone.Mode : modeNv.Value);

        // Kapma: uçarken serbest ödülün üstünden geçince
        if (serverCarried == null &&
            (mode == SupplyDrone.DroneMode.Piloted ||
             mode == SupplyDrone.DroneMode.AI))
        {
            PickupItem item = zone.TryGrabNearby(
                drone.transform.position, drone.GrabRadius);
            if (item != null)
            {
                serverCarried = item;
                if (item.TryGetComponent<NetworkItem>(out NetworkItem ni))
                    ni.SetHolder(NetworkObject);
                EventZoneSync.PlaySfx(Sfx.Id.Grab, 0.5f);
            }
        }

        // Teslim: yükle kendi pedinin üstüne gelince (moddan bağımsız —
        // Returning inişi de burada yakalanır)
        if (serverCarried != null)
        {
            Vector3 a = drone.transform.position; a.y = 0f;
            Vector3 b = drone.HomePosition;       b.y = 0f;
            if (Vector3.Distance(a, b) <= drone.DeliverRadius)
                ServerDeliver(zone);
        }
    }

    private void EnsureRedOwnership()
    {
        // Kırmızı drone misafirindir — bağlanır bağlanmaz devret
        if (drone.IsPlayerTeam || OwnerClientId != NetworkManager.ServerClientId)
            return;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == NetworkManager.ServerClientId) continue;
            NetworkObject.ChangeOwnership(client.ClientId);
            Debug.Log($"[DroneSync] Kırmızı drone sahipliği client " +
                      $"{client.ClientId}'e devredildi.");
            return;
        }
    }

    private void ServerDeliver(DroneRaidZone zone)
    {
        PickupItem item = serverCarried;
        serverCarried   = null;

        zone.ReleaseReward(item);

        if (item.TryGetComponent<NetworkItem>(out NetworkItem ni))
            ni.SetHolder(null);

        // Pedin üstüne bırak — sahibi E ile alır (iki takımda da aynı)
        item.transform.position = drone.HomePosition + Vector3.up * 0.5f;
        item.transform.rotation = Quaternion.identity;

        EventZoneSync.PlaySfx(Sfx.Id.Deposit);
        EventZoneSync.Announce(
            drone.IsPlayerTeam
                ? $"TESLİMAT: {SupplyDrone.ItemName(item.Type)} MAVİ PEDDE!"
                : $"TESLİMAT: {SupplyDrone.ItemName(item.Type)} KIRMIZI PEDDE!",
            drone.IsPlayerTeam ? new Color(0.25f, 0.50f, 0.95f)
                               : new Color(0.95f, 0.32f, 0.26f), 2.5f);
    }

    // ── Server → owner köprüleri ─────────────────────────────────────────

    /// <summary>DroneConsole.Interact (server) çağırır: pilotun makinesinde
    /// lokal kilidi kur; kırmızıysa sahipliği garantile.</summary>
    public void ServerBeginPiloting(PlayerInteraction player)
    {
        if (!IsServer || player == null) return;

        NetworkObject playerNo = player.GetComponent<NetworkObject>();
        if (playerNo == null) return;

        if (playerNo.OwnerClientId != OwnerClientId)
            NetworkObject.ChangeOwnership(playerNo.OwnerClientId);

        BeginPilotClientRpc(playerNo);
    }

    [ClientRpc]
    private void BeginPilotClientRpc(NetworkObjectReference playerRef)
    {
        if (!playerRef.TryGet(out NetworkObject playerNo)) return;
        if (!playerNo.IsOwner) return;   // Kilit yalnız pilotun makinesinde

        if (console != null &&
            playerNo.TryGetComponent<PlayerInteraction>(out PlayerInteraction pi))
            console.LocalBeginPiloting(pi);
    }

    /// <summary>DroneRaidZone pencere kapatınca (server) çağırır.</summary>
    public void ServerForceReturn()
    {
        if (IsServer) ForceReturnClientRpc();
    }

    [ClientRpc]
    private void ForceReturnClientRpc()
    {
        if (!IsOwner) return;   // Simülasyon owner makinede

        // Pilot içerideyse konsol çözülür (o da drone'u eve yollar)
        if (console != null && console.InUse) console.EndPiloting();
        else                                  drone.ForceReturnLocal();
    }

    /// <summary>Zone çarpışması (server): yükü düşür + owner'da sersemlet.
    /// Dönüş: yük düştü mü (anons için).</summary>
    public bool ServerHandleRam(Vector3 knockDir)
    {
        if (!IsServer) return false;

        RamClientRpc(knockDir);

        if (serverCarried == null) return false;

        PickupItem item = serverCarried;
        serverCarried   = null;

        if (item.TryGetComponent<NetworkItem>(out NetworkItem ni))
            ni.SetHolder(null);

        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
            rb.AddForce(knockDir * 2f + Vector3.up * 1.5f, ForceMode.Impulse);

        return true;
    }

    [ClientRpc]
    private void RamClientRpc(Vector3 knockDir)
    {
        if (IsOwner) drone.ApplyRamLocal(knockDir);
    }
}
