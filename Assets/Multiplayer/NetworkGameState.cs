// NetworkGameState.cs — MP Faz 1: Server-authoritative faz + timer senkronu
// Görev: Hazırlık/arena timer'ı yalnız server'da (host) koşar; bu bileşen
//   faz ve kalan süreyi NetworkVariable ile client'lara yayınlar.
//   Client tarafında GameManager kendi timer'ını koşturmaz — buradan okur.
// Kurulum: MapGenerator "Generate Map" sırasında NetworkObject'li sahne
//   objesi olarak üretir (in-scene placed NetworkObject; NGO sahne
//   yüklenince server'da otomatik spawn eder, client'la eşleştirir).
// Offline: NGO dinlemiyorken hiçbir şey yapmaz — tekli oyun aynen çalışır.

using Unity.Netcode;
using UnityEngine;

public class NetworkGameState : NetworkBehaviour
{
    public static NetworkGameState Instance { get; private set; }

    // Server yazar, herkes okur. Timer her karede değişir — trafiği boğmamak
    // için server 4 Hz yazar; client aradaki kareleri kendisi tahmin eder.
    private readonly NetworkVariable<float> syncedTimer =
        new(600f, NetworkVariableReadPermission.Everyone,
                  NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> syncedPhase =
        new((int)GamePhase.Preparation,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private const float WRITE_INTERVAL = 0.25f;
    private float writeTimer;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[NetworkGameState] ✅ Senkron köprüsü aktif " +
                  $"(IsServer={IsServer}) — timer server'dan yayınlanıyor.");

        // NGO bazı akışlarda lobby'de spawn olan oyuncuyu yeni sahneye
        // taşımıyor — server, oyuncu objesi kayıp client'ları onarır.
        if (IsServer) StartCoroutine(EnsurePlayersSpawned());
    }

    private System.Collections.IEnumerator EnsurePlayersSpawned()
    {
        yield return null;   // Spawn/migrasyon işlemleri otursun

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) yield break;

        GameObject prefab = nm.NetworkConfig.PlayerPrefab;

        foreach (NetworkClient client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.IsSpawned)
                continue;

            if (prefab == null)
            {
                Debug.LogError("[NetworkGameState] PlayerPrefab atanmamış — " +
                               "NetworkManager ayarlarını kontrol et!");
                yield break;
            }

            GameObject obj = Instantiate(prefab);
            obj.GetComponent<NetworkObject>()
               .SpawnAsPlayerObject(client.ClientId, destroyWithScene: true);

            Debug.Log($"[NetworkGameState] 🔧 Kayıp oyuncu yeniden spawn " +
                      $"edildi: client {client.ClientId}");
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned || GameManager.Instance == null) return;

        if (IsServer)
        {
            // Server: GameManager gerçeği → ağ değişkenleri
            writeTimer -= Time.deltaTime;
            if (writeTimer > 0f) return;
            writeTimer = WRITE_INTERVAL;

            syncedTimer.Value = GameManager.Instance.PhaseTimer;
            syncedPhase.Value = (int)GameManager.Instance.CurrentPhase;
        }
        else
        {
            // Client: ağ değişkenleri → GameManager (yerel timer koşmaz)
            GameManager.Instance.ApplyNetworkState(
                (GamePhase)syncedPhase.Value, syncedTimer.Value);
        }
    }
}
