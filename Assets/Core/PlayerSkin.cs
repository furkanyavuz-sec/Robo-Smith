// PlayerSkin.cs — Sevimli low-poly robot karakter kaplaması
// Görev: Oyuncunun primitif görselini FreeLowPolyRobot karakteriyle değiştirir:
//   - Tema (ThemeRef.playerCharacter) üzerinden gelir; yoksa eski görünüm kalır
//   - Parça seçimi DETERMİNİSTİK (seed = OwnerClientId) — MP'de aynı oyuncu
//     iki makinede de aynı görünür
//   - Animasyon: pozisyon deltasından Run/StaticIdle geçişi (parametresiz
//     controller — CrossFade ile state adına geçilir). Remote kopyada da
//     çalışır (NetworkTransform pozisyonu oynatır, delta yeter)
//   - Randomizer'ın Space tuşu davranışı kaldırılır (yumrukla çakışır)
// Kurulum: PlayerController.Awake runtime'da iliştirir — prefab değişmez.

using UnityEngine;

public class PlayerSkin : MonoBehaviour
{
    // Histerezis: tek eşikte kalınca kare bazında idle/koşu titriyordu
    // ("takılma" görüntüsü) — açma/kapama eşiği ayrık + hız yumuşatılır
    private const float RUN_ON        = 0.8f;   // m/sn üstü → koşu
    private const float RUN_OFF       = 0.3f;   // m/sn altı → idle
    private const float MIN_SWITCH    = 0.15f;  // sn — geçişler arası bekleme
    private const float RETRY_INTERVAL = 0.5f;  // ThemeRef sahneye gelene dek

    /// <summary>-1: otomatik (OwnerClientId). TechnicianBot farklı görünüm
    /// için kendi seed'ini verir.</summary>
    public int seedOverride = -1;

    private Animator anim;
    private Vector3  lastPos;
    private bool     running;
    private bool     built;
    private float    retryTimer;
    private float    smoothSpeed;
    private float    switchTimer;

    private void Update()
    {
        if (!built)
        {
            retryTimer -= Time.deltaTime;
            if (retryTimer > 0f) return;
            retryTimer = RETRY_INTERVAL;
            TryBuild();
            return;
        }

        if (anim == null) return;

        // Hız pozisyon deltasından — girdiye değil harekete bakar (remote
        // kopyada da çalışır); yumuşatma kare titremesini yok eder
        float raw = (transform.position - lastPos).magnitude /
                    Mathf.Max(Time.deltaTime, 1e-5f);
        lastPos = transform.position;
        smoothSpeed = Mathf.Lerp(smoothSpeed, raw, Time.deltaTime * 10f);

        switchTimer -= Time.deltaTime;
        if (switchTimer > 0f) return;

        if (!running && smoothSpeed > RUN_ON)
        {
            running = true;
            switchTimer = MIN_SWITCH;
            anim.CrossFade("Run", 0.12f);
        }
        else if (running && smoothSpeed < RUN_OFF)
        {
            running = false;
            switchTimer = MIN_SWITCH;
            anim.CrossFade("StaticIdle", 0.15f);
        }
    }

    private void TryBuild()
    {
        MapTheme th = ThemeRef.Current;
        if (th == null || th.playerCharacter == null) return;   // Lobby/temasız
        if (transform.Find("Skin") != null) { built = true; return; }

        // Deterministik parça seçimi: randomizer Awake'te Random kullanır —
        // seed'i oyuncu kimliğine sabitle, sonra RNG durumunu geri koy
        int seed = 12345;
        if (seedOverride >= 0)
            seed = seedOverride;
        else if (TryGetComponent<Unity.Netcode.NetworkObject>(
                out Unity.Netcode.NetworkObject no) && no.IsSpawned)
            seed = (int)no.OwnerClientId + 1000;

        Random.State saved = Random.state;
        Random.InitState(seed);
        GameObject skin = Instantiate(th.playerCharacter, transform);
        Random.state = saved;

        skin.name = "Skin";

        // Space tuşu randomizer'ı ve collider'lar oyunla çakışmasın
        foreach (MonoBehaviour mb in skin.GetComponentsInChildren<MonoBehaviour>())
            if (mb.GetType().Name == "ModularRobotRandomizer") Destroy(mb);
        foreach (Collider c in skin.GetComponentsInChildren<Collider>())
            Destroy(c);

        // Paket shader'ı URP'de mor kalıyor — atlas dokulu Lit materyali bas
        if (th.playerCharacterMaterial != null)
            foreach (Renderer r in skin.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = th.playerCharacterMaterial;
                r.sharedMaterials = mats;
            }

        // Eski primitif görseli gizle (skin hariç; sonradan eklenen taşınan
        // item'lar etkilenmez — o anki renderer'lar toplanır)
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (!r.transform.IsChildOf(skin.transform)) r.enabled = false;

        // Boyutla: kapsül yüksekliğine uniform sığdır, tabana otur
        float targetH = TryGetComponent<CapsuleCollider>(
            out CapsuleCollider cap) ? cap.height : 1.8f;

        Renderer[] rends = skin.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float s = targetH / Mathf.Max(b.size.y, 0.01f);
            skin.transform.localScale *= s;

            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float bottom = cap != null
                ? transform.TransformPoint(cap.center).y - targetH / 2f
                : transform.position.y - targetH / 2f;
            skin.transform.position += new Vector3(
                transform.position.x - b.center.x,
                bottom - b.min.y,
                transform.position.z - b.center.z);
        }

        anim    = skin.GetComponentInChildren<Animator>();
        lastPos = transform.position;
        if (anim != null)
        {
            // Root motion animasyonu karakteri kendi oynatmaya çalışıyordu —
            // hareket PlayerController/bot'un işi; animasyon salt görsel
            anim.applyRootMotion = false;
            anim.Play("StaticIdle");
        }
        built = true;
    }
}
