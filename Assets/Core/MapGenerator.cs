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

    // ── Harita/bölge boyutları (KOD İÇİNDE — tek denge noktası) ──────────
    // Inspector'daki eski seri değerler haritayı etkilemesin diye sabit.
    // Hurdalık 16→24: yan bantlar (atölyeler) + kilitlenebilir orta şerit.
    private const float ScrapyardWidth = 24f;
    private const float CoreZoneGap   = 3f;    // Ön duvar → platform boşluğu
    private const float CoreZoneWidth = 18f;
    private const float CoreZoneDepth = 13f;

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

        totalWidth  = garageWidth * 2f + ScrapyardWidth;
        teamACenter = -(garageWidth + ScrapyardWidth) / 2f;
        teamBCenter =  (garageWidth + ScrapyardWidth) / 2f;

        // Zeminler
        CreateFloor("Zemin - Mavi Garaj",    teamACenter, garageWidth,    blueFloor);
        CreateFloor("Zemin - Hurdalık",      0f,          ScrapyardWidth, neutralFloor,
            HasTheme ? theme.scrapFloorTile : null);
        CreateFloor("Zemin - Kırmızı Garaj", teamBCenter, garageWidth,    redFloor);

        CreateWalls();

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
    // ── SciFi tema bağlayıcısı ───────────────────────────────────────────
    // 3D Scifi Kit Starter Kit (Creepy_Cat) prefablarından MapTheme asset'i
    // üretir/günceller ve bu generator'a bağlar. Kit klasörü gitignore'da —
    // kit importlu olmayan makinede alanlar null kalır, harita primitife düşer.

    private const string KitPrefabRoot =
        "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Prefabs/";
    private const string ThemeAssetPath = "Assets/SciFiMapTheme.asset";

    [ContextMenu("Wire SciFi Theme")]
    private void WireSciFiTheme()
    {
        MapTheme t = UnityEditor.AssetDatabase
            .LoadAssetAtPath<MapTheme>(ThemeAssetPath);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<MapTheme>();
            UnityEditor.AssetDatabase.CreateAsset(t, ThemeAssetPath);
        }

        // Zeminler — bölge başına farklı doku (garaj/hurdalık/platform)
        t.floorTile      = LoadKitPrefab("Floors/Floor_Squared_01_6x6");
        t.scrapFloorTile = LoadKitPrefab("Floors/Floor_Squared_02_6x6");
        t.platformFloor  = LoadKitPrefab("Floors/Floor_Pipes_01");
        t.depotBase      = LoadKitPrefab("Floors/Floor_Coin_01_Small");

        // Duvar & bariyer
        t.wallPanel    = LoadKitPrefab("Walls/Wall_Simple_01_Long");
        t.pillar       = LoadKitPrefab("Walls/Column_01_Big");
        t.barrierFence = LoadKitPrefab("Fences/Fence_Long_01");

        // Proplar
        t.crate = LoadKitPrefab("Props/Crate_01");

        // İstasyon kabukları — gövde kit'ten, neon renk dili üstünde kalır
        t.supplyShell    = LoadKitPrefab("Props/Crate_01");
        t.processorShell = LoadKitPrefab("Walls/Wall_Gear_01_Half");
        t.weaponShell    = LoadKitPrefab("Walls/Wall_Gear_02_Half");
        t.assemblyShell  = LoadKitPrefab("Walls/Wall_Table_01");
        t.trashShell     = LoadKitPrefab("Props/Airing_01");
        t.consoleShell   = LoadKitPrefab("Walls/Wall_Console_01_Half");
        t.plasmaShell    = LoadKitPrefab("Stuff/Pipes_01");

        // Atmosfer
        t.decorProps = new[]
        {
            LoadKitPrefab("Stuff/Pipes_02"),
            LoadKitPrefab("Stuff/Intercom_01"),
            LoadKitPrefab("Stuff/Air_Grid_01"),
        };
        t.skybox = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Textures/Skybox/Skybox.mat");
        if (t.skybox == null)
            Debug.LogWarning("[MapGenerator] Kit skybox materyali bulunamadı.");

        UnityEditor.EditorUtility.SetDirty(t);

        theme = t;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();

        Debug.Log("[MapGenerator] ✅ SciFi tema bağlandı (SciFiMapTheme.asset). " +
                  "Şimdi Generate Map çalıştır + Ctrl+S.");
    }

    private static GameObject LoadKitPrefab(string relativePath)
    {
        GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            KitPrefabRoot + relativePath + ".prefab");
        if (p == null)
            Debug.LogWarning("[MapGenerator] Kit prefabı bulunamadı: " +
                             relativePath + " — bu parça primitif kalacak.");
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
    }

    // ── Tarafsız Orta Bölge (Hurdalık — gri tonlar) ──────────────────────

    private void BuildScrapyard()
    {
        float halfD = garageDepth / 2f;

        // İç zemin katmanı — kenarlardan bir tık açık gri
        CreatePad("Hurdalık İç Zemin",
            Vector3.zero, new Vector2(ScrapyardWidth - 2f, garageDepth - 2f),
            neutralMid, 0.015f);

        // Geçitleri birbirine bağlayan yürüyüş yolu
        CreatePad("Yürüyüş Yolu",
            Vector3.zero, new Vector2(ScrapyardWidth - 0.5f, 2.2f),
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

        // Yan bantlar: her takım tarafında TAM atölye seti + plazma kaynağı.
        // Bantlar Hurdalık Penceresi bariyerlerinin (x ±7) DIŞINDA kalır —
        // pencere kapalıyken de silah üretimi ve plazma akışı hiç kilitlenmez.
        foreach (int sign in new[] { -1, +1 })
        {
            float bx = sign * 9f;
            string side = sign < 0 ? "Mavi Taraf" : "Kırmızı Taraf";

            BuildWeaponCraft(new Vector3(bx, 0f, -8.5f), ItemType.ScrapMetal,   ItemType.Sword,  "Kılıç",  "Hurda Metal");
            BuildWeaponCraft(new Vector3(bx, 0f, -5.1f), ItemType.CrystalShard, ItemType.Laser,  "Lazer",  "Kristal Kıymık");
            BuildWeaponCraft(new Vector3(bx, 0f, -1.7f), ItemType.RocketFuel,   ItemType.Rocket, "Roket",  "Roket Yakıtı");
            BuildWeaponCraft(new Vector3(bx, 0f,  1.7f), ItemType.ShieldAlloy,  ItemType.Shield, "Kalkan", "Kalkan Alaşımı");
            BuildWeaponCraft(new Vector3(bx, 0f,  5.1f), ItemType.EMPCore,      ItemType.EMP,    "EMP",    "EMP Çekirdeği");

            PlasmaSource plasma = Place<PlasmaSource>(plasmaSourcePrefab,
                $"Plazma Kaynağı [{side}]", new Vector3(bx, 0f, 8.5f));
            StationVisuals.DecoratePlasmaSource(plasma?.gameObject);
        }

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
    /// Ön duvarın ötesinde uçarak ulaşılan ödül platformu + bariyerler +
    /// DroneRaidZone yöneticisi. Oyuncu yürüyerek giremez (duvar + boşluk).
    /// </summary>
    private void BuildDroneRaidZone()
    {
        float halfD   = garageDepth / 2f;
        float coreZ   = halfD + CoreZoneGap + CoreZoneDepth / 2f;
        Vector3 center = new Vector3(0f, 0f, coreZ);

        // Platform zemini
        if (HasTheme && theme.platformFloor != null)
        {
            FillBox(theme.platformFloor, "Çekirdek Platform Zemini",
                MapPos(new Vector3(0f, -0.5f, coreZ)),
                new Vector3(CoreZoneWidth, 1f, CoreZoneDepth),
                keepCollider: true, stretchY: false);
        }
        else
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Çekirdek Platform Zemini";
            floor.transform.SetParent(transform);
            floor.transform.position   = MapPos(new Vector3(0f, -0.5f, coreZ));
            floor.transform.localScale = new Vector3(CoreZoneWidth, 1f, CoreZoneDepth);
            ApplyColor(floor, neutralFloor);
        }

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
        float halfD = garageDepth / 2f;
        const float zoneHalfW = 7f;

        Transform[] barriers = new Transform[2];
        barriers[0] = CreateBarrier("Hurdalık Bariyeri (Mavi taraf)",
            new Vector3(-zoneHalfW, 0f, 0f),
            new Vector3(0.35f, 3.5f, garageDepth), keepCollider: true);
        barriers[1] = CreateBarrier("Hurdalık Bariyeri (Kırmızı taraf)",
            new Vector3(zoneHalfW, 0f, 0f),
            new Vector3(0.35f, 3.5f, garageDepth), keepCollider: true);

        // Kapı ağızları — pencere kapanınca içeridekiler buraya ışınlanır
        Vector3 blueGate = MapPos(new Vector3(teamACenter + garageWidth / 2f - 2f, 0f, 0f));
        Vector3 redGate  = MapPos(new Vector3(teamBCenter - garageWidth / 2f + 2f, 0f, 0f));

        GameObject zoneObj = new GameObject("Hurdalık Penceresi");
        zoneObj.transform.SetParent(transform);
        zoneObj.transform.position = MapPos(Vector3.zero);

        ScrapWindowZone zone = zoneObj.AddComponent<ScrapWindowZone>();
        Configure(zone, "zoneRect", new Vector4(
            transform.position.x - zoneHalfW, transform.position.x + zoneHalfW,
            transform.position.z - halfD,     transform.position.z + halfD));
        Configure(zone, "blueEvictPoint", blueGate);
        Configure(zone, "redEvictPoint",  redGate);
        Configure(zone, "barriers",       barriers);
        TryAssignPrefab(zone, "lootPrefab", "ScrapMetal_Prefab");

        // Takım depoları — kapan sırasında toplanan malzemenin güvenli yeri
        Transform blueDepot = CreateDepot("Mavi Depo",
            new Vector3(-5.2f, 0f, -6.8f), blueAccent);
        Transform redDepot  = CreateDepot("Kırmızı Depo",
            new Vector3(5.2f, 0f, -6.8f), redAccent);
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
        Vector3 size, bool keepCollider, bool stretchY, bool stackY = false)
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
            piece.transform.rotation = rotate
                ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
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

        float halfD    = garageDepth / 2f;
        float mapLeft  = teamACenter - garageWidth / 2f;
        float mapRight = teamBCenter + garageWidth / 2f;
        int   i = 0;

        for (float x = mapLeft + 4f; x < mapRight - 3f; x += 9f, i++)
        {
            // Arka duvar iç yüzü (+z'ye bakar) / ön duvar iç yüzü (-z'ye bakar)
            PlaceDecor(theme.decorProps[i % theme.decorProps.Length],
                new Vector3(x, 0f, -halfD + 0.45f), 0f);
            PlaceDecor(theme.decorProps[(i + 1) % theme.decorProps.Length],
                new Vector3(x + 4.5f, 0f, halfD - 0.45f), 180f);
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

    private void CreateFloor(string floorName, float centerX, float width,
        Color color, GameObject tileOverride = null)
    {
        GameObject tile = tileOverride != null ? tileOverride
            : HasTheme ? theme.floorTile : null;

        if (HasTheme && tile != null)
        {
            // Primitif küple aynı kutu: üst yüz y=0, altı dolgu
            FillBox(tile, floorName,
                MapPos(new Vector3(centerX, -0.5f, 0f)),
                new Vector3(width, 1f, garageDepth),
                keepCollider: true, stretchY: false);
            return;
        }

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
        if (HasTheme && theme.wallPanel != null)
        {
            // Panel döşemesi + collider container'da; neon şerit aynen kalır
            FillBox(theme.wallPanel, wallName, MapPos(position), scale,
                keepCollider: true, stretchY: true);
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
        float tAC = -(garageWidth + ScrapyardWidth) / 2f;
        float tBC =  (garageWidth + ScrapyardWidth) / 2f;
        Vector3 c = transform.position;

        Gizmos.color = new Color(0.3f, 0.5f, 1f);
        Gizmos.DrawWireCube(c + new Vector3(tAC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth));

        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(c,
            new Vector3(ScrapyardWidth, 0.1f, garageDepth));

        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        Gizmos.DrawWireCube(c + new Vector3(tBC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth));
    }
}
