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

    // ── MP Faz 3: Hurdalık yumruğu ───────────────────────────────────────
    // Girdi + swing görseli PlayerMelee'de lokal; isabet kararı burada
    // server'da (pozisyon/rotasyon ClientNetworkTransform ile senkron),
    // sersemleme kurbanın SAHİBİ makinede uygulanır (hareket owner-auth).
    // PlayerMelee runtime'da eklenir, NetworkBehaviour olamaz — RPC'ler
    // prefab'da hazır duran bu bileşende yaşar.

    // PlayerMelee.punchRange / punchRadius ile aynı denge değerleri
    private const float PUNCH_RANGE  = 1.3f;
    private const float PUNCH_RADIUS = 1.1f;

    [ServerRpc]
    public void PunchServerRpc()
    {
        Vector3 hitCenter = transform.position + transform.forward * PUNCH_RANGE;

        foreach (Collider col in Physics.OverlapSphere(hitCenter, PUNCH_RADIUS))
        {
            if (!col.TryGetComponent<NetworkPlayer>(out NetworkPlayer other) ||
                other == this) continue;

            Vector3 dir = other.transform.position - transform.position;
            dir.y = 0f;

            // Elindekini server düşürür, sersemleme sahibine gider
            other.GetComponent<PlayerInteraction>()?.ForceDropFromStation();
            other.ReceivePunchClientRpc(dir.normalized);
            return;
        }
    }

    [ClientRpc]
    private void ReceivePunchClientRpc(Vector3 knockDir)
    {
        if (IsOwner)
        {
            if (TryGetComponent<PlayerMelee>(out PlayerMelee melee))
                melee.ReceivePunch(knockDir);
        }
        else
        {
            // Uzak kopya: yalnız görsel geri bildirim (hareketi sahibi taşır)
            DamagePopup.Spawn(transform.position, "SERSEMLEDİ!",
                new Color(0.95f, 0.45f, 0.15f), 1.1f);
        }
    }

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

        // Lobby sahnesinde zemin yok — oyuncu sonsuz düşüşe geçmesin:
        // oyun sahnesine yerleşene kadar fiziği dondur. (Düşüşte biriken
        // hız, garaja ışınlamadan sonra oyuncuyu aşağı çekiyordu.)
        if (SceneManager.GetActiveScene().name != GameSceneName &&
            TryGetComponent<Rigidbody>(out Rigidbody ownRb))
            ownRb.isKinematic = true;

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
                TeleportTo(spawn.transform.position);
                Debug.Log($"[NetworkPlayer] Takım spawn'ına taşındı: {spawnName}");
                yield break;
            }
            yield return new WaitForSeconds(0.3f);
        }

        Debug.LogWarning($"[NetworkPlayer] '{spawnName}' bulunamadı — " +
                         "haritayı Generate Map ile üretmeyi unutma.");

        // Spawn yoksa bile oyuncuyu dondurulmuş bırakma — harita merkezine koy
        TeleportTo(Vector3.up * 2f);
    }

    /// <summary>
    /// Rigidbody'li güvenli ışınlama: lobby dondurması açılır, düşüşte
    /// biriken hız sıfırlanır, fizik gövdesi transform'la birlikte taşınır
    /// (yalnız transform taşımak fizik motorunun eski pozisyona/hıza geri
    /// çekmesine yol açıyordu).
    /// </summary>
    private void TeleportTo(Vector3 pos)
    {
        if (TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic     = false;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position        = pos;
        }

        transform.position = pos;
    }
}