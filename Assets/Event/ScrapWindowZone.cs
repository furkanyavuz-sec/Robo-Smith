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
    private float meleeSetupTimer;

    private readonly List<PickupItem> loot = new();
    private float[] barrierClosedY;

    private const float BARRIER_SINK       = 4.5f;
    private const float BARRIER_ANIM_SPEED = 3.5f;

    public ZoneState State { get; private set; } = ZoneState.Closed;
    public bool IsOpen     => State == ZoneState.Open;

    public bool IsInside(Vector3 pos) =>
        pos.x > zoneRect.x && pos.x < zoneRect.y &&
        pos.z > zoneRect.z && pos.z < zoneRect.w;

    private void Awake()
    {
        Instance = this;

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

        elapsed += Time.deltaTime;

        EnsurePlayerMelee();
        UpdateWindowState();
        AnimateBarriers();
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
                        "SONRA AÇILIYOR\n<size=60%>Malzemeler yere saçılacak — " +
                        "rakibe dikkat! [Boşluk: Yumruk]</size>",
                        new Color(0.95f, 0.85f, 0.10f), 4f);
                }
                break;

            case ZoneState.Announcing:
                if (elapsed >= openT)
                {
                    State = ZoneState.Open;
                    tenSecondsWarned = false;
                    ScatterLoot();
                    RaidAnnouncer.Show("HURDALIK AÇILDI — MALZEMELERİ KAP!",
                        new Color(0.20f, 0.95f, 0.60f), 3f);
                }
                break;

            case ZoneState.Open:
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

        // Serbest kalan yağmayı temizle (taşınanlar oyuncuda kalır)
        for (int i = loot.Count - 1; i >= 0; i--)
        {
            PickupItem l = loot[i];
            if (l == null) { loot.RemoveAt(i); continue; }
            if (l.transform.parent == null && IsInside(l.transform.position))
            {
                Destroy(l.gameObject);
                loot.RemoveAt(i);
            }
        }

        EvictOccupants();

        if (!silent)
            RaidAnnouncer.Show("HURDALIK KAPANDI",
                new Color(0.95f, 0.32f, 0.26f), 2.5f);
    }

    /// <summary>Bariyer kalkarken içeride kalan herkesi kapı ağzına ışınla.</summary>
    private void EvictOccupants()
    {
        foreach (PlayerController pc in
                 FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!IsInside(pc.transform.position)) continue;
            pc.transform.position = blueEvictPoint + Vector3.up * 0.75f;
        }

        TechnicianBot bot = FindFirstObjectByType<TechnicianBot>();
        if (bot != null && IsInside(bot.transform.position))
            bot.transform.position = redEvictPoint + Vector3.up * 0.75f;
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

    // ── Bariyer animasyonu ───────────────────────────────────────────────

    private void AnimateBarriers()
    {
        if (barriers == null) return;

        for (int i = 0; i < barriers.Length; i++)
        {
            Transform b = barriers[i];
            if (b == null) continue;

            float targetY = IsOpen ? barrierClosedY[i] - BARRIER_SINK
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
                 FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (pc.GetComponent<PlayerMelee>() == null)
                pc.gameObject.AddComponent<PlayerMelee>();

            // FPV: pencere açıkken bölgeye girince kamera göze iner
            if (pc.GetComponent<FirstPersonView>() == null)
                pc.gameObject.AddComponent<FirstPersonView>();
        }
    }
}
