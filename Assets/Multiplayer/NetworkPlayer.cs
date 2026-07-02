using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Kapatılacak Lokal Bileşenler")]
    [SerializeField] private MonoBehaviour[] localScriptsToDisable;
    [SerializeField] private Behaviour[] otherComponentsToDisable; // NavMeshAgent, AudioListener vb.

    public override void OnNetworkSpawn()
{
    // Eğer bu obje BİZE aitse (Local Player ise)
    if (IsOwner)
    {
        Debug.Log($"[NetworkPlayer] Kendi robotumuz ağda doğdu: {name}");
        
        // 🌟 KAMERAYI OTOMATİK OLARAK KENDİMİZE KİLİTLİYORUZ!
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetTarget(transform);
        }
    }
    else
    {
        // Eğer bu obje BAŞKASINA aitse controls ve local scriptleri kapat
        Debug.Log($"[NetworkPlayer] Diğer oyuncunun robotu ağda belirdi: {name}. Kontrolleri kapatılıyor.");

        foreach (var script in localScriptsToDisable)
        {
            if (script != null) script.enabled = false;
        }

        foreach (var comp in otherComponentsToDisable)
        {
            if (comp != null) comp.enabled = false;
        }

        // Uzak kopyanın pozisyonunu ClientNetworkTransform yönetir —
        // lokal fizik simülasyonu onunla çakışmasın
        if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            rb.isKinematic = true;
    }
}
}