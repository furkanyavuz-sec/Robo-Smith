// DroneRaidZone.cs — Çekirdek Bölge (drone raid) yöneticisi
// Görev: Hazırlık fazında zamanlı pencereler açar; pencere açılınca
//   platforma işlenmiş ürün ödülleri spawn eder, bariyerleri indirir,
//   kapanınca kalan ödülleri temizleyip drone'ları eve gönderir.
// Ayrıca iki drone'un çarpışma/çalma kontrolü buradadır (tek nokta).
// Kurulum: MapGenerator.BuildDroneRaidZone() üretir, alanları SetField ile bağlar.

using System.Collections.Generic;
using UnityEngine;

public class DroneRaidZone : MonoBehaviour
{
    public static DroneRaidZone Instance { get; private set; }

    public enum ZoneState { Closed, Announcing, Open }

    [Header("Pencere Zamanlaması (hazırlık başından itibaren sn)")]
    [SerializeField] private float[] windowOpenTimes = { 120f, 300f, 480f };
    [SerializeField] private float windowDuration = 60f;
    [SerializeField] private float announceLead   = 30f;

    [Header("Ödüller")]
    [SerializeField] private GameObject itemPrefab;   // PlasmaCore_Prefab (PickupItem)
    [SerializeField] private int rewardCount = 3;

    [Header("Platform Geometrisi (MapGenerator bağlar)")]
    [SerializeField] private Vector3 platformCenter;
    [SerializeField] private Vector2 platformSize = new Vector2(14f, 10f);
    [SerializeField] private float   mapEdgeZ     = 10f;   // Ana harita ön duvarı

    [Header("Bariyerler & Drone'lar (MapGenerator bağlar)")]
    [SerializeField] private Transform[] barriers;
    [SerializeField] private SupplyDrone blueDrone;
    [SerializeField] private SupplyDrone redDrone;

    // ── Çalışma durumu ───────────────────────────────────────────────────
    private float elapsed;            // Hazırlık fazında geçen süre
    private int   windowIndex;         // Sıradaki pencere
    private bool  tenSecondsWarned;
    private float stealCooldown;

    private readonly List<PickupItem> rewards = new();
    private float[] barrierClosedY;

    private const float BARRIER_SINK      = 6.5f;   // Açıkken ne kadar gömülür
    private const float BARRIER_ANIM_SPEED = 4f;
    private const float STEAL_RADIUS       = 1.7f;
    private const float STEAL_COOLDOWN     = 1.2f;

    // ── Public API ───────────────────────────────────────────────────────
    public ZoneState State  { get; private set; } = ZoneState.Closed;
    public bool IsOpen      => State == ZoneState.Open;

    /// <summary>Konsol pencere anonsuyla birlikte kullanılabilir olur.</summary>
    public bool DroneUsable => State != ZoneState.Closed;

    /// <summary>Drone'ların uçabildiği en ileri z (kapalıyken platform yasak).</summary>
    public float MaxAllowedZ => IsOpen
        ? platformCenter.z + platformSize.y / 2f - 0.5f
        : mapEdgeZ - 0.8f;

    /// <summary>Sonraki pencereye kalan süre (HUD/etiket için, -1 = kalmadı).</summary>
    public float TimeToNextWindow => windowIndex < windowOpenTimes.Length
        ? Mathf.Max(0f, windowOpenTimes[windowIndex] - elapsed)
        : -1f;

    private void Awake()
    {
        Instance = this;

        // Bariyerlerin kapalı pozisyonlarını ezberle
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

        elapsed       += Time.deltaTime;
        stealCooldown -= Time.deltaTime;

        UpdateWindowState();
        AnimateBarriers();
        CheckDroneCollision();
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
                        $"ÇEKİRDEK BÖLGE {Mathf.RoundToInt(announceLead)} SANİYE " +
                        "SONRA AÇILIYOR\n<size=60%>Drone konsoluna geç [E]</size>",
                        new Color(0.95f, 0.85f, 0.10f), 4f);
                }
                break;

            case ZoneState.Announcing:
                if (elapsed >= openT)
                {
                    State = ZoneState.Open;
                    tenSecondsWarned = false;
                    SpawnRewards();
                    RaidAnnouncer.Show("ÇEKİRDEK BÖLGE AÇILDI!",
                        new Color(0.20f, 0.95f, 0.60f), 3f);
                }
                break;

            case ZoneState.Open:
                if (!tenSecondsWarned && elapsed >= closeT - 10f)
                {
                    tenSecondsWarned = true;
                    RaidAnnouncer.Show("ÇEKİRDEK BÖLGE 10 SANİYE İÇİNDE KAPANIYOR!",
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

        // Platformda kalan (alınmamış, taşınmayan) ödülleri yok et
        for (int i = rewards.Count - 1; i >= 0; i--)
        {
            PickupItem r = rewards[i];
            if (r == null) { rewards.RemoveAt(i); continue; }
            if (r.transform.position.z > mapEdgeZ && r.transform.parent == null)
            {
                Destroy(r.gameObject);
                rewards.RemoveAt(i);
            }
        }

        blueDrone?.ForceReturnHome();
        redDrone?.ForceReturnHome();

        if (!silent)
            RaidAnnouncer.Show("ÇEKİRDEK BÖLGE KAPANDI",
                new Color(0.95f, 0.32f, 0.26f), 2.5f);
    }

    // ── Ödüller ──────────────────────────────────────────────────────────

    private void SpawnRewards()
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("[DroneRaidZone] itemPrefab atanmamış — ödül yok!");
            return;
        }

        ItemType[] pool =
            { ItemType.SteelPlate, ItemType.PlasmaCore, ItemType.Microchip };

        for (int i = 0; i < rewardCount; i++)
        {
            // Platformda yayılmış konumlar (kenardan pay bırak)
            float x = platformCenter.x +
                Mathf.Lerp(-platformSize.x / 2f + 2f, platformSize.x / 2f - 2f,
                    rewardCount <= 1 ? 0.5f : (float)i / (rewardCount - 1));
            float z = platformCenter.z + Random.Range(-platformSize.y / 2f + 1.5f,
                                                       platformSize.y / 2f - 1.5f);

            GameObject obj = Instantiate(itemPrefab,
                new Vector3(x, platformCenter.y + 0.6f, z), Quaternion.identity);

            ItemType type = pool[Random.Range(0, pool.Length)];
            if (obj.TryGetComponent<PickupItem>(out PickupItem item))
            {
                item.SetType(type);
                rewards.Add(item);
            }

            // Işık huzmesi — ödülün yerini uzaktan belli eder (kapılınca gider)
            StationVisuals.AddLootBeam(obj, StationVisuals.ItemColor(type));
        }
    }

    /// <summary>
    /// Drone her kare çağırır: yakında serbest ödül varsa kaptırır.
    /// Taşınan ödül (parent'lı) ve teslim edilmişler listeden düşer.
    /// </summary>
    public PickupItem TryGrabNearby(Vector3 dronePos, float radius)
    {
        foreach (PickupItem r in rewards)
        {
            if (r == null || r.transform.parent != null) continue;

            // Yatay mesafe — drone yüksekte uçar, ödül yerdedir
            Vector3 a = r.transform.position; a.y = 0f;
            Vector3 b = dronePos;             b.y = 0f;
            if (Vector3.Distance(a, b) <= radius)
            {
                // Huzmeyi söndür
                Transform beam = r.transform.Find("Beam");
                if (beam != null) Destroy(beam.gameObject);
                return r;
            }
        }
        return null;
    }

    /// <summary>Teslim edilen/yok edilen ödülü takipten çıkar.</summary>
    public void ReleaseReward(PickupItem item) => rewards.Remove(item);

    /// <summary>Kapılmamış (serbest) ödüller — AI pilot hedef seçimi için.</summary>
    public IEnumerable<PickupItem> FreeRewards()
    {
        foreach (PickupItem r in rewards)
            if (r != null && r.transform.parent == null)
                yield return r;
    }

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

    // ── Drone çarpışması (çalma) ─────────────────────────────────────────

    private void CheckDroneCollision()
    {
        if (stealCooldown > 0f || blueDrone == null || redDrone == null) return;
        if (!blueDrone.IsFlying || !redDrone.IsFlying) return;

        if (Vector3.Distance(blueDrone.transform.position,
                             redDrone.transform.position) > STEAL_RADIUS)
            return;

        stealCooldown = STEAL_COOLDOWN;

        Vector3 apart = (blueDrone.transform.position -
                         redDrone.transform.position).normalized;
        if (apart == Vector3.zero) apart = Vector3.right;

        bool anyDropped = blueDrone.OnRammed( apart) |
                          redDrone .OnRammed(-apart);

        if (anyDropped)
            RaidAnnouncer.Show("DRONE ÇARPIŞMASI — YÜK DÜŞTÜ!",
                new Color(0.95f, 0.85f, 0.10f), 2f);
    }
}
