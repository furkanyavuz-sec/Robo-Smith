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
