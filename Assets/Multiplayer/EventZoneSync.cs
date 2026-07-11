// EventZoneSync.cs — MP Faz 3: Etkinlik bölgesi senkron köprüsü
// Görev: DroneRaidZone + ScrapWindowZone saatleri yalnız server'da koşar;
//   bu bileşen elapsed/windowIndex/state'i NetworkVariable ile client'lara
//   yayınlar. Client zone'ları kendi durum makinelerini bu saate göre
//   deterministik koşturur (anons/sfx lokal tetiklenir) — NV'ler yalnız
//   sapma düzeltmesidir.
// Ayrıca server-taraflı olayların (çarpışma, teslimat, depo) görsel/işitsel
//   geri bildirimi için statik relay sağlar: Announce/PlaySfx/Popup/Shake —
//   offline'da doğrudan, MP server'da ClientRpc ile (host da alır).
// Kurulum: MapGenerator "Network Game State" objesine ekler.

using Unity.Netcode;
using UnityEngine;

public class EventZoneSync : NetworkBehaviour
{
    public static EventZoneSync Instance { get; private set; }

    // Server yazar, herkes okur — 4 Hz (NetworkGameState deseni)
    private readonly NetworkVariable<float> droneElapsedNv =
        new(0f, NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> droneIndexNv =
        new(0, NetworkVariableReadPermission.Everyone,
               NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> droneStateNv =
        new(0, NetworkVariableReadPermission.Everyone,
               NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> scrapElapsedNv =
        new(0f, NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> scrapIndexNv =
        new(0, NetworkVariableReadPermission.Everyone,
               NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> scrapStateNv =
        new(0, NetworkVariableReadPermission.Everyone,
               NetworkVariableWritePermission.Server);

    private const float WRITE_INTERVAL = 0.25f;
    private float writeTimer;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            writeTimer -= Time.deltaTime;
            if (writeTimer > 0f) return;
            writeTimer = WRITE_INTERVAL;

            DroneRaidZone drone = DroneRaidZone.Instance;
            if (drone != null)
            {
                droneElapsedNv.Value = drone.Elapsed;
                droneIndexNv.Value   = drone.WindowIndex;
                droneStateNv.Value   = (int)drone.State;
            }

            ScrapWindowZone scrap = ScrapWindowZone.Instance;
            if (scrap != null)
            {
                scrapElapsedNv.Value = scrap.Elapsed;
                scrapIndexNv.Value   = scrap.WindowIndex;
                scrapStateNv.Value   = (int)scrap.State;
            }
        }
        else
        {
            DroneRaidZone.Instance?.ApplyNetworkClock(
                droneElapsedNv.Value, droneIndexNv.Value,
                (DroneRaidZone.ZoneState)droneStateNv.Value);

            ScrapWindowZone.Instance?.ApplyNetworkClock(
                scrapElapsedNv.Value, scrapIndexNv.Value,
                (ScrapWindowZone.ZoneState)scrapStateNv.Value);
        }
    }

    // ── Statik relay'ler — server olaylarının iki taraflı geri bildirimi ──
    // Offline: doğrudan lokal çağrı. MP server: ClientRpc (host dahil herkes
    // alır). MP client: sessiz (client bu olayları üretmez).

    private static bool RelayReady =>
        Instance != null && Instance.IsSpawned;

    public static void Announce(string msg, Color color, float duration)
    {
        if (!RelayReady) { RaidAnnouncer.Show(msg, color, duration); return; }
        if (Instance.IsServer)
            Instance.AnnounceClientRpc(msg, color, duration);
    }

    public static void PlaySfx(Sfx.Id id, float volume = 1f)
    {
        if (!RelayReady) { Sfx.Play(id, volume); return; }
        if (Instance.IsServer)
            Instance.SfxClientRpc((int)id, volume);
    }

    public static void Popup(Vector3 pos, string text, Color color, float scale)
    {
        if (!RelayReady) { DamagePopup.Spawn(pos, text, color, scale); return; }
        if (Instance.IsServer)
            Instance.PopupClientRpc(pos, text, color, scale);
    }

    public static void Shake(float amount)
    {
        if (!RelayReady) { CameraShake.Add(amount); return; }
        if (Instance.IsServer)
            Instance.ShakeClientRpc(amount);
    }

    [ClientRpc]
    private void AnnounceClientRpc(string msg, Color color, float duration) =>
        RaidAnnouncer.Show(msg, color, duration);

    [ClientRpc]
    private void SfxClientRpc(int id, float volume) =>
        Sfx.Play((Sfx.Id)id, volume);

    [ClientRpc]
    private void PopupClientRpc(Vector3 pos, string text, Color color,
        float scale) => DamagePopup.Spawn(pos, text, color, scale);

    [ClientRpc]
    private void ShakeClientRpc(float amount) => CameraShake.Add(amount);
}
