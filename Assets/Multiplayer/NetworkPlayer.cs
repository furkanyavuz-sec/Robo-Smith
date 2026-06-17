// NetworkPlayer.cs
// Görev: Oyuncunun NetworkBehaviour versiyonu.
// MonoBehaviour'dan NGO'ya geçiş köprüsü.
// Her client sadece kendi objesini kontrol eder (IsOwner).

using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private PlayerController    controller;
    [SerializeField] private PlayerInteraction   interaction;
    [SerializeField] private CameraController    cameraController;

    // NetworkVariable: tüm clientlar görür, sadece server yazar
    public NetworkVariable<int> TeamID = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> PlayerScore = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
{
    if (!IsOwner)
    {
        // Başkasının oyuncusu — bileşenleri kapat
        if (controller  != null) controller.enabled  = false;
        if (interaction != null) interaction.enabled = false;
        if (cameraController != null) cameraController.enabled = false;
        return;
    }

    // Kamerayı bu oyuncuya bağla
    if (cameraController != null) cameraController.enabled = true;

    Debug.Log($"[NetworkPlayer] Oyuncu spawn oldu. " +
              $"ClientID: {OwnerClientId} | IsOwner: {IsOwner}");
}

    // ── Takım Atama (Server tarafından çağrılır) ─────────────────────────

    [ServerRpc(RequireOwnership = false)]
    public void RequestTeamAssignmentServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int   count    = NetworkManager.ConnectedClients.Count;

        // İlk 3 oyuncu Takım 0, sonraki 3 oyuncu Takım 1
        int teamId = count <= 3 ? 0 : 1;
        TeamID.Value = teamId;

        Debug.Log($"[NetworkPlayer] Client {clientId} → Takım {teamId}");
    }
}