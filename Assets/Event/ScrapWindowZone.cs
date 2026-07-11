// ScrapWindowZone.cs — Hurdalık Penceresi (yaya yağma etkinliği) yöneticisi
// Görev: Hazırlıkta drone pencereleriyle DÖNÜŞÜMLÜ zamanlarda hurdalığın
//   orta şeridini açar: bariyerler gömülür, yere yağma malzemeleri saçılır,
//   oyuncu + rakip teknisyen içeri girip toplar/dövüşür. Süre bitince
//   bariyerler kalkar, içeride kalanlar kapı ağzına ışınlanır, serbest
//   yağma temizlenir.
// Not: Bariyerlerin COLLIDER'ı vardır (drone bariyerlerinin aksine) —
//   oyuncu fiziksel olarak engellenir; drone 4.2'de uçtuğu için üstünden geçer.
// Kurulum: MapGenerator.BuildScrapyard üretir, alanları SetField ile bağlar.

using System.Collections.Generic;
using UnityEngine;

public class ScrapWindowZone : MonoBehaviour
{
    public static ScrapWindowZone Instance { get; private set; }

    public enum ZoneState { Closed, Announcing, Open }

    [Header("Pencere Zamanlaması (hazırlık başından itibaren sn)")]
    // Drone raid 2/5/8. dk'da — bunlar araya girer: ~1.5 dk'da bir olay
    [SerializeField] private float[] windowOpenTimes = { 210f, 390f };
    [SerializeField] private float windowDuration = 75f;
    [SerializeField] private float announceLead   = 25f;

    [Header("Yağma")]
    [SerializeField] private GameObject lootPrefab;   // ScrapMetal_Prefab (PickupItem)
    [SerializeField] private int lootCount = 8;

    [Header("Giriş Fazı — kapan mekaniği")]
    // Kapılar sadece pencerenin ilk saniyelerinde açık: giren, süre bitene
    // kadar içeride kilitli kalır; dışarıda kalan sonraki pencereyi bekler.
    [SerializeField] private float entryDuration = 10f;

    [Header("Takım Depoları (MapGenerator bağlar)")]
    // İçeride toplanan malzeme buraya bırakılır — rakip erişemez.
    [SerializeField] private Transform blueDepotAnchor;
    [SerializeField] private Transform redDepotAnchor;
    [SerializeField] private float depotRadius = 1.7f;

    [Header("Bölge Geometrisi (MapGenerator bağlar — dünya koordinatı)")]
    [SerializeField] private Vector4 zoneRect;         // minX, maxX, minZ, maxZ
    [SerializeField] private Vector3 blueEvictPoint;   // Mavi kapı ağzı
    [SerializeField] private Vector3 redEvictPoint;    // Kırmızı kapı ağzı

    [Header("Bariyerler (MapGenerator bağlar)")]
    [SerializeField] private Transform[] barriers;

    // ── Çalışma durumu ───────────────────────────────────────────────────
    private float elapsed;
    private int   windowIndex;
    private bool  tenSecondsWarned;
    private bool  gatesClosedAnnounced;
    private float entryTimer;
    private float meleeSetupTimer;

    private readonly List<PickupItem> loot      = new();
    private readonly List<PickupItem> blueDepot = new();
    private readonly List<PickupItem> redDepot  = new();
    private readonly List<Transform>  lockedOccupants = new();
    private float[] barrierClosedY;
    private float driftTimer;          // MP client: NV ile yerel durum farkı
    private float lastNetElapsed = -1f;

    private const float BARRIER_SINK       = 4.5f;
    private const float BARRIER_ANIM_SPEED = 3.5f;

    public ZoneState State { get; private set; } = ZoneState.Closed;
    public bool IsOpen     => State == ZoneState.Open;

    /// <summary>EventZoneSync okur (server → client saat yayını).</summary>
    public float Elapsed     => elapsed;
    public int   WindowIndex => windowIndex;

    // MP Faz 3: yağma/depo/temizlik server'da; kapan kilidi + FPV + evict
    // her makinede KENDİ lokal oyuncusu için koşar (hareket owner-auth).
    private static bool Mp =>
        Unity.Netcode.NetworkManager.Singleton != null &&
        Unity.Netcode.NetworkManager.Singleton.IsListening;
    private static bool Authority =>
        !Mp || Unity.Netcode.NetworkManager.Singleton.IsServer;

    /// <summary>Kapılar sadece giriş fazında iner; sonra kapan kapanır.</summary>
    public bool GatesOpen  => IsOpen && entryTimer > 0f;

    /// <summary>Rakip teknisyenin depo hedefi.</summary>
    public Vector3 RedDepotPosition => redDepotAnchor != null
        ? redDepotAnchor.position
        : redEvictPoint;

    /// <summary>Bekleyen pencerelere kalan süreler — EventTimelineHUD için.</summary>
    public IEnumerable<float> UpcomingWindows()
    {
        for (int i = windowIndex; i < windowOpenTimes.Length; i++)
        {
            float remain = windowOpenTimes[i] - elapsed;
            if (remain > 0f) yield return remain;
        }
    }

    /// <summary>Açık pencerenin kapanmasına kalan süre (açık değilse -1).</summary>
    public float OpenTimeRemaining => IsOpen && windowIndex < windowOpenTimes.Length
        ? Mathf.Max(0f, windowOpenTimes[windowIndex] + windowDuration - elapsed)
        : -1f;

    public bool IsInside(Vector3 pos) =>
        pos.x > zoneRect.x && pos.x < zoneRect.y &&
        pos.z > zoneRect.z && pos.z < zoneRect.w;

    private void Awake()
    {
        Instance = this;
        EventTimelineHUD.Ensure();

        if (barriers != null)
        {
            barrierClosedY = new float[barriers.Length];
            for (int i = 0; i < barriers.Length; i++)
                if (barriers[i] != null)
                    barrierClosedY[i] = barriers[i].position.y;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentPhase != GamePhase.Preparation)
        {
            if (State != ZoneState.Closed) CloseWindow(silent: true);
            return;
        }

        // Client: saat NV düzeltmesiyle akar (ApplyNetworkClock), aradaki
        // kareleri yerel deltaTime doldurur — anonslar deterministik yerel
        elapsed += Time.deltaTime;

        EnsurePlayerMelee();
        UpdateWindowState();
        AnimateBarriers();

        if (IsOpen)
        {
            if (Authority) CheckPlayerDeposit();   // Item'lar server'ın
            KeepOccupantsInside();                 // Kilit lokal oyuncuya
        }
    }

    /// <summary>
    /// MP client: EventZoneSync her karede çağırır. Elapsed yalnız NV
    /// tazelenince yazılır (4 Hz); durum/pencere farkı 1 sn sürerse
    /// sessizce server değerine çekilir.
    /// </summary>
    public void ApplyNetworkClock(float netElapsed, int netIndex,
        ZoneState netState)
    {
        if (!Mathf.Approximately(netElapsed, lastNetElapsed))
        {
            lastNetElapsed = netElapsed;
            elapsed        = netElapsed;
        }

        if (netIndex == windowIndex && netState == State)
        {
            driftTimer = 0f;
            return;
        }

        driftTimer += Time.deltaTime;
        if (driftTimer < 1f) return;

        driftTimer  = 0f;
        windowIndex = netIndex;
        State       = netState;
        if (State == ZoneState.Open) CaptureOccupants();   // Kapana katıl
    }

    // ── Pencere durum makinesi ───────────────────────────────────────────

    private void UpdateWindowState()
    {
        if (windowIndex >= windowOpenTimes.Length) return;

        float openT  = windowOpenTimes[windowIndex];
        float closeT = openT + windowDuration;

        switch (State)
        {
            case ZoneState.Closed:
                if (elapsed >= openT - announceLead)
                {
                    State = ZoneState.Announcing;
                    RaidAnnouncer.Show(
                        $"HURDALIK {Mathf.RoundToInt(announceLead)} SANİYE " +
                        "SONRA AÇILIYOR\n<size=60%>Kapılar sadece ilk " +
                        $"{Mathf.RoundToInt(entryDuration)} sn açık — giren süre " +
                        "bitene kadar içeride kalır!</size>",
                        new Color(0.95f, 0.85f, 0.10f), 4f);
                }
                break;

            case ZoneState.Announcing:
                if (elapsed >= openT)
                {
                    State = ZoneState.Open;
                    tenSecondsWarned     = false;
                    gatesClosedAnnounced = false;
                    entryTimer           = entryDuration;
                    if (Authority) ScatterLoot();   // Yağma ağdan gelir
                    Sfx.Play(Sfx.Id.WindowOpen);
                    RaidAnnouncer.Show(
                        $"HURDALIK AÇILDI — KAPILAR {Mathf.RoundToInt(entryDuration)} " +
                        "SANİYE AÇIK, İÇERİ GİR!",
                        new Color(0.20f, 0.95f, 0.60f), 3f);
                }
                break;

            case ZoneState.Open:
                // Giriş fazı biter → kapan kapanır, içeridekiler kilitlenir
                entryTimer -= Time.deltaTime;
                if (!gatesClosedAnnounced && entryTimer <= 0f)
                {
                    gatesClosedAnnounced = true;
                    CaptureOccupants();
                    Sfx.Play(Sfx.Id.WindowClose, 0.5f);
                    CameraShake.Add(0.2f);
                    RaidAnnouncer.Show(
                        "KAPILAR KAPANDI — SÜRE BİTENE KADAR İÇERİDESİN!\n" +
                        "<size=60%>Topladığını kendi depona bırak</size>",
                        new Color(0.95f, 0.45f, 0.15f), 3f);
                }

                if (!tenSecondsWarned && elapsed >= closeT - 10f)
                {
                    tenSecondsWarned = true;
                    RaidAnnouncer.Show("HURDALIK 10 SANİYE İÇİNDE KAPANIYOR!",
                        new Color(0.95f, 0.45f, 0.15f), 2.5f);
                }
                if (elapsed >= closeT) CloseWindow(silent: false);
                break;
        }
    }

    private void CloseWindow(bool silent)
    {
        State = ZoneState.Closed;
        windowIndex++;

        int delivered = 0;
        if (Authority)
        {
            // Depolar teslim edilir: mavi → garaj kapısına; kırmızı offline
            // DirectorAI'ye, MP'de client oyuncusunun kapısına
            delivered = DeliverDepot(blueDepot, blueEvictPoint);
            if (Mp) DeliverDepot(redDepot, redEvictPoint);
            else    ConvertRedDepot();

            // Serbest kalan yağmayı temizle (taşınanlar oyuncuda kalır)
            for (int i = loot.Count - 1; i >= 0; i--)
            {
                PickupItem l = loot[i];
                if (l == null) { loot.RemoveAt(i); continue; }
                if (l.transform.parent == null && !IsHeldOnNetwork(l) &&
                    IsInside(l.transform.position))
                {
                    Destroy(l.gameObject);
                    loot.RemoveAt(i);
                }
            }
        }

        EvictOccupants();
        lockedOccupants.Clear();

        if (!silent)
        {
            Sfx.Play(Sfx.Id.WindowClose);
            RaidAnnouncer.Show("HURDALIK KAPANDI",
                new Color(0.95f, 0.32f, 0.26f), 3f);

            // Teslimat sayısını yalnız server bilir — relay herkese gösterir
            if (delivered > 0)
                EventZoneSync.Announce(
                    $"HURDALIK KAPANDI — {delivered} MALZEME GARAJ KAPINA TAŞINDI!",
                    new Color(0.95f, 0.32f, 0.26f), 3f);
        }
    }

    /// <summary>MP'de taşıma parent'sız (NetworkItem holder) — ona da bak.</summary>
    private static bool IsHeldOnNetwork(PickupItem l) =>
        l.TryGetComponent<NetworkItem>(out NetworkItem ni) && ni.IsHeld;

    /// <summary>
    /// Bariyer kalkarken içeride kalanları kapı ağzına ışınla. MP: hareket
    /// owner-authoritative — her makine yalnız KENDİ oyuncusunu, kendi
    /// takımının kapısına taşır (iki taraf da aynı anı deterministik yaşar).
    /// </summary>
    private void EvictOccupants()
    {
        if (Mp)
        {
            PlayerInteraction local = FindLocalPlayer();
            if (local != null && IsInside(local.transform.position))
            {
                bool blue = !local.TryGetComponent<NetworkPlayer>(
                    out NetworkPlayer np) || np.IsBlueTeam;
                Vector3 gate = blue ? blueEvictPoint : redEvictPoint;
                local.transform.position = gate + Vector3.up * 0.75f;
            }
            return;
        }

        foreach (PlayerController pc in
                 FindObjectsByType<PlayerController>())
        {
            if (!IsInside(pc.transform.position)) continue;
            pc.transform.position = blueEvictPoint + Vector3.up * 0.75f;
        }

        TechnicianBot bot = FindAnyObjectByType<TechnicianBot>();
        if (bot != null && IsInside(bot.transform.position))
            bot.transform.position = redEvictPoint + Vector3.up * 0.75f;
    }

    /// <summary>MP: bu makinenin sahiplendiği oyuncu (offline: tek oyuncu).</summary>
    private static PlayerInteraction FindLocalPlayer()
    {
        foreach (PlayerInteraction pi in
                 FindObjectsByType<PlayerInteraction>())
            if (pi.IsLocalPlayer) return pi;
        return null;
    }

    // ── Yağma ────────────────────────────────────────────────────────────

    private static readonly ItemType[] lootPool =
    {
        ItemType.ScrapMetal, ItemType.CrystalShard, ItemType.RocketFuel,
        ItemType.ShieldAlloy, ItemType.EMPCore, ItemType.RawPlasma
    };

    private void ScatterLoot()
    {
        if (lootPrefab == null)
        {
            Debug.LogWarning("[ScrapWindowZone] lootPrefab atanmamış — yağma yok!");
            return;
        }

        for (int i = 0; i < lootCount; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(zoneRect.x + 1.5f, zoneRect.y - 1.5f),
                0.6f,
                Random.Range(zoneRect.z + 1.5f, zoneRect.w - 1.5f));

            SpawnLoot(lootPool[Random.Range(0, lootPool.Length)], pos);
        }
    }

    /// <summary>
    /// Tek yağma item'ı üretir (etkinlik açılışı + bot istasyon hasadı).
    /// Dönen item takip listesindedir; teslim/yok edilince ReleaseLoot çağır.
    /// </summary>
    public PickupItem SpawnLoot(ItemType type, Vector3 pos)
    {
        if (lootPrefab == null) return null;

        GameObject obj = Instantiate(lootPrefab, pos, Quaternion.identity);
        if (!obj.TryGetComponent<PickupItem>(out PickupItem item))
        {
            Destroy(obj);
            return null;
        }

        item.SetType(type);
        loot.Add(item);
        StationVisuals.AddLootBeam(obj, StationVisuals.ItemColor(type));

        // MP: client kopyası huzmeyi beamNv üzerinden kendisi kurar
        if (obj.TryGetComponent<NetworkItem>(out NetworkItem ni))
            ni.SetBeam(true);

        return item;
    }

    /// <summary>Kapılmamış (yerde duran) yağmalar — bot hedef seçimi için.</summary>
    public IEnumerable<PickupItem> FreeLoot()
    {
        foreach (PickupItem l in loot)
            if (l != null && l.transform.parent == null)
                yield return l;
    }

    /// <summary>Teslim edilen/alınan yağmayı takipten çıkar.</summary>
    public void ReleaseLoot(PickupItem item) => loot.Remove(item);

    // ── Kapan kilidi ─────────────────────────────────────────────────────
    // Kapılar kapanınca içeride kimler varsa süre bitene kadar kilitlidir.
    // Bariyer collider'ı ana engel; yumruk savrulması gibi ışınlamalarla
    // sınırdan sızanları geri iter.

    private void CaptureOccupants()
    {
        lockedOccupants.Clear();

        if (Mp)
        {
            // Hareket owner-authoritative: her makine kendi oyuncusunu
            // kilitler (kapan anı senkron saatle iki tarafta aynı karede)
            PlayerInteraction local = FindLocalPlayer();
            if (local != null && IsInside(local.transform.position))
                lockedOccupants.Add(local.transform);
            return;
        }

        foreach (PlayerController pc in
                 FindObjectsByType<PlayerController>())
            if (IsInside(pc.transform.position))
                lockedOccupants.Add(pc.transform);

        TechnicianBot bot = FindAnyObjectByType<TechnicianBot>();
        if (bot != null && IsInside(bot.transform.position))
            lockedOccupants.Add(bot.transform);
    }

    private void KeepOccupantsInside()
    {
        if (GatesOpen) return;   // Giriş fazında serbest dolaşım

        foreach (Transform t in lockedOccupants)
        {
            if (t == null || IsInside(t.position)) continue;

            Vector3 pos = t.position;
            pos.x = Mathf.Clamp(pos.x, zoneRect.x + 0.6f, zoneRect.y - 0.6f);
            pos.z = Mathf.Clamp(pos.z, zoneRect.z + 0.6f, zoneRect.w - 0.6f);
            t.position = pos;
        }
    }

    // ── Takım depoları ───────────────────────────────────────────────────
    // Depoya bırakılan item'ın collider'ı kapanır: rakip ne E ile alabilir
    // ne yumrukla düşürtebilir. Pencere kapanınca sahibine teslim edilir.

    /// <summary>
    /// Malzeme taşıyan oyuncu deposuna yaklaşınca otomatik depolar.
    /// MP'de server koşar: depo oyuncunun TAKIMINA göre seçilir.
    /// </summary>
    private void CheckPlayerDeposit()
    {
        if (blueDepotAnchor == null) return;

        foreach (PlayerInteraction pi in
                 FindObjectsByType<PlayerInteraction>())
        {
            if (pi.HeldObject == null) continue;
            if (!IsInside(pi.transform.position)) continue;

            bool blue = !Mp || !pi.TryGetComponent<NetworkPlayer>(
                out NetworkPlayer np) || np.IsBlueTeam;
            Transform anchor = blue ? blueDepotAnchor : redDepotAnchor;
            if (anchor == null) continue;

            Vector3 a = pi.transform.position;  a.y = 0f;
            Vector3 b = anchor.position;        b.y = 0f;
            if (Vector3.Distance(a, b) > depotRadius) continue;

            GameObject held = pi.HeldObject;
            pi.ForceDropFromStation();

            if (held.TryGetComponent<PickupItem>(out PickupItem item))
            {
                DepositItem(item, blue ? blueDepot : redDepot, anchor);
                EventZoneSync.PlaySfx(Sfx.Id.Deposit);
                EventZoneSync.Popup(anchor.position, "DEPOLANDI!",
                    blue ? new Color(0.25f, 0.50f, 0.95f)
                         : new Color(0.95f, 0.32f, 0.26f), 1f);
            }
        }
    }

    /// <summary>Rakip teknisyen kendi deposuna bırakır (TechnicianBot çağırır).</summary>
    public void DepositFromBot(PickupItem item)
    {
        if (item == null || redDepotAnchor == null) return;

        DepositItem(item, redDepot, redDepotAnchor);
        DamagePopup.Spawn(redDepotAnchor.position, "RAKİP DEPOLADI",
            new Color(0.95f, 0.32f, 0.26f), 1f);
    }

    private static void DepositItem(PickupItem item, List<PickupItem> depot,
        Transform anchor)
    {
        // Işık huzmesi taşıma sırasında kalır, depoda söner
        Transform beam = item.transform.Find("Beam");
        if (beam != null) Destroy(beam.gameObject);

        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
        if (item.TryGetComponent<Collider>(out Collider col))
            col.enabled = false;   // Rakip erişemez

        // Depo pedinde 3'lü sıralar halinde istifle
        int i = depot.Count;
        Vector3 offset = new Vector3(
            (i % 3 - 1) * 0.55f, 0.35f, (i / 3) * 0.55f - 0.35f);

        if (Mp)
        {
            // NetworkObject sahne child'ına parent'lanamaz — pozisyona
            // sabitle (NetworkTransform taşır) + ağ kilidi (rakip alamaz)
            item.transform.position = anchor.position + offset;
            item.transform.rotation = Quaternion.identity;
            if (item.TryGetComponent<NetworkItem>(out NetworkItem ni))
            {
                ni.SetBeam(false);
                ni.SetLocked(true);
            }
        }
        else
        {
            item.transform.SetParent(anchor);
            item.transform.localPosition = offset;
            item.transform.localRotation = Quaternion.identity;
        }

        depot.Add(item);
    }

    /// <summary>Depo → takımın garaj kapısı iç tarafına sıralanır.
    /// Mavi kapı yönü -x, kırmızı +x (kapıdan içeri bakar).</summary>
    private int DeliverDepot(List<PickupItem> depot, Vector3 gate)
    {
        int   count = 0;
        float xDir  = gate == blueEvictPoint ? -1.4f : 1.4f;

        for (int i = 0; i < depot.Count; i++)
        {
            PickupItem item = depot[i];
            if (item == null) continue;

            item.transform.SetParent(null);
            item.transform.position = gate +
                new Vector3(xDir, 0.5f, (i % 5 - 2) * 0.8f);
            item.transform.rotation = Quaternion.identity;

            if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
            }
            if (item.TryGetComponent<Collider>(out Collider col))
            {
                col.enabled   = true;
                col.isTrigger = false;
            }

            // Ağ kilidi kalkar — sahibi artık alabilir
            if (Mp && item.TryGetComponent<NetworkItem>(out NetworkItem ni))
                ni.SetLocked(false);

            ReleaseLoot(item);
            count++;
        }

        depot.Clear();
        return count;
    }

    /// <summary>Kırmızı depo → DirectorAI üretim hızlanmasına dönüşür.</summary>
    private void ConvertRedDepot()
    {
        DirectorAI director = FindAnyObjectByType<DirectorAI>();

        foreach (PickupItem item in redDepot)
        {
            if (item == null) continue;
            director?.ReceiveScrapDelivery();
            ReleaseLoot(item);
            Destroy(item.gameObject);
        }

        redDepot.Clear();
    }

    // ── Bariyer animasyonu ───────────────────────────────────────────────

    private void AnimateBarriers()
    {
        if (barriers == null) return;

        for (int i = 0; i < barriers.Length; i++)
        {
            Transform b = barriers[i];
            if (b == null) continue;

            // Kapılar sadece giriş fazında iner — kapan mekaniği
            float targetY = GatesOpen ? barrierClosedY[i] - BARRIER_SINK
                                      : barrierClosedY[i];
            Vector3 pos = b.position;
            pos.y = Mathf.MoveTowards(pos.y, targetY,
                BARRIER_ANIM_SPEED * Time.deltaTime);
            b.position = pos;
        }
    }

    // ── Oyuncu yumruk bileşeni ───────────────────────────────────────────

    /// <summary>
    /// Oyuncu runtime'da spawn olur (OfflinePlayerSpawner / NGO) — prefabı
    /// düzenlemeden PlayerMelee'yi burada iliştiririz (yarım sn'de bir dene).
    /// </summary>
    private void EnsurePlayerMelee()
    {
        meleeSetupTimer -= Time.deltaTime;
        if (meleeSetupTimer > 0f) return;
        meleeSetupTimer = 0.5f;

        foreach (PlayerController pc in
                 FindObjectsByType<PlayerController>())
        {
            if (pc.GetComponent<PlayerMelee>() == null)
                pc.gameObject.AddComponent<PlayerMelee>();

            // FPV: pencere açıkken bölgeye girince kamera göze iner
            if (pc.GetComponent<FirstPersonView>() == null)
                pc.gameObject.AddComponent<FirstPersonView>();
        }
    }
}
