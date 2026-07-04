// MapGenerator.cs — v2.1: Üç bölgeli harita + görsel tasarım
// Düzen: [Mavi Garaj] | [Tarafsız Hurdalık - gri] | [Kırmızı Garaj]
// v2.1 yenilikleri:
//   • Magenta sorunu çözüldü: shader artık aktif render pipeline'dan alınıyor
//     (URP'de Shader.Find("Standard") pembe render ediyordu)
//   • Görsel tasarım: takım renginde zemin + parlak kenar şeritleri,
//     şasi platformları, spawn pedleri; tarafsız bölgede gri tonlu zemin,
//     yürüyüş yolları, siper sandıkları ve sütunlar
//   • Malzemeler renk başına önbelleklenir (onlarca kopya materyal yok)
// Kullanım: Map objesi → sağ tık → "Generate Map" → sahneyi kaydet.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MapGenerator : MonoBehaviour
{
    [Header("Harita Boyutları")]
    [SerializeField] private float garageWidth    = 20f;
    [SerializeField] private float garageDepth    = 20f;
    [SerializeField] private float scrapyardWidth = 16f;
    [SerializeField] private float wallHeight     = 3f;
    [SerializeField] private float wallThickness  = 0.5f;

    [Header("Renk Paleti — Takımlar")]
    [SerializeField] private Color blueFloor  = new Color(0.13f, 0.18f, 0.32f);
    [SerializeField] private Color blueAccent = new Color(0.25f, 0.50f, 0.95f);
    [SerializeField] private Color redFloor   = new Color(0.30f, 0.12f, 0.12f);
    [SerializeField] private Color redAccent  = new Color(0.95f, 0.32f, 0.26f);

    [Header("Renk Paleti — Tarafsız Bölge (gri tonlar)")]
    [SerializeField] private Color neutralFloor = new Color(0.15f, 0.15f, 0.17f);
    [SerializeField] private Color neutralMid   = new Color(0.22f, 0.22f, 0.24f);
    [SerializeField] private Color neutralLight = new Color(0.34f, 0.34f, 0.37f);
    [SerializeField] private Color crateGray    = new Color(0.28f, 0.28f, 0.30f);
    [SerializeField] private Color wallTone     = new Color(0.19f, 0.19f, 0.21f);

    // ── Çekirdek Bölge boyutları (KOD İÇİNDE — tek denge noktası) ────────
    // Inspector'daki eski seri değerler haritayı etkilemesin diye sabit.
    private const float CoreZoneGap   = 3f;    // Ön duvar → platform boşluğu
    private const float CoreZoneWidth = 18f;
    private const float CoreZoneDepth = 13f;

    [Header("Çekirdek Bölge (Drone Raid)")]
    [SerializeField] private Color barrierColor  = new Color(0.95f, 0.30f, 0.20f);
    [SerializeField] private Color coreAccent    = new Color(0.20f, 0.85f, 0.90f);

    [Header("İstasyon Prefabları")]
    [SerializeField] private GameObject supplyBinPrefab;
    [SerializeField] private GameObject processorPrefab;
    [SerializeField] private GameObject trashBinPrefab;
    [SerializeField] private GameObject chassisPrefab;
    [SerializeField] private GameObject scrapyardStationPrefab;
    [SerializeField] private GameObject weaponCraftStationPrefab;
    [SerializeField] private GameObject plasmaSourcePrefab;

    // Hesaplanan merkezler (generator pozisyonuna göre)
    private float totalWidth;
    private float teamACenter;
    private float teamBCenter;

    // Üretim sırasında toplanan drone referansları (zone kablolaması için)
    private SupplyDrone blueDrone;
    private SupplyDrone redDrone;

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ClearMap();

        totalWidth  = garageWidth * 2f + scrapyardWidth;
        teamACenter = -(garageWidth + scrapyardWidth) / 2f;
        teamBCenter =  (garageWidth + scrapyardWidth) / 2f;

        // Zeminler
        CreateFloor("Zemin - Mavi Garaj",    teamACenter, garageWidth,    blueFloor);
        CreateFloor("Zemin - Hurdalık",      0f,          scrapyardWidth, neutralFloor);
        CreateFloor("Zemin - Kırmızı Garaj", teamBCenter, garageWidth,    redFloor);

        CreateWalls();

        // Garajlar (aynalı) + tarafsız orta bölge
        List<RobotChassis> blueChassis = BuildGarage(teamACenter, -1, "Mavi",
            blueAccent, out Transform blueSpawn);
        BuildGarage(teamBCenter, +1, "Kırmızı", redAccent, out Transform _);
        BuildScrapyard();

        // Çekirdek bölge: ön duvarın ötesinde, sadece drone'la ulaşılan platform
        BuildDroneRaidZone();

        WireSceneReferences(blueChassis, blueSpawn);
        WarnAboutOrphanStations();
        MarkSceneDirty();

        Debug.Log($"[MapGenerator] ✅ Harita {transform.position} konumunda kuruldu: " +
                  $"Mavi | Hurdalık | Kırmızı. Sahneyi kaydetmeyi unutma (Ctrl+S).");

        if (transform.position.sqrMagnitude > 1f)
            Debug.LogWarning("[MapGenerator] ℹ️ Map objesi (0,0,0)'da değil — harita " +
                             "objenin bulunduğu yerde kurulur. Görmüyorsan Hierarchy'de " +
                             "Map'e çift tıkla veya pozisyonu Reset'le ve tekrar üret.");
    }

    // ── Garaj (takım bölgesi) ────────────────────────────────────────────

    private List<RobotChassis> BuildGarage(float centerX, int sign, string zone,
        Color accent, out Transform spawnPoint)
    {
        // sign: -1 = sol (Mavi), +1 = sağ (Kırmızı)

        // ── Görsel: kenar şeritleri + şasi platformu ────────────────────
        BuildGarageDecor(centerX, sign, accent);

        // ── İstasyonlar ─────────────────────────────────────────────────
        SupplyBin ironBin = Place<SupplyBin>(supplyBinPrefab,
            $"Tedarik Kutusu - Demir [{zone}]",
            new Vector3(centerX + sign * 7f, 0f, -6f));
        Configure(ironBin, "supplyItemType", ItemType.Iron);
        TryAssignPrefab(ironBin, "itemPrefab", "Iron_Prefab");
        StationVisuals.DecorateSupplyBin(ironBin?.gameObject, ItemType.Iron, "Demir");

        SupplyBin circuitBin = Place<SupplyBin>(supplyBinPrefab,
            $"Tedarik Kutusu - Devre [{zone}]",
            new Vector3(centerX + sign * 7f, 0f, -2f));
        Configure(circuitBin, "supplyItemType", ItemType.Circuit);
        TryAssignPrefab(circuitBin, "itemPrefab", "Circuit_Prefab");
        StationVisuals.DecorateSupplyBin(circuitBin?.gameObject, ItemType.Circuit, "Devre");

        TrashBin trash = Place<TrashBin>(trashBinPrefab, $"Çöp Kutusu [{zone}]",
            new Vector3(centerX + sign * 7f, 0f, 6f));
        StationVisuals.DecorateTrashBin(trash?.gameObject);

        Processor processor = Place<Processor>(processorPrefab,
            $"İşleme Masası [{zone}]",
            new Vector3(centerX + sign * 3f, 0f, 7f));
        StationVisuals.DecorateProcessor(processor?.gameObject);

        PlaceAssemblyStation(zone, new Vector3(centerX + sign * 3f, 0f, -7f));

        // 3 şasi — GameManager 3 oyuncu robotu destekliyor
        var chassisList = new List<RobotChassis>();
        for (int i = 0; i < 3; i++)
        {
            RobotChassis chassis = Place<RobotChassis>(chassisPrefab,
                $"Robot Şasisi {i + 1} [{zone}]",
                new Vector3(centerX, 0f, -5f + i * 5f));
            if (chassis != null)
            {
                chassisList.Add(chassis);
                StationVisuals.DecorateChassis(chassis.gameObject,
                    $"Robot Şasisi {i + 1}", accent);
            }
        }

        // ── Drone rıhtımı: konsol + park pedi + drone ───────────────────
        SupplyDrone drone = BuildDroneDock(centerX, sign, zone, accent);
        if (sign < 0) blueDrone = drone;
        else          redDrone  = drone;

        // ── Spawn noktası + ped ─────────────────────────────────────────
        GameObject spawn = new GameObject($"PlayerSpawn [{zone}]");
        spawn.transform.SetParent(transform);
        spawn.transform.position = MapPos(new Vector3(centerX - sign * 7f, 0.75f, 0f));
        spawnPoint = spawn.transform;

        CreatePad($"Spawn Pedi [{zone}]",
            new Vector3(centerX - sign * 7f, 0f, 0f), new Vector2(3f, 3f), accent);

        return chassisList;
    }

    private void BuildGarageDecor(float centerX, int sign, Color accent)
    {
        float halfW = garageWidth / 2f;
        float halfD = garageDepth / 2f;

        // Parlak kenar şeritleri — garaj sınırını takım rengiyle çerçeveler
        CreatePad("Şerit (dış)",
            new Vector3(centerX + sign * (halfW - 0.6f), 0f, 0f),
            new Vector2(0.5f, garageDepth - 1.2f), accent);

        CreatePad("Şerit (geçit)",
            new Vector3(centerX - sign * (halfW - 0.6f), 0f, 0f),
            new Vector2(0.5f, garageDepth - 1.2f), accent);

        CreatePad("Şerit (ön)",
            new Vector3(centerX, 0f, halfD - 0.6f),
            new Vector2(garageWidth - 1.2f, 0.5f), accent);

        CreatePad("Şerit (arka)",
            new Vector3(centerX, 0f, -(halfD - 0.6f)),
            new Vector2(garageWidth - 1.2f, 0.5f), accent);

        // Şasi platformu — üç şasinin altında hafif açık ton
        Color platform = Color.Lerp(
            sign < 0 ? blueFloor : redFloor, Color.white, 0.12f);
        CreatePad("Şasi Platformu",
            new Vector3(centerX, 0f, 0f), new Vector2(5f, 16f), platform);
    }

    // ── Tarafsız Orta Bölge (Hurdalık — gri tonlar) ──────────────────────

    private void BuildScrapyard()
    {
        float halfD = garageDepth / 2f;

        // İç zemin katmanı — kenarlardan bir tık açık gri
        CreatePad("Hurdalık İç Zemin",
            Vector3.zero, new Vector2(scrapyardWidth - 2f, garageDepth - 2f),
            neutralMid, 0.015f);

        // Geçitleri birbirine bağlayan yürüyüş yolu
        CreatePad("Yürüyüş Yolu",
            Vector3.zero, new Vector2(scrapyardWidth - 0.5f, 2.2f),
            neutralLight, 0.03f);

        // Orta kolon: 5 ham madde hurdalığı
        (ItemType type, string name)[] scraps =
        {
            (ItemType.ScrapMetal,   "Hurda Metal"),
            (ItemType.CrystalShard, "Kristal Kıymık"),
            (ItemType.RocketFuel,   "Roket Yakıtı"),
            (ItemType.ShieldAlloy,  "Kalkan Alaşımı"),
            (ItemType.EMPCore,      "EMP Çekirdeği"),
        };

        for (int i = 0; i < scraps.Length; i++)
        {
            float z = -8f + i * 4f;
            ScrapyardStation s = Place<ScrapyardStation>(scrapyardStationPrefab,
                $"Hurdalık - {scraps[i].name}",
                new Vector3(0f, 0f, z));
            Configure(s, "supplyType", scraps[i].type);
            TryAssignPrefab(s, "itemPrefab", "ScrapMetal_Prefab");
            StationVisuals.DecorateScrapyard(s?.gameObject,
                scraps[i].type, scraps[i].name);

            CreatePad($"İstasyon Pedi - {scraps[i].name}",
                new Vector3(0f, 0f, z), new Vector2(2.4f, 2.4f), neutralLight, 0.025f);
        }

        // Sol kolon: 3 silah atölyesi
        BuildWeaponCraft(new Vector3(-5f, 0f, -7f),   ItemType.ScrapMetal,   ItemType.Sword,  "Kılıç",  "Hurda Metal");
        BuildWeaponCraft(new Vector3(-5f, 0f, -2.5f), ItemType.CrystalShard, ItemType.Laser,  "Lazer",  "Kristal Kıymık");
        BuildWeaponCraft(new Vector3(-5f, 0f,  2.5f), ItemType.RocketFuel,   ItemType.Rocket, "Roket",  "Roket Yakıtı");

        // Sağ kolon: 2 silah atölyesi + plazma kaynakları
        BuildWeaponCraft(new Vector3(5f, 0f, -7f),   ItemType.ShieldAlloy, ItemType.Shield, "Kalkan", "Kalkan Alaşımı");
        BuildWeaponCraft(new Vector3(5f, 0f, -2.5f), ItemType.EMPCore,     ItemType.EMP,    "EMP",    "EMP Çekirdeği");

        PlasmaSource plasma1 = Place<PlasmaSource>(plasmaSourcePrefab,
            "Plazma Kaynağı", new Vector3(5f, 0f, 3f));
        StationVisuals.DecoratePlasmaSource(plasma1?.gameObject);

        PlasmaSource plasma2 = Place<PlasmaSource>(plasmaSourcePrefab,
            "Plazma Kaynağı (2)", new Vector3(-5f, 0f, 7f));
        StationVisuals.DecoratePlasmaSource(plasma2?.gameObject);

        // Dekor: siper sandıkları — gri tonlarda, çeşitli boylar
        CreateCrate("Sandık 1", new Vector3(-2.6f,  0f,  5.2f), 1.3f,  18f);
        CreateCrate("Sandık 2", new Vector3( 2.6f,  0f,  5.6f), 1.0f, -25f);
        CreateCrate("Sandık 3", new Vector3( 2.8f,  0f, -5.4f), 1.4f,  40f);
        CreateCrate("Sandık 4", new Vector3(-2.7f,  0f, -5.8f), 1.1f, -12f);
        CreateCrate("Sandık 5", new Vector3( 6.3f,  0f,  8.2f), 0.9f,  30f);
        CreateCrate("Sandık 6", new Vector3(-6.3f,  0f, -8.4f), 0.9f, -35f);

        // Dekor: köşe sütunları — hafif siper
        CreatePillar("Sütun 1", new Vector3(-2.5f, 0f,  8.6f));
        CreatePillar("Sütun 2", new Vector3( 2.5f, 0f,  8.6f));
        CreatePillar("Sütun 3", new Vector3(-2.5f, 0f, -8.6f));
        CreatePillar("Sütun 4", new Vector3( 2.5f, 0f, -8.6f));

        // Geçit işaretleri — garaj sınırındaki kapı ağızları
        float gateAx = teamACenter + garageWidth / 2f;
        float gateBx = teamBCenter - garageWidth / 2f;
        CreatePad("Geçit İşareti (Mavi)",
            new Vector3(gateAx, 0f, 0f), new Vector2(1.2f, garageDepth / 2f),
            neutralLight, 0.03f);
        CreatePad("Geçit İşareti (Kırmızı)",
            new Vector3(gateBx, 0f, 0f), new Vector2(1.2f, garageDepth / 2f),
            neutralLight, 0.03f);
    }

    /// <summary>
    /// Montaj İstasyonu için ayrı prefab yok — Processor prefabını klonlayıp
    /// bileşenini AssemblyStation ile değiştiririz (görsel/collider/layer kalır).
    /// </summary>
    private void PlaceAssemblyStation(string zone, Vector3 pos)
    {
        Processor temp = Place<Processor>(processorPrefab,
            $"Montaj İstasyonu [{zone}]", pos);
        if (temp == null) return;

        GameObject obj = temp.gameObject;
        if (Application.isPlaying) Destroy(temp);
        else                       DestroyImmediate(temp);

        AssemblyStation assembly = obj.AddComponent<AssemblyStation>();
        TryAssignPrefab(assembly, "outputPrefab", "PlasmaCore_Prefab");
        StationVisuals.DecorateAssembly(obj);
    }

    // ── Drone Rıhtımı & Çekirdek Bölge ───────────────────────────────────

    /// <summary>
    /// Drone Konsolu için ayrı prefab yok — SupplyBin prefabını klonlayıp
    /// bileşenini DroneConsole ile değiştiririz (Montaj İstasyonu deseni).
    /// Yanına park pedi + prosedürel drone kurulur.
    /// </summary>
    private SupplyDrone BuildDroneDock(float centerX, int sign, string zone,
        Color accent)
    {
        Vector3 consolePos = new Vector3(centerX - sign * 5f, 0f, 7f);
        Vector3 padPos     = new Vector3(centerX - sign * 5f, 0f, 8.6f);

        // Konsol istasyonu
        SupplyBin temp = Place<SupplyBin>(supplyBinPrefab,
            $"Drone Konsolu [{zone}]", consolePos);
        DroneConsole console = null;
        if (temp != null)
        {
            GameObject obj = temp.gameObject;
            if (Application.isPlaying) Destroy(temp);
            else                       DestroyImmediate(temp);

            console = obj.AddComponent<DroneConsole>();
            StationVisuals.DecorateDroneConsole(obj, accent);
        }

        // Park pedi
        CreatePad($"Drone Pedi [{zone}]", padPos, new Vector2(2f, 2f), accent);

        // Drone — prefabsız, görselini kendi kurar
        GameObject droneObj = new GameObject($"Tedarik Drone [{zone}]");
        droneObj.transform.SetParent(transform);
        droneObj.transform.position = MapPos(padPos) + Vector3.up * 0.9f;

        SupplyDrone drone = droneObj.AddComponent<SupplyDrone>();
        Configure(drone, "isPlayerTeam", sign < 0);
        Configure(drone, "homePosition", MapPos(padPos));
        drone.BuildVisual();

        // Kırmızı drone'u AI sürer
        if (sign > 0) droneObj.AddComponent<DroneAIPilot>();

        if (console != null) Configure(console, "drone", drone);
        return drone;
    }

    /// <summary>
    /// Ön duvarın ötesinde uçarak ulaşılan ödül platformu + bariyerler +
    /// DroneRaidZone yöneticisi. Oyuncu yürüyerek giremez (duvar + boşluk).
    /// </summary>
    private void BuildDroneRaidZone()
    {
        float halfD   = garageDepth / 2f;
        float coreZ   = halfD + CoreZoneGap + CoreZoneDepth / 2f;
        Vector3 center = new Vector3(0f, 0f, coreZ);

        // Platform zemini
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Çekirdek Platform Zemini";
        floor.transform.SetParent(transform);
        floor.transform.position   = MapPos(new Vector3(0f, -0.5f, coreZ));
        floor.transform.localScale = new Vector3(CoreZoneWidth, 1f, CoreZoneDepth);
        ApplyColor(floor, neutralFloor);

        // Neon çerçeve + köşe direkleri — çekirdek bölge kimliği
        CreatePad("Çekirdek Çerçeve (ön)",
            new Vector3(0f, 0f, coreZ + CoreZoneDepth / 2f - 0.4f),
            new Vector2(CoreZoneWidth - 0.6f, 0.35f), coreAccent);
        CreatePad("Çekirdek Çerçeve (arka)",
            new Vector3(0f, 0f, coreZ - CoreZoneDepth / 2f + 0.4f),
            new Vector2(CoreZoneWidth - 0.6f, 0.35f), coreAccent);
        CreatePad("Çekirdek Çerçeve (sol)",
            new Vector3(-CoreZoneWidth / 2f + 0.4f, 0f, coreZ),
            new Vector2(0.35f, CoreZoneDepth - 0.6f), coreAccent);
        CreatePad("Çekirdek Çerçeve (sağ)",
            new Vector3(CoreZoneWidth / 2f - 0.4f, 0f, coreZ),
            new Vector2(0.35f, CoreZoneDepth - 0.6f), coreAccent);

        // Enerji bariyerleri — kapalıyken platformu çevreler, açılınca gömülür
        Transform[] barriers = new Transform[4];
        barriers[0] = CreateBarrier("Bariyer (ön)",
            new Vector3(0f, 0f, coreZ + CoreZoneDepth / 2f),
            new Vector3(CoreZoneWidth, 6f, 0.3f));
        barriers[1] = CreateBarrier("Bariyer (arka)",
            new Vector3(0f, 0f, coreZ - CoreZoneDepth / 2f),
            new Vector3(CoreZoneWidth, 6f, 0.3f));
        barriers[2] = CreateBarrier("Bariyer (sol)",
            new Vector3(-CoreZoneWidth / 2f, 0f, coreZ),
            new Vector3(0.3f, 6f, CoreZoneDepth));
        barriers[3] = CreateBarrier("Bariyer (sağ)",
            new Vector3(CoreZoneWidth / 2f, 0f, coreZ),
            new Vector3(0.3f, 6f, CoreZoneDepth));

        // Zone yöneticisi + kablolar
        GameObject zoneObj = new GameObject("Çekirdek Bölge");
        zoneObj.transform.SetParent(transform);
        zoneObj.transform.position = MapPos(center);

        DroneRaidZone zone = zoneObj.AddComponent<DroneRaidZone>();
        Configure(zone, "platformCenter", MapPos(center));
        Configure(zone, "platformSize",   new Vector2(CoreZoneWidth, CoreZoneDepth));
        Configure(zone, "mapEdgeZ",       transform.position.z + halfD);
        Configure(zone, "barriers",       barriers);
        Configure(zone, "blueDrone",      blueDrone);
        Configure(zone, "redDrone",       redDrone);
        TryAssignPrefab(zone, "itemPrefab", "PlasmaCore_Prefab");

        // Drone uçuş sınırları (dünya koordinatı): tüm harita + platform
        float mapLeft  = teamACenter - garageWidth / 2f;
        float mapRight = teamBCenter + garageWidth / 2f;
        Vector4 bounds = new Vector4(
            transform.position.x + mapLeft  + 1f,
            transform.position.x + mapRight - 1f,
            transform.position.z - halfD + 1f,
            transform.position.z + coreZ + CoreZoneDepth / 2f - 0.5f);
        Configure(blueDrone, "flightBounds", bounds);
        Configure(redDrone,  "flightBounds", bounds);
    }

    /// <summary>Yüksek enerji duvarı — DroneRaidZone açılınca gömer.</summary>
    private Transform CreateBarrier(string barrierName, Vector3 pos, Vector3 scale)
    {
        GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = barrierName;
        barrier.transform.SetParent(transform);
        barrier.transform.position   = MapPos(new Vector3(pos.x, scale.y / 2f, pos.z));
        barrier.transform.localScale = scale;

        // Bariyer görsel + mantık DroneRaidZone.MaxAllowedZ'de — fiziği yok
        if (barrier.TryGetComponent<Collider>(out Collider col))
            DestroyImmediate(col);

        ApplyColor(barrier, barrierColor);
        return barrier.transform;
    }

    private void BuildWeaponCraft(Vector3 pos, ItemType input, ItemType output,
        string trName, string inputTrName)
    {
        WeaponCraftStation w = Place<WeaponCraftStation>(weaponCraftStationPrefab,
            $"Silah Atölyesi - {trName}", pos);
        Configure(w, "inputType",        input);
        Configure(w, "outputWeaponType", output);
        TryAssignPrefab(w, "outputPrefab", "Weapon_Prefab");
        StationVisuals.DecorateWeaponCraft(w?.gameObject,
            input, output, trName, inputTrName);

        CreatePad($"İstasyon Pedi - {trName}",
            pos, new Vector2(2.4f, 2.4f), neutralLight, 0.025f);
    }

    // ── Zemin, Duvar & Dekor Primitifleri ────────────────────────────────

    private void CreateFloor(string floorName, float centerX, float width, Color color)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = floorName;
        floor.transform.SetParent(transform);
        floor.transform.position   = MapPos(new Vector3(centerX, -0.5f, 0f));
        floor.transform.localScale = new Vector3(width, 1f, garageDepth);
        ApplyColor(floor, color);
    }

    /// <summary>Zeminin üstünde ince renkli plaka (şerit/ped/yol).</summary>
    private void CreatePad(string padName, Vector3 pos, Vector2 size,
        Color color, float yOffset = 0.02f)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = padName;
        pad.transform.SetParent(transform);
        pad.transform.position   = MapPos(new Vector3(pos.x, yOffset, pos.z));
        pad.transform.localScale = new Vector3(size.x, 0.04f, size.y);

        // Ped dekoratif — oyuncuya takılmasın
        if (pad.TryGetComponent<Collider>(out Collider col))
            DestroyImmediate(col);

        ApplyColor(pad, color);
    }

    private void CreateCrate(string crateName, Vector3 pos, float size, float yRot)
    {
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = crateName;
        crate.transform.SetParent(transform);
        crate.transform.position   = MapPos(new Vector3(pos.x, size / 2f, pos.z));
        crate.transform.rotation   = Quaternion.Euler(0f, yRot, 0f);
        crate.transform.localScale = Vector3.one * size;
        ApplyColor(crate, crateGray);
    }

    private void CreatePillar(string pillarName, Vector3 pos)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = pillarName;
        pillar.transform.SetParent(transform);
        pillar.transform.position   = MapPos(new Vector3(pos.x, 1.25f, pos.z));
        pillar.transform.localScale = new Vector3(1.2f, 2.5f, 1.2f);
        ApplyColor(pillar, neutralLight);
    }

    private void CreateWalls()
    {
        float mapLeft  = teamACenter - garageWidth / 2f;
        float mapRight = teamBCenter + garageWidth / 2f;
        float halfD    = garageDepth / 2f;
        float wallY    = wallHeight / 2f;

        CreateWall("Duvar - Sol",
            new Vector3(mapLeft - wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, garageDepth));

        CreateWall("Duvar - Sağ",
            new Vector3(mapRight + wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, garageDepth));

        CreateWall("Duvar - Ön",
            new Vector3(0f, wallY, halfD + wallThickness / 2f),
            new Vector3(totalWidth + wallThickness * 2f, wallHeight, wallThickness));

        CreateWall("Duvar - Arka",
            new Vector3(0f, wallY, -halfD - wallThickness / 2f),
            new Vector3(totalWidth + wallThickness * 2f, wallHeight, wallThickness));

        // Garaj-hurdalık ayraçları: ortada geçit kalır, ışıklar takım renginde
        CreateDivider("Ayraç - Mavi",    teamACenter + garageWidth / 2f, blueAccent);
        CreateDivider("Ayraç - Kırmızı", teamBCenter - garageWidth / 2f, redAccent);
    }

    private void CreateDivider(string dividerName, float x, Color lightColor)
    {
        float wallY   = wallHeight / 2f;
        float segLen  = garageDepth / 4f;
        float segMidZ = garageDepth / 2f - segLen / 2f;

        CreateWall($"{dividerName} (üst)",
            new Vector3(x, wallY, segMidZ),
            new Vector3(wallThickness, wallHeight, segLen), lightColor);

        CreateWall($"{dividerName} (alt)",
            new Vector3(x, wallY, -segMidZ),
            new Vector3(wallThickness, wallHeight, segLen), lightColor);
    }

    private void CreateWall(string wallName, Vector3 position, Vector3 scale)
        => CreateWall(wallName, position, scale, new Color(0.20f, 0.70f, 0.95f));

    private void CreateWall(string wallName, Vector3 position, Vector3 scale,
        Color lightColor)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(transform);
        wall.transform.position   = MapPos(position);
        wall.transform.localScale = scale;
        ApplyColor(wall, wallTone);

        // Neon tepe şeridi — duvar boyunca ışık hattı (fütüristik kimlik)
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = $"{wallName} (ışık)";
        strip.transform.SetParent(transform);
        strip.transform.position = MapPos(position) +
            Vector3.up * (scale.y / 2f + 0.04f);
        strip.transform.localScale = new Vector3(
            Mathf.Max(scale.x * 0.98f, 0.1f), 0.07f,
            Mathf.Max(scale.z * 0.98f, 0.1f));

        if (strip.TryGetComponent<Collider>(out Collider col))
            DestroyImmediate(col);

        ApplyColor(strip, lightColor);
    }

    // ── Yerleştirme & Yapılandırma ───────────────────────────────────────

    private T Place<T>(GameObject prefab, string stationName, Vector3 pos)
        where T : Component
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[MapGenerator] '{stationName}' için prefab atanmamış!");
            return null;
        }

        GameObject obj = Instantiate(prefab, MapPos(pos), Quaternion.identity, transform);
        obj.name = stationName;

        T comp = obj.GetComponent<T>();
        if (comp == null)
            Debug.LogError($"[MapGenerator] '{stationName}' prefabında " +
                           $"{typeof(T).Name} bileşeni yok!");
        return comp;
    }

    /// <summary>Harita, generator objesinin pozisyonuna göre kurulur.</summary>
    private Vector3 MapPos(Vector3 local) => transform.position + local;

    private void Configure(Component target, string field, object value)
    {
        if (target == null) return;
        UIFactory.SetField(target, field, value);
    }

    /// <summary>Editor'de item/silah prefabını isimle bulup bağlar.</summary>
    private void TryAssignPrefab(Component target, string field, string prefabName)
    {
        if (target == null) return;
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{prefabName} t:Prefab");
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[MapGenerator] '{prefabName}' bulunamadı — " +
                             $"'{target.gameObject.name}' prefab varsayılanını kullanacak.");
            return;
        }

        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        UIFactory.SetField(target, field, prefab);
#endif
    }

    // ── Sahne Bağlantıları ───────────────────────────────────────────────

    private void WireSceneReferences(List<RobotChassis> blueChassis, Transform blueSpawn)
    {
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && blueChassis.Count > 0)
        {
            UIFactory.SetField(gm, "playerChassis", blueChassis.ToArray());
            Debug.Log($"[MapGenerator] GameManager'a {blueChassis.Count} şasi bağlandı.");
        }

        OfflinePlayerSpawner spawner = FindFirstObjectByType<OfflinePlayerSpawner>();
        if (spawner != null && blueSpawn != null)
        {
            UIFactory.SetField(spawner, "spawnPoint", blueSpawn);
            Debug.Log("[MapGenerator] OfflinePlayerSpawner spawn noktasına bağlandı.");
        }

        RobotChassis[] allChassis =
            FindObjectsByType<RobotChassis>(FindObjectsSortMode.None);

        RobotStatusUI statusUI = FindFirstObjectByType<RobotStatusUI>();
        if (statusUI != null)
        {
            UIFactory.SetField(statusUI, "chassisList", allChassis);
            Debug.Log("[MapGenerator] RobotStatusUI şasi listesi güncellendi.");
        }

        ArmorSelectUI armorUI = FindFirstObjectByType<ArmorSelectUI>();
        if (armorUI != null)
        {
            UIFactory.SetField(armorUI, "chassisList", blueChassis.ToArray());
            Debug.Log("[MapGenerator] ArmorSelectUI şasi listesi güncellendi.");
        }
    }

    private void WarnAboutOrphanStations()
    {
        foreach (BaseStation s in FindObjectsByType<BaseStation>(FindObjectsSortMode.None))
        {
            if (!s.transform.IsChildOf(transform))
                Debug.LogWarning($"[MapGenerator] ⚠️ Haritaya ait olmayan eski istasyon: " +
                                 $"'{s.gameObject.name}' — kopya olmaması için sil!");
        }
    }

    // ── Temizlik & Yardımcılar ───────────────────────────────────────────

    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        Debug.Log("[MapGenerator] Harita temizlendi.");
    }

    private void ApplyColor(GameObject obj, Color color)
    {
        // Materyaller StationVisuals'ta merkezi üretilir — RP uyumlu, önbellekli
        if (!obj.TryGetComponent<Renderer>(out Renderer rend)) return;
        rend.sharedMaterial = StationVisuals.GetMaterial(color);
    }

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    // ── Gizmos ───────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        float tAC = -(garageWidth + scrapyardWidth) / 2f;
        float tBC =  (garageWidth + scrapyardWidth) / 2f;
        Vector3 c = transform.position;

        Gizmos.color = new Color(0.3f, 0.5f, 1f);
        Gizmos.DrawWireCube(c + new Vector3(tAC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth));

        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(c,
            new Vector3(scrapyardWidth, 0.1f, garageDepth));

        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        Gizmos.DrawWireCube(c + new Vector3(tBC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth));
    }
}
