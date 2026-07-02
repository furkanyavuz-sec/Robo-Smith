// MapGenerator.cs — v2: Tam donanımlı üç bölgeli harita
// Görev: [Mavi Garaj] | [Tarafsız Hurdalık] | [Kırmızı Garaj] düzenini
//        TÜM istasyonlarla, doğru YAPILANDIRMAYLA kurar.
// v1'den farkları:
//   • İstasyonlar sadece isimlendirilmez, içerikleri de ayarlanır
//     (v1'de "WeaponCraft_Laser" aslında Kılıç üretiyordu!)
//   • Eksiksiz set: garaj başına Demir+Devre kutusu, 3 şasi;
//     ortada 5 hurdalık, 5 silah atölyesi, plazma kaynağı
//   • GameManager.playerChassis ve OfflinePlayerSpawner.spawnPoint
//     otomatik bağlanır
//   • Harita generator objesinin pozisyonuna göre kurulur — taşınabilir
// Kullanım: MapGenerator objesi → sağ tık → "Generate Map" → sahneyi kaydet.

using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Harita Boyutları")]
    [SerializeField] private float garageWidth    = 20f;
    [SerializeField] private float garageDepth    = 20f;
    [SerializeField] private float scrapyardWidth = 16f;
    [SerializeField] private float wallHeight     = 3f;
    [SerializeField] private float wallThickness  = 0.5f;

    [Header("Renkler")]
    [SerializeField] private Color teamAColor     = new Color(0.16f, 0.22f, 0.38f); // Mavi zemin
    [SerializeField] private Color teamBColor     = new Color(0.38f, 0.16f, 0.16f); // Kırmızı zemin
    [SerializeField] private Color scrapyardColor = new Color(0.28f, 0.26f, 0.20f); // Tarafsız zemin
    [SerializeField] private Color wallColor      = new Color(0.3f, 0.3f, 0.3f);

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

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ClearMap();

        totalWidth  = garageWidth * 2f + scrapyardWidth;
        teamACenter = -(garageWidth + scrapyardWidth) / 2f;
        teamBCenter =  (garageWidth + scrapyardWidth) / 2f;

        // Zeminler
        CreateFloor("Zemin - Mavi Garaj",  teamACenter, garageWidth,    teamAColor);
        CreateFloor("Zemin - Hurdalık",    0f,          scrapyardWidth, scrapyardColor);
        CreateFloor("Zemin - Kırmızı Garaj", teamBCenter, garageWidth,  teamBColor);

        CreateWalls();

        // Garajlar (aynalı) + tarafsız orta bölge
        List<RobotChassis> blueChassis = BuildGarage(teamACenter, -1, "Mavi",
            out Transform blueSpawn);
        BuildGarage(teamBCenter, +1, "Kırmızı", out Transform _);
        BuildScrapyard();

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
        out Transform spawnPoint)
    {
        // sign: -1 = sol (Mavi) → dış kenar solda; +1 = sağ (Kırmızı)
        // Tedarik kutuları dış kenara, şasiler ortaya, geçit tarafına spawn.

        SupplyBin ironBin = Place<SupplyBin>(supplyBinPrefab,
            $"Tedarik Kutusu - Demir [{zone}]",
            new Vector3(centerX + sign * 7f, 0f, -6f));
        Configure(ironBin, "supplyItemType", ItemType.Iron);
        TryAssignPrefab(ironBin, "itemPrefab", "Iron_Prefab");

        SupplyBin circuitBin = Place<SupplyBin>(supplyBinPrefab,
            $"Tedarik Kutusu - Devre [{zone}]",
            new Vector3(centerX + sign * 7f, 0f, -2f));
        Configure(circuitBin, "supplyItemType", ItemType.Circuit);
        TryAssignPrefab(circuitBin, "itemPrefab", "Circuit_Prefab");

        Place<TrashBin>(trashBinPrefab, $"Çöp Kutusu [{zone}]",
            new Vector3(centerX + sign * 7f, 0f, 6f));

        Place<Processor>(processorPrefab, $"İşleme Masası [{zone}]",
            new Vector3(centerX + sign * 3f, 0f, 7f));
        // Tarifler prefabdan gelir (Demir/Plazma/Devre — 3 tarif)

        // 3 şasi — GameManager 3 oyuncu robotu destekliyor
        var chassisList = new List<RobotChassis>();
        for (int i = 0; i < 3; i++)
        {
            RobotChassis chassis = Place<RobotChassis>(chassisPrefab,
                $"Robot Şasisi {i + 1} [{zone}]",
                new Vector3(centerX, 0f, -5f + i * 5f));
            if (chassis != null) chassisList.Add(chassis);
        }

        // Spawn noktası — geçide yakın iç kenar
        GameObject spawn = new GameObject($"PlayerSpawn [{zone}]");
        spawn.transform.SetParent(transform);
        spawn.transform.position = MapPos(new Vector3(centerX - sign * 7f, 0.75f, 0f));
        spawnPoint = spawn.transform;

        return chassisList;
    }

    // ── Tarafsız Orta Bölge (Hurdalık) ───────────────────────────────────

    private void BuildScrapyard()
    {
        // Orta kolon: 5 ham madde hurdalığı (rekabetçi kaynaklar)
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
            ScrapyardStation s = Place<ScrapyardStation>(scrapyardStationPrefab,
                $"Hurdalık - {scraps[i].name}",
                new Vector3(0f, 0f, -8f + i * 4f));
            Configure(s, "supplyType", scraps[i].type);
            TryAssignPrefab(s, "itemPrefab", "ScrapMetal_Prefab");
            // Görsel kimlik ItemVisual'dan gelir — prefab ortak olabilir
        }

        // Sol kolon: 3 silah atölyesi
        BuildWeaponCraft(new Vector3(-5f, 0f, -7f),   ItemType.ScrapMetal,   ItemType.Sword,  "Kılıç");
        BuildWeaponCraft(new Vector3(-5f, 0f, -2.5f), ItemType.CrystalShard, ItemType.Laser,  "Lazer");
        BuildWeaponCraft(new Vector3(-5f, 0f,  2.5f), ItemType.RocketFuel,   ItemType.Rocket, "Roket");

        // Sağ kolon: 2 silah atölyesi + plazma kaynağı
        BuildWeaponCraft(new Vector3(5f, 0f, -7f),   ItemType.ShieldAlloy, ItemType.Shield, "Kalkan");
        BuildWeaponCraft(new Vector3(5f, 0f, -2.5f), ItemType.EMPCore,     ItemType.EMP,    "EMP");

        Place<PlasmaSource>(plasmaSourcePrefab, "Plazma Kaynağı",
            new Vector3(5f, 0f, 3f));

        Place<PlasmaSource>(plasmaSourcePrefab, "Plazma Kaynağı (2)",
            new Vector3(-5f, 0f, 7f));
    }

    private void BuildWeaponCraft(Vector3 pos, ItemType input, ItemType output, string trName)
    {
        WeaponCraftStation w = Place<WeaponCraftStation>(weaponCraftStationPrefab,
            $"Silah Atölyesi - {trName}", pos);
        Configure(w, "inputType",        input);
        Configure(w, "outputWeaponType", output);
        TryAssignPrefab(w, "outputPrefab", "Weapon_Prefab");
    }

    // ── Zemin & Duvarlar ─────────────────────────────────────────────────

    private void CreateFloor(string floorName, float centerX, float width, Color color)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = floorName;
        floor.transform.SetParent(transform);
        floor.transform.position   = MapPos(new Vector3(centerX, -0.5f, 0f));
        floor.transform.localScale = new Vector3(width, 1f, garageDepth);
        ApplyColor(floor, color);
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

        // Garaj-hurdalık ayraçları: ortada geçit kalır (geçit ≈ garageDepth/2)
        CreateDivider("Ayraç - Mavi",    teamACenter + garageWidth / 2f);
        CreateDivider("Ayraç - Kırmızı", teamBCenter - garageWidth / 2f);
    }

    private void CreateDivider(string dividerName, float x)
    {
        float wallY   = wallHeight / 2f;
        float segLen  = garageDepth / 4f;          // Üstte ve altta birer parça
        float segMidZ = garageDepth / 2f - segLen / 2f;

        CreateWall($"{dividerName} (üst)",
            new Vector3(x, wallY, segMidZ),
            new Vector3(wallThickness, wallHeight, segLen));

        CreateWall($"{dividerName} (alt)",
            new Vector3(x, wallY, -segMidZ),
            new Vector3(wallThickness, wallHeight, segLen));
    }

    private void CreateWall(string wallName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(transform);
        wall.transform.position   = MapPos(position);
        wall.transform.localScale = scale;
        ApplyColor(wall, wallColor);
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
        // GameManager: oyuncu şasileri (Mavi takım)
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && blueChassis.Count > 0)
        {
            UIFactory.SetField(gm, "playerChassis", blueChassis.ToArray());
            Debug.Log($"[MapGenerator] GameManager'a {blueChassis.Count} şasi bağlandı.");
        }

        // Offline spawner: Mavi spawn noktası
        OfflinePlayerSpawner spawner = FindFirstObjectByType<OfflinePlayerSpawner>();
        if (spawner != null && blueSpawn != null)
        {
            UIFactory.SetField(spawner, "spawnPoint", blueSpawn);
            Debug.Log("[MapGenerator] OfflinePlayerSpawner spawn noktasına bağlandı.");
        }

        // Hazırlık HUD'ı yeni şasileri tanısın
        RobotChassis[] allChassis =
            FindObjectsByType<RobotChassis>(FindObjectsSortMode.None);

        RobotStatusUI statusUI = FindFirstObjectByType<RobotStatusUI>();
        if (statusUI != null)
        {
            UIFactory.SetField(statusUI, "chassisList", allChassis);
            Debug.Log("[MapGenerator] RobotStatusUI şasi listesi güncellendi.");
        }

        // Zırh seçim UI'ı da yeni şasileri tanısın
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
        if (!obj.TryGetComponent<Renderer>(out Renderer rend)) return;

        // URP projesi — Standard shader magenta olur, önce URP Lit dene
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        rend.material = mat;
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

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(c,
            new Vector3(scrapyardWidth, 0.1f, garageDepth));

        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        Gizmos.DrawWireCube(c + new Vector3(tBC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth));
    }
}
