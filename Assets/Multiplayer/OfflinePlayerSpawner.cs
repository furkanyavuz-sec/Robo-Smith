// OfflinePlayerSpawner.cs
// Görev: Sahne NGO olmadan (tekli oyun / doğrudan Play) çalıştırıldığında
// oyuncuyu spawn eder. Multiplayer'da NetworkManager player prefabını
// kendisi spawn ettiği için bu script hiçbir şey yapmaz.

using Unity.Netcode;
using UnityEngine;

public class OfflinePlayerSpawner : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform  spawnPoint;   // Boşsa bu objenin pozisyonu

    private void Start()
    {
        // MP modunda NGO spawn eder — karışma
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;

        // Sahnede zaten bir oyuncu varsa ikinci kez spawn etme
        if (FindAnyObjectByType<PlayerController>() != null) return;

        if (playerPrefab == null)
        {
            Debug.LogError("[OfflinePlayerSpawner] playerPrefab atanmamış!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);

        // Kamerayı offline oyuncuya kilitle
        CameraController.Instance?.SetTarget(player.transform);

        Debug.Log("[OfflinePlayerSpawner] Offline oyuncu spawn edildi.");
    }
}
