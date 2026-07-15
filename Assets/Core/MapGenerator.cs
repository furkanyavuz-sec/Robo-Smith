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
using UnityEngine.Rendering.Universal;

public class MapGenerator : MonoBehaviour
{
    [Header("Harita Boyutları")]
    [SerializeField] private float garageWidth    = 20f;
    [SerializeField] private float garageDepth    = 20f;
    [SerializeField] private float wallThickness  = 0.5f;
    // wallHeight kaldırıldı — kapalı fabrikada FactoryWallH sabiti geçerli

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

    // ── Harita/bölge boyutları (KOD İÇİNDE — tek denge noktası) ──────────
    // YENİ YERLEŞİM (v3): mahalledeki teknoloji garajı.
    //   [Cadde/mahalle]  ←  -z dışarısı
    //   [Mavi Garaj][alçak ayraç][Kırmızı Garaj]   ← yan yana, birbirini görür
    //   [Hurdalık şeridi — garajların ÜST kapısından girilir]
    //   [Çekirdek Bölge — hurdalığın ötesi, sadece drone]
    private const float GarageGap    = 1.2f;   // İki garaj arası (alçak ayraç)
    private const float ScrapDepth   = 16f;    // Hurdalık şeridi derinliği
    private const float DoorWidth    = 4f;     // Garaj üst kapısı genişliği

    // v5: AÇIK teknoloji atölyesi (tavan yok — kullanıcı istemedi);
    // drone bölgesi içerideki yüksek sevkiyat rafında kalır
    private const float FactoryWallH = 4.0f;   // Dış duvar yüksekliği
    private const float ShelfWidth   = 32f;    // Raf genişliği (x)
    private const float ShelfDepth   = 4f;     // Raf derinliği (z 22..26)
    private const float ShelfTopY    = 3.4f;   // Raf üst yüzü (ödüller burada)

    [Header("Çekirdek Bölge (Drone Raid)")]
    [SerializeField] private Color barrierColor  = new Color(0.95f, 0.30f, 0.20f);
    [SerializeField] private Color coreAccent    = new Color(0.20f, 0.85f, 0.90f);

    [Header("Görsel Tema (Sci-Fi kit — boşsa primitif görseller)")]
    [SerializeField] private MapTheme theme;

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

        // Tema istasyon kabuklarına da işlesin (Decorate* çağrılarından önce)
        StationVisuals.SetTheme(theme);

        // Runtime tema erişimi (RobotBodyBuilder arena gövdesi buradan okur)
        GameObject themeRefObj = new GameObject("Theme Ref");
        themeRefObj.transform.SetParent(transform);
        themeRefObj.AddComponent<ThemeRef>().theme = theme;

        totalWidth  = garageWidth * 2f + GarageGap;
        teamACenter = -(garageWidth + GarageGap) / 2f;
        teamBCenter =  (garageWidth + GarageGap) / 2f;
        float scrapCenterZ = garageDepth / 2f + ScrapDepth / 2f;

        // Zeminler — garajlar yan yana, hurdalık üstte şerit
        CreateFloor("Zemin - Mavi Garaj",    teamACenter, 0f,
            garageWidth, garageDepth, blueFloor);
        CreateFloor("Zemin - Kırmızı Garaj", teamBCenter, 0f,
            garageWidth, garageDepth, redFloor);
        CreateFloor("Zemin - Hurdalık",      0f, scrapCenterZ,
            totalWidth, ScrapDepth, neutralFloor,
            HasTheme ? theme.scrapFloorTile : null);

        CreateWalls();

        // Mahalle: caddeler, kaldırım, sokak lambaları, çevre binalar
        BuildNeighborhood();

        // Bloom/vignette + kamera post-processing — neonlar gerçekten parlar
        BuildPostProcessing();

        // Garajlar (aynalı) + tarafsız orta bölge
        List<RobotChassis> blueChassis = BuildGarage(teamACenter, -1, "Mavi",
            blueAccent, out Transform blueSpawn);
        BuildGarage(teamBCenter, +1, "Kırmızı", redAccent, out Transform _);
        BuildScrapyard();

        // Hurdalık Penceresi: orta şerit zamanlı açılan yaya yağma alanı
        BuildScrapWindow();

        // Çekirdek bölge: ön duvarın ötesinde, sadece drone'la ulaşılan platform
        BuildDroneRaidZone();

        // MP Faz 1: server-authoritative faz/timer köprüsü (in-scene NetworkObject —
        // NGO sahneyi yükleyince server'da spawn eder, client'la eşleştirir)
        GameObject netState = new GameObject("Network Game State");
        netState.transform.SetParent(transform);
        netState.transform.position = MapPos(Vector3.zero);
        netState.AddComponent<Unity.Netcode.NetworkObject>();
        netState.AddComponent<NetworkGameState>();

        // MP Faz 3: etkinlik bölgesi saat/durum senkronu + olay relay'i
        netState.AddComponent<EventZoneSync>();

        // Dünya zemini: tesis uzayda süzülmesin — haritanın çevresine
        // dev beton saha (üstü harita zemininin 5cm altında, z-fight yok)
        BuildOuterGround();

        // Atmosfer: duvar dibi dekorları + kit gökyüzü
        if (HasTheme)
        {
            BuildWallDecor();
            if (theme.skybox != null)
                RenderSettings.skybox = theme.skybox;
        }

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

#if UNITY_EDITOR
    // ── Sevimli Şehir tema bağlayıcısı ───────────────────────────────────
    // Pandazole City/Nature + SimplePoly City paketlerinden CuteCityTheme
    // üretir: MAHALLE (binalar, yollar, araçlar, ağaçlar, sokak propları)
    // + oyuncu karakteri + prosedürel güneşli gökyüzü.
    // Atölye İÇİ alanları bilinçli BOŞ bırakılır — kullanıcı iç mekân
    // paketini seçince ayrıca bağlanacak; o zamana dek primitif yer tutucu.

    private const string PandaCity =
        "Assets/Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/";
    private const string PandaNature =
        "Assets/Pandazole_Ultimate_Pack/Pandazole Nature Environment Pack/Prefabs/";
    private const string SPoly =
        "Assets/SimplePoly City - Low Poly Assets/Prefab/";
    private const string Ithappy =
        "Assets/ithappy/Cartoon_City_Free/Prefabs/";
    private const string CuteThemePath = "Assets/CuteCityTheme.asset";

    [ContextMenu("Wire Cute City Theme")]
    private void WireCuteCityTheme()
    {
        MapTheme t = UnityEditor.AssetDatabase
            .LoadAssetAtPath<MapTheme>(CuteThemePath);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<MapTheme>();
            UnityEditor.AssetDatabase.CreateAsset(t, CuteThemePath);
        }

        // Mahalle binaları — iki paketten karışık (çeşitlilik)
        t.cityBuildings = new[]
        {
            LoadPrefabAt(PandaCity + "Env_ResidentBuilding_01.prefab"),
            LoadPrefabAt(PandaCity + "Env_ResidentBuilding_03.prefab"),
            LoadPrefabAt(PandaCity + "Env_CommercialBuilding_01.prefab"),
            LoadPrefabAt(PandaCity + "Env_CommercialBuilding_03.prefab"),
            LoadPrefabAt(PandaCity + "Env_CompanyBuilding_02.prefab"),
            LoadPrefabAt(PandaCity + "Env_Motel_02.prefab"),
            LoadPrefabAt(Ithappy + "Buildings/Eco_Building_Grid.prefab"),
            LoadPrefabAt(Ithappy + "Buildings/Eco_Building_Terrace.prefab"),
            LoadPrefabAt(SPoly + "Buildings/Building Sky_small_color01.prefab"),
            LoadPrefabAt(SPoly + "Buildings/Building Sky_small_color03.prefab"),
        };

        t.roadStraight = LoadPrefabAt(PandaCity + "Env_Road_Straight_01.prefab");
        t.streetLight  = LoadPrefabAt(SPoly + "Props/Props_Street Light.prefab");

        t.cityCars = new[]
        {
            LoadPrefabAt(SPoly + "Vehicles/Vehicle with Static Wheels/Vehicle_Car_color01.prefab"),
            LoadPrefabAt(SPoly + "Vehicles/Vehicle with Static Wheels/Vehicle_Car_color02.prefab"),
            LoadPrefabAt(SPoly + "Vehicles/Vehicle with Static Wheels/Vehicle_Car_color03.prefab"),
            LoadPrefabAt(SPoly + "Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color01.prefab"),
        };

        t.cityTrees = new[]
        {
            LoadPrefabAt(SPoly + "Natures/Natures_Big Tree.prefab"),
            LoadPrefabAt(SPoly + "Natures/Natures_Fir Tree.prefab"),
            LoadPrefabAt(SPoly + "Natures/Natures_Cube Tree.prefab"),
            LoadPrefabAt(Ithappy + "Vegetation/Palm_03.prefab"),
        };

        t.cityBushes = new[]
        {
            LoadPrefabAt(PandaNature + "Bush_03.prefab"),
            LoadPrefabAt(PandaNature + "Bush_08.prefab"),
            LoadPrefabAt(PandaNature + "HardRock_02.prefab"),
        };

        t.cityProps = new[]
        {
            LoadPrefabAt(SPoly + "Props/Props_Bus Stop.prefab"),
            LoadPrefabAt(SPoly + "Props/Props_Hydrant.prefab"),
            LoadPrefabAt(SPoly + "Props/Props_Bench_1.prefab"),
            LoadPrefabAt(PandaCity + "Prop_StreetSign_Major.prefab"),
            LoadPrefabAt(PandaCity + "Prop_RoadCone_01.prefab"),
            LoadPrefabAt(Ithappy + "Props/Fountain_03.prefab"),
            LoadPrefabAt(Ithappy + "Props/traffic_light_001.prefab"),
        };

        // Oyuncu karakteri (FreeLowPolyRobot kalıcı) + URP atlas materyali
        t.playerCharacter = LoadPrefabAt(
            "Assets/FreeLowPolyRobot/Meshes_and_Animations/" +
            "RandomModularRobots_Prefab.prefab");

        const string urpMatPath = "Assets/FreeLowPolyRobot/Materials/M_AtlasURP.mat";
        Material urpMat = UnityEditor.AssetDatabase
            .LoadAssetAtPath<Material>(urpMatPath);
        if (urpMat == null)
        {
            RenderPipelineAsset rp = GraphicsSettings.defaultRenderPipeline;
            Shader sh = rp != null ? rp.defaultShader : Shader.Find("Standard");
            urpMat = new Material(sh);
            UnityEditor.AssetDatabase.CreateAsset(urpMat, urpMatPath);
        }
        Texture2D atlas = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/FreeLowPolyRobot/Materials/T_ColorAtlas.png");
        if (atlas != null) urpMat.mainTexture = atlas;
        UnityEditor.EditorUtility.SetDirty(urpMat);
        t.playerCharacterMaterial = urpMat;

        // Gökyüzü: prosedürel güneşli gündüz (eski paket skybox'ları silindi)
        const string skyPath = "Assets/CuteSkybox.mat";
        Material sky = UnityEditor.AssetDatabase
            .LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            UnityEditor.AssetDatabase.CreateAsset(sky, skyPath);
        }
        sky.SetFloat("_Exposure", 1.25f);
        sky.SetColor("_SkyTint", new Color(0.52f, 0.74f, 1f));
        sky.SetColor("_GroundColor", new Color(0.78f, 0.80f, 0.74f));
        UnityEditor.EditorUtility.SetDirty(sky);
        t.skybox = sky;

        // ── Atölye içi: sahip olunan paketlerin teknik proplarıyla ──────
        // Zemin/duvar pastel prosedürel kalır (aşağıdaki palet) — kit
        // karosu yok; istasyon gövdeleri sevimli "cihaz" propları
        t.floorTile = null;      t.scrapFloorTile = null;
        t.platformFloor = null;  t.depotBase = null;
        t.wallPanel = null;      t.windowPanel = null;
        t.ceilingTile = null;    t.ceilingBeam = null;
        t.pillar = null;         t.barrierFence = null;

        t.supplyShell    = LoadPrefabAt(PandaCity + "Prop_ElectracityCabinet_01.prefab");
        t.processorShell = LoadPrefabAt(PandaCity + "Prop_ElectracityCabinet_02.prefab");
        t.weaponShell    = LoadPrefabAt(PandaCity + "Prop_ElectracityCabinet_03.prefab");
        t.assemblyShell  = LoadPrefabAt(PandaCity + "Prop_ACVent_Cross.prefab");
        t.trashShell     = LoadPrefabAt(Ithappy + "Props/Trash_Can_04.prefab");
        t.consoleShell   = LoadPrefabAt(SPoly + "Props/Props_BillBoard_small.prefab");
        t.plasmaShell    = LoadPrefabAt(PandaCity + "Prop_OilBerrel_01.prefab");
        t.crate          = LoadPrefabAt(Ithappy + "Props/Trash_06.prefab");
        t.stationBase    = null;
        t.chassisPedestal = LoadPrefabAt(SPoly + "Props/Props_Roof Helipad.prefab");

        // İç dekor: spot ışığı + havalandırma + baca (duvar dipleri)
        t.decorProps = new[]
        {
            LoadPrefabAt(Ithappy + "Props/Spotlight_01.prefab"),
            LoadPrefabAt(PandaCity + "Prop_ACVent_Stright.prefab"),
            LoadPrefabAt(PandaCity + "Prop_Chimney_01.prefab"),
        };

        // Arena savaş robotu — polyart savaşçı (animatörü prefabla gelir)
        t.battleCharacter = LoadPrefabAt(
            "Assets/SciFiWarriorPBRHPPolyart/Prefabs/PolyartCharacter.prefab");

        // Arena robot primitif parçaları devre dışı (hero gövde kullanılır)
        t.robotCore = null;      t.robotPlate = null;
        t.robotJoint = null;     t.robotBackpack = null;

        // ── Item şekilleri: tip → paket propu (renk otomatik biner) ─────
        t.itemShapes = new[]
        {
            Shape(ItemType.Iron,         PandaNature + "HardRock_03.prefab"),
            Shape(ItemType.RawPlasma,    PandaNature + "HardRock_08.prefab"),
            Shape(ItemType.Circuit,      SPoly + "Props/Props_BillBoard_small.prefab"),
            Shape(ItemType.SteelPlate,   SPoly + "Natures/Natures_House Floor.prefab"),
            Shape(ItemType.PlasmaCore,   PandaCity + "Prop_RoofVent_02.prefab"),
            Shape(ItemType.Microchip,    PandaCity + "Prop_Intel_02.prefab"),
            Shape(ItemType.ScrapMetal,   Ithappy + "Props/Trash_02.prefab"),
            Shape(ItemType.CrystalShard, PandaNature + "Coral_05.prefab"),
            Shape(ItemType.RocketFuel,   PandaCity + "Prop_OilBerrel_01.prefab"),
            Shape(ItemType.ShieldAlloy,  PandaCity + "Prop_StreetSign_Empty.prefab"),
            Shape(ItemType.EMPCore,      PandaCity + "Prop_RoofVent_06.prefab"),
            // Silah paketleri tek silüet (renk ayırt eder), modüller cihaz
            Shape(ItemType.Sword,  SPoly + "Props/Props_Roof Antenna.prefab"),
            Shape(ItemType.Laser,  SPoly + "Props/Props_Roof Antenna.prefab"),
            Shape(ItemType.Rocket, SPoly + "Props/Props_Roof Antenna.prefab"),
            Shape(ItemType.Shield, SPoly + "Props/Props_Roof Antenna.prefab"),
            Shape(ItemType.EMP,    SPoly + "Props/Props_Roof Antenna.prefab"),
            Shape(ItemType.RepairModule,      PandaCity + "Prop_Intel_04.prefab"),
            Shape(ItemType.OverdriveModule,   PandaCity + "Prop_Intel_04.prefab"),
            Shape(ItemType.TargetingComputer, PandaCity + "Prop_Intel_04.prefab"),
        };

        // ── Pastel palet: iç mekân renkleri şehirle aynı dile çekilir ───
        // (Inspector'daki eski koyu değerleri SetField ile ezer)
        Configure(this, "blueFloor",    new Color(0.72f, 0.80f, 0.93f));
        Configure(this, "redFloor",     new Color(0.95f, 0.79f, 0.76f));
        Configure(this, "blueAccent",   new Color(0.33f, 0.62f, 0.96f));
        Configure(this, "redAccent",    new Color(0.96f, 0.45f, 0.40f));
        Configure(this, "neutralFloor", new Color(0.90f, 0.89f, 0.85f));
        Configure(this, "neutralMid",   new Color(0.82f, 0.81f, 0.77f));
        Configure(this, "neutralLight", new Color(0.95f, 0.94f, 0.90f));
        Configure(this, "crateGray",    new Color(0.80f, 0.78f, 0.74f));
        Configure(this, "wallTone",     new Color(0.86f, 0.84f, 0.80f));
        Configure(this, "barrierColor", new Color(0.98f, 0.62f, 0.35f));
        Configure(this, "coreAccent",   new Color(0.20f, 0.80f, 0.85f));

        UnityEditor.EditorUtility.SetDirty(t);

        theme = t;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();

        Debug.Log("[MapGenerator] ✅ Sevimli Şehir teması bağlandı " +
                  "(CuteCityTheme.asset). Şimdi Generate Map + Ctrl+S.");
    }

    private static MapTheme.ItemShape Shape(ItemType type, string assetPath) =>
        new MapTheme.ItemShape { type = type, prefab = LoadPrefabAt(assetPath) };

    private static GameObject LoadPrefabAt(string assetPath)
    {
        GameObject p = UnityEditor.AssetDatabase
            .LoadAssetAtPath<GameObject>(assetPath);
        if (p == null)
            Debug.LogWarning("[MapGenerator] Prefab bulunamadı: " +
                             assetPath + " — bu parça atlanacak.");
        return p;
    }
#endif

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

                // MP Faz 2B: şasi durumunun client aynası (statlar, silahlar,
                // zırh — RobotStatusUI/hologram iki tarafta da doğru gösterir)
                if (chassis.GetComponent<ChassisSync>() == null)
                    chassis.gameObject.AddComponent<ChassisSync>();

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

        // Garaj aydınlatması — takım tonlu iki tepe ışığı
        Color glow = Color.Lerp(accent, Color.white, 0.55f);
        AddAreaLight($"Garaj Işığı 1", new Vector3(centerX, 3.6f, -4.5f),
            glow, 11f, 1.5f);
        AddAreaLight($"Garaj Işığı 2", new Vector3(centerX, 3.6f, 4.5f),
            glow, 11f, 1.5f);
    }

    // ── Tarafsız Orta Bölge (Hurdalık — gri tonlar) ──────────────────────

    private void BuildScrapyard()
    {
        // Hurdalık artık garajların ÜSTÜNDE yatay şerit: z 10..26.
        // Orta kolon (x ±7) kapan bölgesi; yan bantlar takım atölyeleri.
        float zC = garageDepth / 2f + ScrapDepth / 2f;   // Şerit merkezi

        // İç zemin katmanı — kenarlardan bir tık açık gri
        CreatePad("Hurdalık İç Zemin",
            new Vector3(0f, 0f, zC), new Vector2(totalWidth - 2f, ScrapDepth - 2f),
            neutralMid, 0.015f);

        // Kapan şeridi boyunca yürüyüş yolu
        CreatePad("Yürüyüş Yolu",
            new Vector3(0f, 0f, zC), new Vector2(2.2f, ScrapDepth - 1f),
            neutralLight, 0.03f);

        // Hurdalık tepe aydınlatması — soğuk endüstriyel beyaz
        AddAreaLight("Hurdalık Işığı",
            new Vector3(0f, 4.2f, zC), new Color(0.85f, 0.92f, 1f), 16f, 1.6f);

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
            float z = garageDepth / 2f + 2f + i * 3f;   // 12, 15, 18, 21, 24
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

        // Yan bantlar: her takım tarafında TAM atölye seti + plazma kaynağı.
        // Bantlar Hurdalık Penceresi bariyerlerinin (x ±7) DIŞINDA kalır —
        // pencere kapalıyken de silah üretimi ve plazma akışı hiç kilitlenmez.
        // Yan bantlar (x ±7 bariyerlerinin dışı): tam atölye seti + plazma —
        // pencere kapalıyken de üretim hiç kilitlenmez
        float bandZ0 = garageDepth / 2f + 1.5f;   // 11.5
        foreach (int sign in new[] { -1, +1 })
        {
            float bx = sign * 10.5f;
            string side = sign < 0 ? "Mavi Taraf" : "Kırmızı Taraf";

            BuildWeaponCraft(new Vector3(bx, 0f, bandZ0),          ItemType.ScrapMetal,   ItemType.Sword,  "Kılıç",  "Hurda Metal");
            BuildWeaponCraft(new Vector3(bx, 0f, bandZ0 + 3f),     ItemType.CrystalShard, ItemType.Laser,  "Lazer",  "Kristal Kıymık");
            BuildWeaponCraft(new Vector3(bx, 0f, bandZ0 + 6f),     ItemType.RocketFuel,   ItemType.Rocket, "Roket",  "Roket Yakıtı");
            BuildWeaponCraft(new Vector3(bx, 0f, bandZ0 + 9f),     ItemType.ShieldAlloy,  ItemType.Shield, "Kalkan", "Kalkan Alaşımı");
            BuildWeaponCraft(new Vector3(bx, 0f, bandZ0 + 12f),    ItemType.EMPCore,      ItemType.EMP,    "EMP",    "EMP Çekirdeği");

            PlasmaSource plasma = Place<PlasmaSource>(plasmaSourcePrefab,
                $"Plazma Kaynağı [{side}]",
                new Vector3(sign * 16f, 0f, bandZ0 + 6f));
            StationVisuals.DecoratePlasmaSource(plasma?.gameObject);
        }

        // Dekor: siper sandıkları — kapan şeridi içinde
        float zMid = garageDepth / 2f + ScrapDepth / 2f;
        CreateCrate("Sandık 1", new Vector3(-3.2f, 0f, zMid + 3.5f), 1.3f,  18f);
        CreateCrate("Sandık 2", new Vector3( 3.0f, 0f, zMid + 4.2f), 1.0f, -25f);
        CreateCrate("Sandık 3", new Vector3( 3.2f, 0f, zMid - 3.8f), 1.4f,  40f);
        CreateCrate("Sandık 4", new Vector3(-3.0f, 0f, zMid - 4.4f), 1.1f, -12f);
        CreateCrate("Sandık 5", new Vector3( 5.4f, 0f, zMid + 6.4f), 0.9f,  30f);
        CreateCrate("Sandık 6", new Vector3(-5.4f, 0f, zMid - 6.4f), 0.9f, -35f);

        // Dekor: kapan köşe sütunları
        CreatePillar("Sütun 1", new Vector3(-2.5f, 0f, zMid + 6.8f));
        CreatePillar("Sütun 2", new Vector3( 2.5f, 0f, zMid + 6.8f));
        CreatePillar("Sütun 3", new Vector3(-2.5f, 0f, zMid - 6.8f));
        CreatePillar("Sütun 4", new Vector3( 2.5f, 0f, zMid - 6.8f));
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

        // Kırmızı drone'u AI sürer (MP'de kendini kapatır — misafir sürer)
        if (sign > 0) droneObj.AddComponent<DroneAIPilot>();

        if (console != null) Configure(console, "drone", drone);

        // MP Faz 3: drone ağ objesi — owner-authoritative hareket (mavi
        // host'un, kırmızı misafirin) + mod/kapma-teslim köprüsü
        droneObj.AddComponent<Unity.Netcode.NetworkObject>();
        droneObj.AddComponent<ClientNetworkTransform>();
        DroneSync droneSync = droneObj.AddComponent<DroneSync>();
        Configure(droneSync, "drone",   drone);
        Configure(droneSync, "console", console);

        return drone;
    }

    /// <summary>
    /// Çekirdek Bölge (v4): fabrika içinde YÜKSEK SEVKİYAT RAFI — hurdalık
    /// üst duvarına bitişik, yerden 3.4m. Oyuncu ulaşamaz; drone 4.2'de
    /// uçarken üstünden kapar (kapma yatay mesafeye bakar). Ön yüzünde
    /// tek enerji perdesi: pencere açılınca yere gömülür.
    /// </summary>
    private void BuildDroneRaidZone()
    {
        float topZ    = garageDepth / 2f + ScrapDepth;        // 26
        float shelfZ  = topZ - ShelfDepth / 2f;               // Raf merkezi (24)
        float frontZ  = topZ - ShelfDepth;                    // Raf ön kenarı (22)
        Vector3 shelfTop = new Vector3(0f, ShelfTopY, shelfZ);

        // Raf tablası — kalın plaka, altı destek kolonlu
        if (HasTheme && theme.platformFloor != null)
        {
            FillBox(theme.platformFloor, "Sevkiyat Rafı",
                MapPos(new Vector3(0f, ShelfTopY - 0.2f, shelfZ)),
                new Vector3(ShelfWidth, 0.4f, ShelfDepth),
                keepCollider: true, stretchY: false);
        }
        else
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Sevkiyat Rafı";
            plate.transform.SetParent(transform);
            plate.transform.position   = MapPos(new Vector3(0f, ShelfTopY - 0.2f, shelfZ));
            plate.transform.localScale = new Vector3(ShelfWidth, 0.4f, ShelfDepth);
            ApplyColor(plate, neutralMid);
        }

        // Destek kolonları — raf altı
        for (int i = -2; i <= 2; i++)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = $"Raf Kolonu ({i})";
            leg.transform.SetParent(transform);
            leg.transform.position   = MapPos(new Vector3(
                i * (ShelfWidth / 2f - 1f) / 2f, (ShelfTopY - 0.4f) / 2f, shelfZ));
            leg.transform.localScale = new Vector3(
                0.45f, ShelfTopY - 0.4f, 0.45f);
            ApplyColor(leg, wallTone);
        }

        // Raf ön kenarı neon hattı — çekirdek bölge kimliği
        GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        edge.name = "Raf Neon Hattı";
        edge.transform.SetParent(transform);
        edge.transform.position   = MapPos(new Vector3(0f, ShelfTopY + 0.02f, frontZ + 0.15f));
        edge.transform.localScale = new Vector3(ShelfWidth - 0.4f, 0.07f, 0.12f);
        if (edge.TryGetComponent<Collider>(out Collider ec)) DestroyImmediate(ec);
        ApplyColor(edge, coreAccent);

        // Enerji perdesi — raf önünde, raf hizasından tavana; açılınca gömülür
        Transform[] barriers = new Transform[1];
        barriers[0] = CreateBarrier("Raf Perdesi",
            new Vector3(0f, 0f, frontZ),
            new Vector3(ShelfWidth, FactoryWallH - ShelfTopY + 0.2f, 0.3f));
        barriers[0].position += Vector3.up * (ShelfTopY - 0.2f);
        // (Bariyer animasyonu kapalı konumu Awake'te ezberler — yükseltilmiş
        //  pozisyon otomatik "kapalı" kabul edilir, gömülünce yere iner)

        // Zone yöneticisi + kablolar
        GameObject zoneObj = new GameObject("Çekirdek Bölge");
        zoneObj.transform.SetParent(transform);
        zoneObj.transform.position = MapPos(shelfTop);

        DroneRaidZone zone = zoneObj.AddComponent<DroneRaidZone>();
        Configure(zone, "platformCenter", MapPos(shelfTop));
        Configure(zone, "platformSize",   new Vector2(ShelfWidth, ShelfDepth));
        Configure(zone, "mapEdgeZ",       transform.position.z + frontZ);
        Configure(zone, "barriers",       barriers);
        Configure(zone, "blueDrone",      blueDrone);
        Configure(zone, "redDrone",       redDrone);
        TryAssignPrefab(zone, "itemPrefab", "PlasmaCore_Prefab");

        // Drone uçuş sınırları — fabrika içi (raf dahil)
        Vector4 bounds = new Vector4(
            transform.position.x - totalWidth / 2f + 1f,
            transform.position.x + totalWidth / 2f - 1f,
            transform.position.z - garageDepth / 2f + 1f,
            transform.position.z + topZ - 0.6f);
        Configure(blueDrone, "flightBounds", bounds);
        Configure(redDrone,  "flightBounds", bounds);
    }

    /// <summary>
    /// Takım deposu: ölçeksiz anchor (item'lar buna parent'lanır — çarpılma
    /// olmasın) + taban plakası + köşe direkleri + yüzen etiket.
    /// </summary>
    private Transform CreateDepot(string depotName, Vector3 pos, Color accent)
    {
        GameObject anchor = new GameObject(depotName);
        anchor.transform.SetParent(transform);
        anchor.transform.position = MapPos(pos);

        // Taban plakası — temalıysa kit modülü (yuvarlak küçük zemin)
        if (HasTheme && theme.depotBase != null)
        {
            GameObject kitBase = Instantiate(theme.depotBase, anchor.transform);
            kitBase.name = "Taban";
            FitToFootprint(kitBase, anchor.transform.position, 2.4f, 0.5f);
        }
        else
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Taban";
            plate.transform.SetParent(anchor.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            plate.transform.localScale    = new Vector3(2.2f, 0.12f, 2.2f);
            if (plate.TryGetComponent<Collider>(out Collider pc))
                DestroyImmediate(pc);
            ApplyColor(plate, Color.Lerp(accent, Color.black, 0.35f));
        }

        // Köşe direkleri
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Direk";
                post.transform.SetParent(anchor.transform, false);
                post.transform.localPosition = new Vector3(x * 1f, 0.5f, z * 1f);
                post.transform.localScale    = new Vector3(0.12f, 1f, 0.12f);
                if (post.TryGetComponent<Collider>(out Collider cc))
                    DestroyImmediate(cc);
                ApplyColor(post, accent);
            }

        StationVisuals.AddLabel(anchor, depotName, accent, 1.9f);
        return anchor.transform;
    }

    /// <summary>
    /// Yüksek enerji duvarı — zone yöneticileri açılınca gömer.
    /// keepCollider: oyunculara fiziksel engel gerekiyorsa true
    /// (drone bariyerleri mantıkla sınırlar, hurdalık bariyerleri collider'la).
    /// </summary>
    private Transform CreateBarrier(string barrierName, Vector3 pos, Vector3 scale,
        bool keepCollider = false)
    {
        if (HasTheme && theme.barrierFence != null)
        {
            // Zone yöneticileri container'ı gömer/kaldırır — parçalar child.
            // Çit native yükseklikte y'de istiflenir (esnetme çirkinliği yok)
            Transform box = FillBox(theme.barrierFence, barrierName,
                MapPos(new Vector3(pos.x, scale.y / 2f, pos.z)), scale,
                keepCollider, stretchY: false, stackY: true);

            // Enerji kimliği korunur: tepede barrierColor ışık şeridi —
            // "renkli hat = kapalı bölge" dili kit görseliyle birleşir
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = $"{barrierName} (enerji)";
            strip.transform.SetParent(box, worldPositionStays: false);
            strip.transform.localPosition = new Vector3(0f, scale.y / 2f + 0.06f, 0f);
            strip.transform.localScale    = new Vector3(
                Mathf.Max(scale.x * 0.98f, 0.14f), 0.10f,
                Mathf.Max(scale.z * 0.98f, 0.14f));
            if (strip.TryGetComponent<Collider>(out Collider sc))
                DestroyImmediate(sc);
            ApplyColor(strip, barrierColor);

            return box;
        }

        GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = barrierName;
        barrier.transform.SetParent(transform);
        barrier.transform.position   = MapPos(new Vector3(pos.x, scale.y / 2f, pos.z));
        barrier.transform.localScale = scale;

        if (!keepCollider && barrier.TryGetComponent<Collider>(out Collider col))
            DestroyImmediate(col);

        ApplyColor(barrier, barrierColor);
        return barrier.transform;
    }

    /// <summary>
    /// Hurdalık Penceresi: orta şeridi (x ±7) çevreleyen collider'lı
    /// bariyerler + ScrapWindowZone yöneticisi + rakip teknisyen.
    /// Ön/arka taraf zaten harita duvarlarıyla kapalı — 2 bariyer yeter.
    /// </summary>
    private void BuildScrapWindow()
    {
        // Kapan şeridi: x ±7, z 10..26 (hurdalık şeridinin orta kolonu)
        const float zoneHalfW = 7f;
        float zLo = garageDepth / 2f;              // 10
        float zHi = zLo + ScrapDepth;              // 26
        float zC  = (zLo + zHi) / 2f;              // 18

        Transform[] barriers = new Transform[2];
        barriers[0] = CreateBarrier("Hurdalık Bariyeri (Mavi taraf)",
            new Vector3(-zoneHalfW, 0f, zC),
            new Vector3(0.35f, 3.5f, ScrapDepth), keepCollider: true);
        barriers[1] = CreateBarrier("Hurdalık Bariyeri (Kırmızı taraf)",
            new Vector3(zoneHalfW, 0f, zC),
            new Vector3(0.35f, 3.5f, ScrapDepth), keepCollider: true);

        // Kapı ağızları (bariyer dışı, bant tarafı) — kapanış ışınlaması
        Vector3 blueGate = MapPos(new Vector3(-zoneHalfW - 1.6f, 0f, zC));
        Vector3 redGate  = MapPos(new Vector3( zoneHalfW + 1.6f, 0f, zC));

        GameObject zoneObj = new GameObject("Hurdalık Penceresi");
        zoneObj.transform.SetParent(transform);
        zoneObj.transform.position = MapPos(new Vector3(0f, 0f, zC));

        ScrapWindowZone zone = zoneObj.AddComponent<ScrapWindowZone>();
        Configure(zone, "zoneRect", new Vector4(
            transform.position.x - zoneHalfW, transform.position.x + zoneHalfW,
            transform.position.z + zLo,       transform.position.z + zHi));
        Configure(zone, "blueEvictPoint", blueGate);
        Configure(zone, "redEvictPoint",  redGate);
        Configure(zone, "barriers",       barriers);
        TryAssignPrefab(zone, "lootPrefab", "ScrapMetal_Prefab");

        // Takım depoları — kapanın garaj tarafı köşeleri
        Transform blueDepot = CreateDepot("Mavi Depo",
            new Vector3(-5.0f, 0f, zLo + 2.2f), blueAccent);
        Transform redDepot  = CreateDepot("Kırmızı Depo",
            new Vector3(5.0f, 0f, zLo + 2.2f), redAccent);
        Configure(zone, "blueDepotAnchor", blueDepot);
        Configure(zone, "redDepotAnchor",  redDepot);

        // Rakip teknisyen — kırmızı kapı ağzında bekler
        GameObject botObj = new GameObject("Rakip Teknisyen");
        botObj.transform.SetParent(transform);
        botObj.transform.position = redGate;

        TechnicianBot bot = botObj.AddComponent<TechnicianBot>();
        Configure(bot, "homePosition", redGate);
        bot.BuildVisual();
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

    // ── Tema (Sci-Fi kit) döşeme yardımcıları ────────────────────────────
    // Kit modülleri keyfî ölçeklenmez: hedef kutuya native ölçüye en yakın
    // tekrar sayısıyla döşenir, kalan fark hafif esnetmeyle kapanır.
    // Modül collider'ları kapatılır — oynanış collider'ı container'daki
    // BoxCollider'dır (keepCollider). Böylece görsel ne olursa olsun
    // fizik/etkileşim davranışı primitif sürümle birebir aynı kalır.

    /// <summary>Prefabın birleşik renderer boyutu (ölçeksiz, rotasyonsuz).</summary>
    private static Vector3 MeasureModule(GameObject prefab)
    {
        GameObject temp = Instantiate(prefab);
        temp.transform.position   = new Vector3(0f, -5000f, 0f);   // Sahne dışı
        temp.transform.rotation   = Quaternion.identity;
        temp.transform.localScale = Vector3.one;

        Bounds b     = default;
        bool   first = true;
        foreach (Renderer r in temp.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else         b.Encapsulate(r.bounds);
        }

        if (Application.isPlaying) Destroy(temp);
        else                       DestroyImmediate(temp);

        return first ? Vector3.one : b.size;
    }

    /// <summary>
    /// Kit modülünü verilen dünya kutusuna döşer. stretchY: modül y'de
    /// kutuya ölçeklenir (duvar/bariyer); false ise native yükseklikte
    /// üst yüze hizalanır (zemin karosu). Dönüş: container transform'u
    /// (bariyer animasyonu gibi hareket ettirilecek şeyler için).
    /// </summary>
    private Transform FillBox(GameObject module, string boxName, Vector3 center,
        Vector3 size, bool keepCollider, bool stretchY, bool stackY = false,
        bool upsideDown = false)
    {
        GameObject container = new GameObject(boxName);
        container.transform.SetParent(transform);
        container.transform.position = center;

        if (keepCollider)
            container.AddComponent<BoxCollider>().size = size;

        Vector3 m = MeasureModule(module);
        m.x = Mathf.Max(m.x, 0.01f);
        m.y = Mathf.Max(m.y, 0.01f);
        m.z = Mathf.Max(m.z, 0.01f);

        // Modül uzun ekseniyle kutu uzun ekseni uyuşmuyorsa 90° çevir
        bool rotate = (size.x >= size.z) != (m.x >= m.z);
        Vector3 eff = rotate ? new Vector3(m.z, m.y, m.x) : m;

        int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / eff.x));
        int nz = Mathf.Max(1, Mathf.RoundToInt(size.z / eff.z));
        // stackY: kutu yüksekliğini native ölçüde sıralarla doldur (çit
        // bariyeri gibi) — stretchY tek parçayı y'de esnetir
        int ny = stackY ? Mathf.Max(1, Mathf.RoundToInt(size.y / eff.y)) : 1;

        Vector3 cell   = new Vector3(size.x / nx, size.y / ny, size.z / nz);
        float   yScale = stretchY ? size.y / m.y
                       : stackY  ? cell.y / m.y : 1f;
        Vector3 pieceScale = rotate
            ? new Vector3(cell.z / m.x, yScale, cell.x / m.z)
            : new Vector3(cell.x / m.x, yScale, cell.z / m.z);

        float top = center.y + size.y / 2f;

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        for (int iy = 0; iy < ny; iy++)
        {
            GameObject piece = Instantiate(module, container.transform);
            piece.transform.rotation = Quaternion.Euler(
                upsideDown ? 180f : 0f, rotate ? 90f : 0f, 0f);
            piece.transform.localScale =
                Vector3.Scale(piece.transform.localScale, pieceScale);

            // Hedef hücre merkezi (y: esneyen/istiflenende hücre merkezi,
            // değilse üst yüze native yükseklikle otur)
            float cellY = stretchY ? center.y
                        : stackY  ? center.y - size.y / 2f + cell.y * (iy + 0.5f)
                                  : top - m.y * 0.5f;
            Vector3 cellCenter = new Vector3(
                center.x - size.x / 2f + cell.x * (ix + 0.5f),
                cellY,
                center.z - size.z / 2f + cell.z * (iz + 0.5f));

            // Pivot nerede olursa olsun bounds merkezini hücreye getir
            piece.transform.position = cellCenter;
            Bounds pb    = default;
            bool   first = true;
            foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
            {
                if (first) { pb = r.bounds; first = false; }
                else         pb.Encapsulate(r.bounds);
            }
            if (!first)
                piece.transform.position += cellCenter - pb.center;

            // Görsel modülün fiziği yok — oynanış collider'ı container'da
            foreach (Collider c in piece.GetComponentsInChildren<Collider>())
            {
                if (Application.isPlaying) Destroy(c);
                else                       DestroyImmediate(c);
            }
        }

        return container.transform;
    }

    /// <summary>Tema asset'i atanmış mı? (alan bazında ayrıca null kontrolü
    /// yapılır — kısmi doldurulmuş tema desteklenir)</summary>
    private bool HasTheme => theme != null;

    /// <summary>
    /// Tesisin oturduğu dev dış saha: koyu beton plaka + çevre aksan
    /// şeritleri. Üst yüzü -0.05'te — harita güvertesi 5cm taşar
    /// (temel üstü tesis görünümü), z-fight olmaz. Collider'lı: drone
    /// kovalarken harita dışına düşen item olursa yerde kalır.
    /// </summary>
    private void BuildOuterGround()
    {
        const float size = 400f;

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Dış Saha (beton)";
        ground.transform.SetParent(transform);
        ground.transform.position   = MapPos(new Vector3(0f, -0.55f, 0f));
        ground.transform.localScale = new Vector3(size, 1f, size);
        ApplyColor(ground, new Color(0.14f, 0.145f, 0.16f));

        // Tesis çevresi ikaz şeridi — "teknoloji sahası" kimliği
        float frontZ = garageDepth / 2f + ScrapDepth;   // Hurdalık üst sınırı
        float backZ  = -garageDepth / 2f;               // Cadde tarafı
        float w      = totalWidth + wallThickness * 4f;
        CreatePad("Saha Şeridi (ön)",
            new Vector3(0f,  0f,  frontZ + wallThickness * 2f + 0.6f),
            new Vector2(w, 0.5f), coreAccent, yOffset: -0.03f);
        CreatePad("Saha Şeridi (arka)",
            new Vector3(0f,  0f,  backZ - wallThickness * 2f - 0.6f),
            new Vector2(w, 0.5f), coreAccent, yOffset: -0.03f);
    }

    // ── Mahalle: caddeler, kaldırım, lambalar, çevre binalar ─────────────
    // Tamamı dekoratif (oynanış dışı) — tesis "gelişmiş bir mahalledeki
    // teknoloji garajı" gibi otursun. Binalar tema panellerinden döşenir,
    // tema yoksa koyu bloklar + neon şerit.

    private void BuildNeighborhood()
    {
        Color asphalt = new Color(0.095f, 0.095f, 0.11f);
        Color lane    = new Color(0.82f, 0.82f, 0.76f);
        Color walkway = new Color(0.55f, 0.55f, 0.52f);
        float backZ   = -garageDepth / 2f;

        // Deterministik dizilim: her Generate aynı mahalleyi kurar
        // (MP'de iki makine aynı sahneyi yükler — yine de garanti olsun)
        Random.State saved = Random.state;
        Random.InitState(4242);

        bool cute = HasTheme && theme.cityBuildings != null &&
                    theme.cityBuildings.Length > 0;

        // Kaldırım — tesisin cadde tarafı
        CreatePad("Kaldırım",
            new Vector3(0f, 0f, backZ - 2.4f),
            new Vector2(totalWidth + 10f, 3.8f), walkway, -0.02f);

        // Ana cadde: kit yol karoları, yoksa asfalt pad + çizgiler
        if (cute && theme.roadStraight != null)
        {
            FillBox(theme.roadStraight, "Ana Cadde",
                MapPos(new Vector3(0f, -0.06f, backZ - 8f)),
                new Vector3(130f, 0.12f, 7.5f),
                keepCollider: false, stretchY: false);
            FillBox(theme.roadStraight, "Yan Cadde (batı)",
                MapPos(new Vector3(-42f, -0.06f, 8f)),
                new Vector3(7.5f, 0.12f, 120f),
                keepCollider: false, stretchY: false);
            FillBox(theme.roadStraight, "Yan Cadde (doğu)",
                MapPos(new Vector3(42f, -0.06f, 8f)),
                new Vector3(7.5f, 0.12f, 120f),
                keepCollider: false, stretchY: false);
        }
        else
        {
            CreatePad("Ana Cadde",
                new Vector3(0f, 0f, backZ - 8f), new Vector2(130f, 7.5f),
                asphalt, -0.025f);
            for (float x = -60f; x <= 60f; x += 6f)
                CreatePad($"Şerit Çizgisi ({x:0})",
                    new Vector3(x, 0f, backZ - 8f), new Vector2(2.2f, 0.28f),
                    lane, -0.015f);
            CreatePad("Yan Cadde (batı)",
                new Vector3(-42f, 0f, 8f), new Vector2(7.5f, 120f), asphalt, -0.025f);
            CreatePad("Yan Cadde (doğu)",
                new Vector3(42f, 0f, 8f), new Vector2(7.5f, 120f), asphalt, -0.025f);
        }

        // Sokak lambaları — kit prefabı varsa o, yoksa prosedürel
        for (float x = -48f; x <= 48f; x += 16f)
        {
            Vector3 pos = new Vector3(x, 0f, backZ - 4.6f);
            if (cute && theme.streetLight != null)
            {
                GameObject lampObj = Instantiate(theme.streetLight, transform);
                lampObj.name = $"Sokak Lambası ({x:0})";
                lampObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                FitToFootprint(lampObj, MapPos(pos), 1.6f, 5.5f);
                AddAreaLight($"Sokak Işığı ({x:0})",
                    pos + Vector3.up * 4.2f, new Color(1f, 0.88f, 0.68f), 9f, 2.1f);
            }
            else BuildStreetLamp(pos);
        }

        // Binalar: karşı sıra + yan parseller — kit binaları, yoksa bloklar
        if (cute)
        {
            float bz = backZ - 19f;
            int   bi = 0;
            for (float x = -40f; x <= 40f; x += 16f, bi++)
                PlaceCityBuilding($"Bina {bi}",
                    theme.cityBuildings[bi % theme.cityBuildings.Length],
                    new Vector3(x + Random.Range(-1.5f, 1.5f), 0f, bz),
                    yaw: 0f);

            // Yan parseller — yüzleri tesise dönük
            PlaceCityBuilding("Bina Yan (doğu)",
                theme.cityBuildings[1 % theme.cityBuildings.Length],
                new Vector3(32f, 0f, 16f), yaw: -90f);
            PlaceCityBuilding("Bina Yan (doğu 2)",
                theme.cityBuildings[3 % theme.cityBuildings.Length],
                new Vector3(32f, 0f, 0f), yaw: -90f);

            // Batı tarafı: mini park (ağaç + çalı + bank)
            BuildMiniPark(new Vector3(-31f, 0f, 6f));
        }
        else
        {
            float bz = backZ - 17f;
            BuildCityBlock("Bina A", new Vector3(-34f, 0f, bz),      new Vector3(14f,  9f, 10f));
            BuildCityBlock("Bina B", new Vector3(-13f, 0f, bz - 2f), new Vector3(12f, 13f, 11f));
            BuildCityBlock("Bina C", new Vector3(  6f, 0f, bz),      new Vector3(10f,  7f,  9f));
            BuildCityBlock("Bina D", new Vector3( 26f, 0f, bz - 1f), new Vector3(15f, 11f, 10f));
            BuildCityBlock("Bina E", new Vector3(-33f, 0f, 20f),     new Vector3(10f,  8f, 12f));
            BuildCityBlock("Bina F", new Vector3( 33f, 0f, 20f),     new Vector3(10f, 10f, 12f));
        }

        if (cute)
        {
            // Park halinde araçlar — kaldırım kenarı
            if (theme.cityCars != null && theme.cityCars.Length > 0)
                for (int i = 0; i < 5; i++)
                {
                    float cx = -28f + i * 13f + Random.Range(-1f, 1f);
                    GameObject car = Instantiate(
                        theme.cityCars[Random.Range(0, theme.cityCars.Length)],
                        transform);
                    car.name = $"Park Araç {i}";
                    car.transform.rotation = Quaternion.Euler(
                        0f, Random.value > 0.5f ? 90f : -90f, 0f);
                    FitToFootprint(car, MapPos(new Vector3(cx, 0f, backZ - 5.4f)),
                        4.6f, 3f);
                }

            // Tesis çevresi ağaç dizisi — yan duvarların dışı
            if (theme.cityTrees != null && theme.cityTrees.Length > 0)
                foreach (int sign in new[] { -1, 1 })
                    for (float z = -6f; z <= 24f; z += 7.5f)
                    {
                        GameObject tree = Instantiate(
                            theme.cityTrees[Random.Range(0, theme.cityTrees.Length)],
                            transform);
                        tree.name = $"Ağaç ({sign * 23:0},{z:0})";
                        tree.transform.rotation =
                            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        FitToFootprint(tree,
                            MapPos(new Vector3(sign * 23.2f, 0f, z + Random.Range(-1f, 1f))),
                            4f, 7f);
                    }

            // Sokak propları — durak, hidrant, tabela, koni
            if (theme.cityProps != null && theme.cityProps.Length >= 5)
            {
                PlaceCityProp(theme.cityProps[0], "Otobüs Durağı",
                    new Vector3(14f, 0f, backZ - 2.6f), 180f, 3.4f, 3f);
                PlaceCityProp(theme.cityProps[1], "Hidrant",
                    new Vector3(-16f, 0f, backZ - 2.2f), 0f, 0.7f, 1.2f);
                PlaceCityProp(theme.cityProps[3], "Tabela",
                    new Vector3(4f, 0f, backZ - 2.2f), 180f, 0.8f, 2.6f);
                PlaceCityProp(theme.cityProps[4], "Koni 1",
                    new Vector3(-6.5f, 0f, backZ - 3.4f), 20f, 0.5f, 0.8f);
                PlaceCityProp(theme.cityProps[4], "Koni 2",
                    new Vector3(-5.4f, 0f, backZ - 4.1f), -35f, 0.5f, 0.8f);
            }
        }

        Random.state = saved;
    }

    /// <summary>Kit binasını parsele oturt — doğal ölçeğine yakın sığdırma.</summary>
    private void PlaceCityBuilding(string buildingName, GameObject prefab,
        Vector3 pos, float yaw)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, transform);
        obj.name = buildingName;
        obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        FitToFootprint(obj, MapPos(pos), 13f, 18f);
    }

    private void PlaceCityProp(GameObject prefab, string propName,
        Vector3 pos, float yaw, float footprint, float maxH)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, transform);
        obj.name = propName;
        obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        FitToFootprint(obj, MapPos(pos), footprint, maxH);
    }

    /// <summary>Mini park: ağaç kümesi + çalılar + bank — batı parseli.</summary>
    private void BuildMiniPark(Vector3 center)
    {
        CreatePad("Park Çimi", center, new Vector2(11f, 13f),
            new Color(0.45f, 0.72f, 0.35f), -0.015f);

        if (theme.cityTrees != null && theme.cityTrees.Length > 0)
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = center + new Vector3(
                    Random.Range(-4f, 4f), 0f, Random.Range(-5f, 5f));
                GameObject tree = Instantiate(
                    theme.cityTrees[Random.Range(0, theme.cityTrees.Length)],
                    transform);
                tree.name = $"Park Ağacı {i}";
                tree.transform.rotation =
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                FitToFootprint(tree, MapPos(p), 4.5f, 8f);
            }

        if (theme.cityBushes != null && theme.cityBushes.Length > 0)
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = center + new Vector3(
                    Random.Range(-4.5f, 4.5f), 0f, Random.Range(-5.5f, 5.5f));
                GameObject bush = Instantiate(
                    theme.cityBushes[Random.Range(0, theme.cityBushes.Length)],
                    transform);
                bush.name = $"Park Çalısı {i}";
                FitToFootprint(bush, MapPos(p), 1.6f, 1.4f);
            }

        if (theme.cityProps != null && theme.cityProps.Length >= 3)
            PlaceCityProp(theme.cityProps[2], "Park Bankı",
                center + new Vector3(0f, 0f, -5.2f), 0f, 1.8f, 1.2f);
    }

    /// <summary>
    /// Global Volume (Bloom + Vignette + renk ayarı) + kamerada
    /// post-processing/SMAA + URP HDR. Profil asset olarak kalıcılaşır
    /// (bellek profili sahne kaydında kopar — GeneratedMaterials dersi).
    /// </summary>
    private void BuildPostProcessing()
    {
        GameObject volObj = new GameObject("Post Processing");
        volObj.transform.SetParent(transform);
        volObj.transform.position = MapPos(Vector3.zero);

        Volume vol = volObj.AddComponent<Volume>();
        vol.isGlobal = true;

        VolumeProfile profile = null;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            const string path = "Assets/PostProfile.asset";
            profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                UnityEditor.AssetDatabase.CreateAsset(profile, path);
            }
        }
#endif
        if (profile == null)
            profile = ScriptableObject.CreateInstance<VolumeProfile>();

        if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>();
        bloom.active = true;
        bloom.intensity.Override(0.85f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.55f);

        if (!profile.TryGet(out Vignette vig)) vig = profile.Add<Vignette>();
        vig.active = true;
        vig.intensity.Override(0.22f);
        vig.smoothness.Override(0.42f);

        if (!profile.TryGet(out ColorAdjustments ca))
            ca = profile.Add<ColorAdjustments>();
        ca.active = true;
        ca.saturation.Override(8f);
        ca.postExposure.Override(0.05f);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(profile);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
        vol.sharedProfile = profile;

        // Kamera: post-processing + SMAA
        Camera cam = Camera.main;
        if (cam != null)
        {
            UniversalAdditionalCameraData data =
                cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing =
                AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        // Bloom'un gerçekten patlaması için HDR şart
        if (GraphicsSettings.defaultRenderPipeline is
                UniversalRenderPipelineAsset urp && !urp.supportsHDR)
        {
            urp.supportsHDR = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(urp);
#endif
        }
    }

    private void BuildStreetLamp(Vector3 pos)
    {
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Sokak Lambası";
        pole.transform.SetParent(transform);
        pole.transform.position   = MapPos(pos + Vector3.up * 1.7f);
        pole.transform.localScale = new Vector3(0.09f, 1.7f, 0.09f);
        if (pole.TryGetComponent<Collider>(out Collider pc)) DestroyImmediate(pc);
        ApplyColor(pole, new Color(0.22f, 0.22f, 0.25f));

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Sokak Lambası (ışık)";
        head.transform.SetParent(transform);
        head.transform.position   = MapPos(pos + Vector3.up * 3.45f);
        head.transform.localScale = new Vector3(0.55f, 0.14f, 0.55f);
        if (head.TryGetComponent<Collider>(out Collider hc)) DestroyImmediate(hc);
        ApplyColor(head, new Color(0.95f, 0.92f, 0.75f));

        // Gerçek ışık — cadde sıcak tonla aydınlanır
        Light lamp = head.AddComponent<Light>();
        lamp.type      = LightType.Point;
        lamp.range     = 9f;
        lamp.intensity = 2.1f;
        lamp.color     = new Color(1f, 0.88f, 0.68f);
    }

    /// <summary>Bölge aydınlatması — soğuk beyaz tepe ışığı.</summary>
    private void AddAreaLight(string lightName, Vector3 pos, Color color,
        float range, float intensity)
    {
        GameObject go = new GameObject(lightName);
        go.transform.SetParent(transform);
        go.transform.position = MapPos(pos);

        Light l = go.AddComponent<Light>();
        l.type      = LightType.Point;
        l.range     = range;
        l.intensity = intensity;
        l.color     = color;
    }

    private void BuildCityBlock(string blockName, Vector3 pos, Vector3 size)
    {
        Vector3 center = MapPos(pos + Vector3.up * size.y / 2f);

        if (HasTheme && theme.wallPanel != null)
        {
            FillBox(theme.wallPanel, blockName, center, size,
                keepCollider: true, stretchY: false, stackY: true);

            // Düz çatı — panel istifinin üstü açık kalmasın
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = $"{blockName} (çatı)";
            roof.transform.SetParent(transform);
            roof.transform.position   = center + Vector3.up * (size.y / 2f - 0.1f);
            roof.transform.localScale = new Vector3(size.x, 0.2f, size.z);
            if (roof.TryGetComponent<Collider>(out Collider rc))
                DestroyImmediate(rc);
            ApplyColor(roof, wallTone);
        }
        else
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(transform);
            block.transform.position   = center;
            block.transform.localScale = size;
            ApplyColor(block, wallTone);
        }

        // Çatı neon hattı — gelişmiş mahalle kimliği
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = $"{blockName} (neon)";
        strip.transform.SetParent(transform);
        strip.transform.position   = center + Vector3.up * (size.y / 2f + 0.06f);
        strip.transform.localScale = new Vector3(size.x * 0.96f, 0.09f, size.z * 0.96f);
        if (strip.TryGetComponent<Collider>(out Collider nc))
            DestroyImmediate(nc);
        ApplyColor(strip, coreAccent);
    }

    /// <summary>
    /// Karo döşemesinin altını dolu güverteyle kapatır. Kit karoları ince
    /// ve TEK YÜZLÜ — altları boş kalırsa kamera yüzeyin altına kayınca
    /// harita "kaybolur" (arka yüz çizilmez). Dolgu, kutunun tabanından
    /// karoların hemen altına kadar çıkar; yandan bakışta koyu istasyon
    /// güvertesi gibi görünür.
    /// </summary>
    private void AddFloorSlab(string floorName, Vector3 boxCenter, Vector3 boxSize)
    {
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = $"{floorName} (dolgu)";
        slab.transform.SetParent(transform);

        // Kutu üstünün 0.25 altında biter — karo kalınlığıyla çakışmaz
        float slabHeight = boxSize.y - 0.25f;
        slab.transform.position = new Vector3(
            boxCenter.x,
            boxCenter.y - boxSize.y / 2f + slabHeight / 2f,
            boxCenter.z);
        slab.transform.localScale = new Vector3(boxSize.x, slabHeight, boxSize.z);

        // Fizik container'ın BoxCollider'ında — dolgu salt görsel
        if (slab.TryGetComponent<Collider>(out Collider col))
            DestroyImmediate(col);

        ApplyColor(slab, wallTone);
    }

    /// <summary>
    /// Sahnedeki kit objesini ayak izine uniform sığdır, tabanını yere otur,
    /// collider'larını kapat (dekor takılma yapmasın). Pivot farkı bounds
    /// merkeziyle düzeltilir.
    /// </summary>
    private static void FitToFootprint(GameObject obj, Vector3 groundPos,
        float footprint, float maxHeight)
    {
        Bounds b     = default;
        bool   first = true;
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else         b.Encapsulate(r.bounds);
        }
        if (first) return;

        float s = Mathf.Min(
            footprint / Mathf.Max(b.size.x, 0.01f),
            footprint / Mathf.Max(b.size.z, 0.01f),
            maxHeight / Mathf.Max(b.size.y, 0.01f));
        obj.transform.localScale *= s;

        // Ölçek sonrası bounds'u yeniden ölç, merkezle ve tabana otur
        b = default; first = true;
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else         b.Encapsulate(r.bounds);
        }
        obj.transform.position += new Vector3(
            groundPos.x - b.center.x,
            groundPos.y - b.min.y,
            groundPos.z - b.center.z);

        foreach (Collider c in obj.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying) Destroy(c);
            else                       DestroyImmediate(c);
        }
    }

    /// <summary>
    /// Atmosfer dekoru: dış duvarların iç yüzü boyunca kit propları
    /// (borular, interkomlar...) dönüşümlü serpilir. Collider'sız —
    /// oynanışa dokunmaz. Yönleri harita içine bakar.
    /// </summary>
    private void BuildWallDecor()
    {
        if (theme.decorProps == null || theme.decorProps.Length == 0) return;

        float backZ    = -garageDepth / 2f;                 // Arka (cadde) duvarı
        float topZ     =  garageDepth / 2f + ScrapDepth;    // Hurdalık üst duvarı
        float mapLeft  = -totalWidth / 2f;
        float mapRight =  totalWidth / 2f;
        int   i = 0;

        for (float x = mapLeft + 4f; x < mapRight - 3f; x += 9f, i++)
        {
            // Arka duvar iç yüzü (+z'ye bakar) / hurdalık üst duvarı iç yüzü
            PlaceDecor(theme.decorProps[i % theme.decorProps.Length],
                new Vector3(x, 0f, backZ + 0.45f), 0f);
            PlaceDecor(theme.decorProps[(i + 1) % theme.decorProps.Length],
                new Vector3(x + 4.5f, 0f, topZ - 0.45f), 180f);
        }
    }

    private void PlaceDecor(GameObject prefab, Vector3 pos, float yaw)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, transform);
        obj.name = $"Dekor - {prefab.name}";
        obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        FitToFootprint(obj, MapPos(pos), 2.2f, 2.6f);
    }

    // ── Zemin, Duvar & Dekor Primitifleri ────────────────────────────────

    private void CreateFloor(string floorName, float centerX, float centerZ,
        float width, float depth, Color color, GameObject tileOverride = null)
    {
        GameObject tile = tileOverride != null ? tileOverride
            : HasTheme ? theme.floorTile : null;

        Vector3 boxCenter = MapPos(new Vector3(centerX, -0.5f, centerZ));
        Vector3 boxSize   = new Vector3(width, 1f, depth);

        if (HasTheme && tile != null)
        {
            // Primitif küple aynı kutu: üst yüz y=0, karolar üstte
            FillBox(tile, floorName, boxCenter, boxSize,
                keepCollider: true, stretchY: false);
            AddFloorSlab(floorName, boxCenter, boxSize);
            return;
        }

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = floorName;
        floor.transform.SetParent(transform);
        floor.transform.position   = boxCenter;
        floor.transform.localScale = boxSize;
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
        if (HasTheme && theme.crate != null)
        {
            Transform box = FillBox(theme.crate, crateName,
                MapPos(new Vector3(pos.x, size / 2f, pos.z)),
                Vector3.one * size, keepCollider: true, stretchY: true);
            box.rotation = Quaternion.Euler(0f, yRot, 0f);
            return;
        }

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
        if (HasTheme && theme.pillar != null)
        {
            FillBox(theme.pillar, pillarName,
                MapPos(new Vector3(pos.x, 1.25f, pos.z)),
                new Vector3(1.2f, 2.5f, 1.2f),
                keepCollider: true, stretchY: true);
            return;
        }

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = pillarName;
        pillar.transform.SetParent(transform);
        pillar.transform.position   = MapPos(new Vector3(pos.x, 1.25f, pos.z));
        pillar.transform.localScale = new Vector3(1.2f, 2.5f, 1.2f);
        ApplyColor(pillar, neutralLight);
    }

    private void CreateWalls()
    {
        float halfW = totalWidth / 2f;
        float backZ = -garageDepth / 2f;                 // Cadde tarafı
        float topZ  =  garageDepth / 2f + ScrapDepth;    // Hurdalık üst sınırı
        float midZ  = (backZ + topZ) / 2f;
        float lenZ  = topZ - backZ;
        float wallY = FactoryWallH / 2f;
        float t     = wallThickness;

        // Dış duvarlar pencereli panel — camlardan mahalle görünür
        GameObject win = HasTheme ? theme.windowPanel : null;

        CreateWall("Duvar - Sol",
            new Vector3(-halfW - t / 2f, wallY, midZ),
            new Vector3(t, FactoryWallH, lenZ + t * 2f),
            new Color(0.20f, 0.70f, 0.95f), win);

        CreateWall("Duvar - Sağ",
            new Vector3(halfW + t / 2f, wallY, midZ),
            new Vector3(t, FactoryWallH, lenZ + t * 2f),
            new Color(0.20f, 0.70f, 0.95f), win);

        CreateWall("Duvar - Arka (cadde)",
            new Vector3(0f, wallY, backZ - t / 2f),
            new Vector3(totalWidth + t * 2f, FactoryWallH, t),
            new Color(0.20f, 0.70f, 0.95f), win);

        CreateWall("Duvar - Ön (hurdalık üstü)",
            new Vector3(0f, wallY, topZ + t / 2f),
            new Vector3(totalWidth + t * 2f, FactoryWallH, t));

        // Garaj üst duvarları — ortada kapı boşluğu (hurdalığa geçiş)
        BuildGarageTopWall(teamACenter, "Mavi",    blueAccent);
        BuildGarageTopWall(teamBCenter, "Kırmızı", redAccent);

        // Alçak ayraç — garajlar birbirini GÖRÜR ama geçemez
        BuildGarageDivider();
        // Tavan YOK — açık atölye (TPS görüşü engelsiz, gökyüzü görünür)
    }

    /// <summary>Garajın hurdalığa bakan duvarı: iki segment + orta kapı.</summary>
    private void BuildGarageTopWall(float centerX, string side, Color accent)
    {
        float z      = garageDepth / 2f;
        float wallY  = FactoryWallH / 2f;
        float segLen = (garageWidth - DoorWidth) / 2f;
        float segOff = DoorWidth / 2f + segLen / 2f;

        CreateWall($"Garaj Üst Duvar [{side}] (sol)",
            new Vector3(centerX - segOff, wallY, z),
            new Vector3(segLen, FactoryWallH, wallThickness), accent);
        CreateWall($"Garaj Üst Duvar [{side}] (sağ)",
            new Vector3(centerX + segOff, wallY, z),
            new Vector3(segLen, FactoryWallH, wallThickness), accent);

        // Kapı ağzı işareti — takım renginde eşik
        CreatePad($"Kapı Eşiği [{side}]",
            new Vector3(centerX, 0f, z), new Vector2(DoorWidth + 0.4f, 1.4f),
            accent, 0.03f);
    }

    /// <summary>İki garaj arasındaki alçak ayraç: görüş açık, geçiş kapalı.</summary>
    private void BuildGarageDivider()
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Garaj Ayracı (alçak)";
        wall.transform.SetParent(transform);
        wall.transform.position   = MapPos(new Vector3(0f, 0.55f, 0f));
        wall.transform.localScale = new Vector3(
            GarageGap * 0.45f, 1.1f, garageDepth);
        ApplyColor(wall, wallTone);   // Collider kalır — üstünden bakışılır

        // Tepe ışık şeridi — iki takımı ayıran neon hat
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "Garaj Ayracı (ışık)";
        strip.transform.SetParent(transform);
        strip.transform.position   = MapPos(new Vector3(0f, 1.13f, 0f));
        strip.transform.localScale = new Vector3(
            GarageGap * 0.4f, 0.07f, garageDepth - 0.2f);
        if (strip.TryGetComponent<Collider>(out Collider sc))
            DestroyImmediate(sc);
        ApplyColor(strip, coreAccent);
    }

    private void CreateWall(string wallName, Vector3 position, Vector3 scale)
        => CreateWall(wallName, position, scale, new Color(0.20f, 0.70f, 0.95f));

    private void CreateWall(string wallName, Vector3 position, Vector3 scale,
        Color lightColor, GameObject panelOverride = null)
    {
        GameObject panel = panelOverride != null ? panelOverride
            : HasTheme ? theme.wallPanel : null;

        if (HasTheme && panel != null)
        {
            // Panel döşemesi + collider container'da; neon şerit aynen kalır.
            // stackY: kısa modüller (çit vb.) y'de istiflenir, duvar boyu
            // paneller tek sıra kalır — esnetme çirkinliği olmaz
            FillBox(panel, wallName, MapPos(position), scale,
                keepCollider: true, stretchY: false, stackY: true);
            CreateWallStrip(wallName, position, scale, lightColor);
            return;
        }

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(transform);
        wall.transform.position   = MapPos(position);
        wall.transform.localScale = scale;
        ApplyColor(wall, wallTone);
        CreateWallStrip(wallName, position, scale, lightColor);
    }

    /// <summary>Neon tepe şeridi — duvar boyunca ışık hattı (fütüristik
    /// kimlik; temalı duvarlarda da takım/bölge rengini taşır).</summary>
    private void CreateWallStrip(string wallName, Vector3 position,
        Vector3 scale, Color lightColor)
    {
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

        // MP Faz 2: her istasyon in-scene NetworkObject — ServerRpc'ler
        // NetworkObjectReference ile adresler (NetworkGameState deseni)
        if (obj.GetComponent<Unity.Netcode.NetworkObject>() == null)
            obj.AddComponent<Unity.Netcode.NetworkObject>();

        // MP Faz 2B: süreli istasyonlarda ilerleme çubuğu senkronu
        // (IProgressReporter olmayanlarda Awake kendini kapatır)
        if (obj.GetComponent<StationProgressSync>() == null)
            obj.AddComponent<StationProgressSync>();

        // Çalışırken kıvılcım (IProgressReporter yoksa kendini kapatır)
        if (obj.GetComponent<StationSparks>() == null)
            obj.AddComponent<StationSparks>();

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
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null && blueChassis.Count > 0)
        {
            UIFactory.SetField(gm, "playerChassis", blueChassis.ToArray());
            Debug.Log($"[MapGenerator] GameManager'a {blueChassis.Count} şasi bağlandı.");
        }

        OfflinePlayerSpawner spawner = FindAnyObjectByType<OfflinePlayerSpawner>();
        if (spawner != null && blueSpawn != null)
        {
            UIFactory.SetField(spawner, "spawnPoint", blueSpawn);
            Debug.Log("[MapGenerator] OfflinePlayerSpawner spawn noktasına bağlandı.");
        }

        RobotChassis[] allChassis =
            FindObjectsByType<RobotChassis>();

        RobotStatusUI statusUI = FindAnyObjectByType<RobotStatusUI>();
        if (statusUI != null)
        {
            UIFactory.SetField(statusUI, "chassisList", allChassis);
            Debug.Log("[MapGenerator] RobotStatusUI şasi listesi güncellendi.");
        }

        ArmorSelectUI armorUI = FindAnyObjectByType<ArmorSelectUI>();
        if (armorUI != null)
        {
            UIFactory.SetField(armorUI, "chassisList", blueChassis.ToArray());
            Debug.Log("[MapGenerator] ArmorSelectUI şasi listesi güncellendi.");
        }
    }

    private void WarnAboutOrphanStations()
    {
        foreach (BaseStation s in FindObjectsByType<BaseStation>())
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
        float tAC = -(garageWidth + GarageGap) / 2f;
        float tBC =  (garageWidth + GarageGap) / 2f;
        float scrapZ = garageDepth / 2f + ScrapDepth / 2f;
        Vector3 c = transform.position;

        Gizmos.color = new Color(0.3f, 0.5f, 1f);
        Gizmos.DrawWireCube(c + new Vector3(tAC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth));

        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(c + new Vector3(0f, 0f, scrapZ),
            new Vector3(garageWidth * 2f + GarageGap, 0.1f, ScrapDepth));

        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        Gizmos.DrawWireCube(c + new Vector3(tBC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth));
    }
}
