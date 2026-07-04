// TechnicianBot.cs — Rakip teknisyen (Hurdalık Penceresi'nin görünür rakibi)
// Görev: Pencere açılınca kırmızı garajdan çıkar, yerdeki yağmayı toplar,
//   yağma bitince ham madde istasyonlarından hasat eder, topladığını eve
//   taşır → DirectorAI üretim hızlanması alır (ReceiveScrapDelivery).
//   Hard'da malzeme taşıyan oyuncuya yumruk atar (sersemletir + düşürtür).
// Zorluk ayarları kod içinde — DirectorAI.GetTuning ile aynı felsefe.
// Kurulum: MapGenerator kırmızı garaj kapısına üretir, görselini kendi kurar.

using System.Collections.Generic;
using UnityEngine;

public class TechnicianBot : MonoBehaviour
{
    // ── Zorluk tablosu (tek denge noktası) ───────────────────────────────
    private struct BotTuning
    {
        public float speed;         // Yürüme hızı (oyuncu: 6.5)
        public float reactDelay;    // Pencere açılınca çıkış gecikmesi
        public float harvestTime;   // İstasyondan hasat süresi
        public bool  punches;       // Taşıyan oyuncuya yumruk atar mı
    }

    private static BotTuning GetTuning(Difficulty d) => d switch
    {
        Difficulty.Easy => new BotTuning
            { speed = 4.0f, reactDelay = 5f, harvestTime = 3.5f, punches = false },
        Difficulty.Hard => new BotTuning
            { speed = 6.2f, reactDelay = 1f, harvestTime = 1.8f, punches = true },
        _ => new BotTuning   // Normal
            { speed = 5.2f, reactDelay = 2.5f, harvestTime = 2.5f, punches = false },
    };

    [Header("Bağlantılar (MapGenerator bağlar)")]
    [SerializeField] private Vector3 homePosition;   // Kırmızı kapı ağzı (dünya)

    private enum BotState { Home, Collect, Harvest, CarryHome }

    private BotTuning tuning;
    private BotState  state = BotState.Home;
    private float     launchTimer;
    private bool      launched;
    private float     harvestTimer;
    private float     punchCooldown;
    private float     stunTimer;

    private PickupItem carried;
    private ScrapyardStation targetStation;
    private Transform  hand;
    private Transform  body;
    private float      bobPhase;

    private readonly List<ScrapyardStation> zoneStations = new();

    private const float PUNCH_RANGE    = 1.4f;
    private const float PUNCH_COOLDOWN = 2.5f;
    private const float STUN_DURATION  = 1.5f;

    private void Start()
    {
        // MP'de rakip gerçek oyuncu — bot sahneden kalkar (Faz 3 notu)
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            gameObject.SetActive(false);
            return;
        }

        Difficulty diff = MatchData.Instance != null
            ? MatchData.Instance.SelectedDifficulty
            : Difficulty.Normal;
        tuning = GetTuning(diff);

        if (transform.Find("Govde") == null) BuildVisual();
        body = transform.Find("Govde");
        hand = transform.Find("El");

        transform.position = homePosition;

        // Pencere bölgesindeki ham madde istasyonlarını ezberle (hasat için)
        if (ScrapWindowZone.Instance != null)
            foreach (ScrapyardStation s in
                     FindObjectsByType<ScrapyardStation>(FindObjectsSortMode.None))
                if (ScrapWindowZone.Instance.IsInside(s.transform.position))
                    zoneStations.Add(s);
    }

    private void Update()
    {
        punchCooldown -= Time.deltaTime;
        bobPhase      += Time.deltaTime;
        Bob();

        // Sersemleme — hareket yok
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            return;
        }

        ScrapWindowZone zone = ScrapWindowZone.Instance;
        if (zone == null) return;

        if (!zone.IsOpen)
        {
            // Pencere kapalı: kalkışı sıfırla, eve yürü
            launched    = false;
            launchTimer = tuning.reactDelay;

            // Elde kalan yağma eve dönerken teslim edilir
            if (state != BotState.Home || carried != null)
                WalkHome(zone);
            return;
        }

        // Kalkış gecikmesi — zorluk hissi
        if (!launched)
        {
            launchTimer -= Time.deltaTime;
            if (launchTimer > 0f) return;
            launched = true;
            state    = BotState.Collect;
        }

        // Hard: taşıyan oyuncuyu yumrukla (fırsatçı — yol üstündeyse)
        if (tuning.punches) TryPunchPlayer(zone);

        switch (state)
        {
            case BotState.Collect:   DoCollect(zone);   break;
            case BotState.Harvest:   DoHarvest(zone);   break;
            case BotState.CarryHome: DoCarryHome(zone); break;
            case BotState.Home:      state = BotState.Collect; break;
        }
    }

    // ── Davranışlar ──────────────────────────────────────────────────────

    private void DoCollect(ScrapWindowZone zone)
    {
        // Öncelik: yerdeki serbest yağma
        PickupItem best = null;
        float bestDist  = float.MaxValue;
        foreach (PickupItem l in zone.FreeLoot())
        {
            float dist = FlatDistance(transform.position, l.transform.position);
            if (dist < bestDist) { bestDist = dist; best = l; }
        }

        if (best != null)
        {
            MoveToward(best.transform.position);
            if (bestDist < 1.1f) Grab(best);
            return;
        }

        // Yağma bitti: rastgele istasyondan hasat et
        if (zoneStations.Count > 0)
        {
            if (targetStation == null)
                targetStation = zoneStations[Random.Range(0, zoneStations.Count)];

            MoveToward(targetStation.transform.position);
            if (FlatDistance(transform.position,
                             targetStation.transform.position) < 1.4f)
            {
                state        = BotState.Harvest;
                harvestTimer = tuning.harvestTime;
            }
        }
    }

    private void DoHarvest(ScrapWindowZone zone)
    {
        if (targetStation == null) { state = BotState.Collect; return; }

        harvestTimer -= Time.deltaTime;
        if (harvestTimer > 0f) return;

        PickupItem item = zone.SpawnLoot(targetStation.SupplyType,
            transform.position + Vector3.up * 0.5f);
        targetStation = null;

        if (item != null) Grab(item);
        else              state = BotState.Collect;
    }

    private void DoCarryHome(ScrapWindowZone zone)
    {
        if (carried == null) { state = BotState.Collect; return; }

        // Pencere açıkken kapan kapalı — eve değil, içerideki depoya taşır
        Vector3 target = zone.IsOpen ? zone.RedDepotPosition : homePosition;

        MoveToward(target);
        if (FlatDistance(transform.position, target) < 1.2f)
        {
            if (zone.IsOpen)
            {
                zone.DepositFromBot(carried);
                carried = null;
                state   = BotState.Collect;
            }
            else
            {
                Deliver(zone);
            }
        }
    }

    private void WalkHome(ScrapWindowZone zone)
    {
        MoveToward(homePosition);
        if (FlatDistance(transform.position, homePosition) < 1f)
        {
            if (carried != null) Deliver(zone);
            state = BotState.Home;
        }
    }

    // ── Yağma taşıma ─────────────────────────────────────────────────────

    private void Grab(PickupItem item)
    {
        carried = item;

        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
        if (item.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = true;

        // Huzmeyi söndür
        Transform beam = item.transform.Find("Beam");
        if (beam != null) Destroy(beam.gameObject);

        item.transform.SetParent(hand != null ? hand : transform);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        state = BotState.CarryHome;
    }

    private void Deliver(ScrapWindowZone zone)
    {
        if (carried == null) return;

        zone.ReleaseLoot(carried);
        RaidAnnouncer.Show($"RAKİP TEKNİSYEN MALZEME KAÇIRDI!",
            new Color(0.95f, 0.32f, 0.26f), 2f);

        FindFirstObjectByType<DirectorAI>()?.ReceiveScrapDelivery();

        Destroy(carried.gameObject);
        carried = null;
        state   = BotState.Collect;
    }

    // ── Dövüş ────────────────────────────────────────────────────────────

    private void TryPunchPlayer(ScrapWindowZone zone)
    {
        if (punchCooldown > 0f) return;

        foreach (PlayerInteraction pi in
                 FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
        {
            if (pi.HeldObject == null) continue;                 // Eli boşsa değmez
            if (!zone.IsInside(pi.transform.position)) continue; // Bölge dışı dokunulmaz
            if (FlatDistance(transform.position, pi.transform.position)
                > PUNCH_RANGE) continue;

            PlayerMelee melee = pi.GetComponent<PlayerMelee>();
            if (melee == null) continue;

            punchCooldown = PUNCH_COOLDOWN;
            Vector3 dir = pi.transform.position - transform.position;
            dir.y = 0f;
            melee.ReceivePunch(dir.normalized);
            return;
        }
    }

    /// <summary>Oyuncu yumruğu: sersemle + taşıdığını düşür.</summary>
    public void ReceivePunch(Vector3 knockDir)
    {
        stunTimer = STUN_DURATION;
        transform.position += knockDir * 0.8f;

        Sfx.Play(Sfx.Id.Hit);
        CameraShake.Add(0.15f);   // İsabet hissi — vuran oyuncuya geri bildirim

        DamagePopup.Spawn(transform.position, "SERSEMLEDİ!",
            new Color(0.95f, 0.45f, 0.15f), 1.1f);

        if (carried == null) return;

        // Taşıdığı yağma yere düşer — kapan alır
        PickupItem item = carried;
        carried = null;
        state   = BotState.Collect;

        item.transform.SetParent(null);
        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
            rb.AddForce(knockDir * 2f + Vector3.up * 1.5f, ForceMode.Impulse);
        }
        if (item.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = false;
    }

    // ── Hareket & Görsel ─────────────────────────────────────────────────

    private void MoveToward(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.magnitude < 0.05f) return;
        dir.Normalize();

        transform.position += dir * tuning.speed * Time.deltaTime;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, 540f * Time.deltaTime);
    }

    private void Bob()
    {
        if (body == null) return;
        Vector3 pos = body.localPosition;
        pos.y = 0.85f + Mathf.Sin(bobPhase * 3f) * 0.05f;
        body.localPosition = pos;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>MapGenerator üretim sırasında da çağırabilir.</summary>
    public void BuildVisual()
    {
        Color red  = new Color(0.95f, 0.32f, 0.26f);
        Color dark = new Color(0.16f, 0.17f, 0.20f);

        // Gövde — süzülen kapsül
        GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyObj.name = "Govde";
        PrepPart(bodyObj, transform, new Vector3(0f, 0.85f, 0f),
            new Vector3(0.55f, 0.45f, 0.55f), dark, keepCollider: false);

        // Vizör — takım kimliği
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Vizor";
        PrepPart(visor, bodyObj.transform, new Vector3(0f, 0.55f, 0.65f),
            new Vector3(0.75f, 0.22f, 0.25f), red, keepCollider: false);

        // Süzülme halkası — oyuncudaki hover diliyle aynı
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Halka";
        PrepPart(ring, transform, new Vector3(0f, 0.12f, 0f),
            new Vector3(0.85f, 0.04f, 0.85f), red, keepCollider: false);

        // El — taşınan yağma buraya asılır
        GameObject handObj = new GameObject("El");
        handObj.transform.SetParent(transform);
        handObj.transform.localPosition = new Vector3(0f, 0.6f, 0.55f);

        // Yumruk algılama gövdesi — oyuncunun OverlapSphere'i bunu bulur
        CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
        col.center    = new Vector3(0f, 0.9f, 0f);
        col.height    = 1.8f;
        col.radius    = 0.45f;
        col.isTrigger = true;   // Fiziği itmesin, sadece algılansın
    }

    private static void PrepPart(GameObject part, Transform parent,
        Vector3 localPos, Vector3 scale, Color color, bool keepCollider)
    {
        if (!keepCollider && part.TryGetComponent<Collider>(out Collider col))
        {
            if (Application.isPlaying) Destroy(col);
            else                       DestroyImmediate(col);
        }
        part.transform.SetParent(parent);
        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale    = scale;
        part.GetComponent<Renderer>().sharedMaterial =
            StationVisuals.GetMaterial(color);
    }
}
