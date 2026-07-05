using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkPlayer : NetworkBehaviour
{
    // NGO oyuncuyu LOBBY sahnesinde spawn eder; oyun sahnesine geçişte
    // NGO objeyi taşır ama garaja yerleştirmek ve yeni sahnenin kamerasını
    // kilitlemek bize düşer — sahne yüklenmelerini dinleriz.
    private const string GameSceneName = "SampleScene";

    [Header("Kapatılacak Lokal Bileşenler")]
    [SerializeField] private MonoBehaviour[] localScriptsToDisable;
    [SerializeField] private Behaviour[] otherComponentsToDisable; // NavMeshAgent, AudioListener vb.

    /// <summary>Host (server sahibi) Mavi takımdır, misafir Kırmızı.</summary>
    public bool IsBlueTeam => OwnerClientId == NetworkManager.ServerClientId;

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

        // MP Faz 1: takım garajında doğ (hareket owner-authoritative —
        // pozisyonu sahibi taşır). Lobby'de spawn olursak sahne geçişini
        // sceneLoaded aboneliği yakalar.
        SceneManager.sceneLoaded += OnSceneLoadedOwner;
        StartCoroutine(MoveToTeamSpawn());
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

    public override void OnNetworkDespawn()
    {
        if (IsOwner) SceneManager.sceneLoaded -= OnSceneLoadedOwner;
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Lobby'den oyun sahnesine geçince: kamerayı yeni sahnenin
    /// CameraController'ına yeniden kilitle + takım garajına ışınlan.
    /// </summary>
    private void OnSceneLoadedOwner(Scene scene, LoadSceneMode mode)
    {
        if (!IsOwner || scene.name != GameSceneName) return;
        StartCoroutine(ClaimSceneSetup());
    }

    private System.Collections.IEnumerator ClaimSceneSetup()
    {
        yield return null;   // Yeni sahnenin objeleri (kamera, harita) otursun

        if (CameraController.Instance != null)
            CameraController.Instance.SetTarget(transform);

        yield return MoveToTeamSpawn();
    }

    /// <summary>
    /// Takım spawn noktasını bulup oraya taşınır. MapGenerator'ın ürettiği
    /// "PlayerSpawn [Mavi/Kırmızı]" objeleri sahne yüklenince var olur;
    /// spawn sırası yarışına karşı kısa aralıklarla birkaç kez dener.
    /// Lobby sahnesindeyken sessizce çıkar (spawn noktaları orada yok).
    /// </summary>
    private System.Collections.IEnumerator MoveToTeamSpawn()
    {
        if (SceneManager.GetActiveScene().name != GameSceneName)
            yield break;   // Menü/lobby — oyun sahnesine geçince tekrar denenir

        string spawnName = IsBlueTeam ? "PlayerSpawn [Mavi]"
                                      : "PlayerSpawn [Kırmızı]";

        for (int attempt = 0; attempt < 10; attempt++)
        {
            GameObject spawn = GameObject.Find(spawnName);
            if (spawn != null)
            {
                transform.position = spawn.transform.position;
                Debug.Log($"[NetworkPlayer] Takım spawn'ına taşındı: {spawnName}");
                yield break;
            }
            yield return new WaitForSeconds(0.3f);
        }

        Debug.LogWarning($"[NetworkPlayer] '{spawnName}' bulunamadı — " +
                         "haritayı Generate Map ile üretmeyi unutma.");
    }
}