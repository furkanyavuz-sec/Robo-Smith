// MapGenerator.cs
// Görev: Sahneyi otomatik oluşturur.
// Inspector'dan "Generate Map" butonuna bas → hazır.
// Dikdörtgen düzen:
// [Takım A Garajı] | [Scrapyard] | [Takım B Garajı]

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapGenerator : MonoBehaviour
{
    [Header("Harita Boyutları")]
    [SerializeField] private float garageWidth    = 20f;
    [SerializeField] private float garageDepth    = 20f;
    [SerializeField] private float scrapyardWidth = 15f;
    [SerializeField] private float wallHeight     = 3f;
    [SerializeField] private float wallThickness  = 0.5f;

    [Header("Renkler")]
    [SerializeField] private Color teamAColor     = new Color(0.2f, 0.4f, 1f);    // Mavi
    [SerializeField] private Color teamBColor     = new Color(1f, 0.2f, 0.2f);    // Kırmızı
    [SerializeField] private Color scrapyardColor = new Color(0.6f, 0.5f, 0.2f); // Sarı
    [SerializeField] private Color wallColor      = new Color(0.3f, 0.3f, 0.3f); // Gri

    [Header("İstasyon Prefabları")]
    [SerializeField] private GameObject supplyBinPrefab;
    [SerializeField] private GameObject processorPrefab;
    [SerializeField] private GameObject trashBinPrefab;
    [SerializeField] private GameObject chassisPrefab;
    [SerializeField] private GameObject scrapyardStationPrefab;
    [SerializeField] private GameObject weaponCraftStationPrefab;
    [SerializeField] private GameObject plasmaSourcePrefab;

    // Hesaplanan pozisyonlar
    private float totalWidth;
    private float teamACenter;
    private float scrapyardCenter;
    private float teamBCenter;

    [ContextMenu("Generate Map")]  // Inspector'da sağ tıkla → Generate Map
    public void GenerateMap()
    {
        // Eski haritayı temizle
        ClearMap();

        // Merkez hesapla
        totalWidth      = garageWidth * 2 + scrapyardWidth;
        teamACenter     = -garageWidth / 2f - scrapyardWidth / 2f;
        scrapyardCenter = 0f;
        teamBCenter     = garageWidth / 2f + scrapyardWidth / 2f;

        // Zeminleri oluştur
        CreateFloor("FloorTeamA",     teamACenter,     garageWidth,    teamAColor);
        CreateFloor("FloorScrapyard", scrapyardCenter, scrapyardWidth, scrapyardColor);
        CreateFloor("FloorTeamB",     teamBCenter,     garageWidth,    teamBColor);

        // Duvarları oluştur
        CreateWalls();

        // İstasyonları yerleştir
        PlaceStations();

        Debug.Log("[MapGenerator] Harita oluşturuldu!");
    }

    // ── Zemin ────────────────────────────────────────────────────────────

    private void CreateFloor(string floorName, float centerX, float width, Color color)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = floorName;
        floor.transform.SetParent(transform);
        floor.transform.position   = new Vector3(centerX, -0.5f, 0f);
        floor.transform.localScale = new Vector3(width, 1f, garageDepth);

        ApplyColor(floor, color);
    }

    // ── Duvarlar ─────────────────────────────────────────────────────────

    private void CreateWalls()
    {
        float mapLeft  = teamACenter - garageWidth / 2f;
        float mapRight = teamBCenter + garageWidth / 2f;
        float mapFront = garageDepth / 2f;
        float mapBack  = -garageDepth / 2f;
        float wallY    = wallHeight / 2f;

        // Dış duvarlar
        CreateWall("WallLeft",
            new Vector3(mapLeft - wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, garageDepth));

        CreateWall("WallRight",
            new Vector3(mapRight + wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, garageDepth));

        CreateWall("WallFront",
            new Vector3(0f, wallY, mapFront + wallThickness / 2f),
            new Vector3(totalWidth + wallThickness * 2f, wallHeight, wallThickness));

        CreateWall("WallBack",
            new Vector3(0f, wallY, mapBack - wallThickness / 2f),
            new Vector3(totalWidth + wallThickness * 2f, wallHeight, wallThickness));

        // Takım A ile Scrapyard arası bölücü duvar (geçit bırak)
        float dividerAX = teamACenter + garageWidth / 2f;
        CreateWall("DividerA_Top",
            new Vector3(dividerAX, wallY, garageDepth / 4f + garageDepth / 8f),
            new Vector3(wallThickness, wallHeight, garageDepth / 4f));

        CreateWall("DividerA_Bottom",
            new Vector3(dividerAX, wallY, -garageDepth / 4f - garageDepth / 8f),
            new Vector3(wallThickness, wallHeight, garageDepth / 4f));

        // Takım B ile Scrapyard arası bölücü duvar (geçit bırak)
        float dividerBX = teamBCenter - garageWidth / 2f;
        CreateWall("DividerB_Top",
            new Vector3(dividerBX, wallY, garageDepth / 4f + garageDepth / 8f),
            new Vector3(wallThickness, wallHeight, garageDepth / 4f));

        CreateWall("DividerB_Bottom",
            new Vector3(dividerBX, wallY, -garageDepth / 4f - garageDepth / 8f),
            new Vector3(wallThickness, wallHeight, garageDepth / 4f));
    }

    private void CreateWall(string wallName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(transform);
        wall.transform.position   = position;
        wall.transform.localScale = scale;
        ApplyColor(wall, wallColor);
    }

    // ── İstasyonlar ──────────────────────────────────────────────────────

    private void PlaceStations()
    {
        // ── Takım A Garajı (sol) ─────────────────────────────────────────
        // SupplyBin — sol arka köşe
        PlaceStation(supplyBinPrefab, "SupplyBin_A",
            new Vector3(teamACenter - 6f, 0f, -6f));

        // Processor — orta sol
        PlaceStation(processorPrefab, "Processor_A",
            new Vector3(teamACenter - 3f, 0f, 0f));

        // TrashBin — sol ön köşe
        PlaceStation(trashBinPrefab, "TrashBin_A",
            new Vector3(teamACenter - 6f, 0f, 6f));

        // RobotChassis — garaj ortası
        PlaceStation(chassisPrefab, "RobotChassis_A",
            new Vector3(teamACenter, 0f, 0f));

        // ── Scrapyard (orta) ─────────────────────────────────────────────
        // ScrapyardStation
        PlaceStation(scrapyardStationPrefab, "ScrapyardStation_ScrapMetal",
            new Vector3(scrapyardCenter, 0f, -4f));

        PlaceStation(scrapyardStationPrefab, "ScrapyardStation_Crystal",
            new Vector3(scrapyardCenter, 0f, 0f));

        PlaceStation(scrapyardStationPrefab, "ScrapyardStation_Rocket",
            new Vector3(scrapyardCenter, 0f, 4f));

        // WeaponCraftStation
        PlaceStation(weaponCraftStationPrefab, "WeaponCraft_Sword",
            new Vector3(scrapyardCenter - 4f, 0f, -4f));

        PlaceStation(weaponCraftStationPrefab, "WeaponCraft_Laser",
            new Vector3(scrapyardCenter - 4f, 0f, 4f));

        // PlasmaSource
        PlaceStation(plasmaSourcePrefab, "PlasmaSource",
            new Vector3(scrapyardCenter + 4f, 0f, 0f));

        // ── Takım B Garajı (sağ) ─────────────────────────────────────────
        PlaceStation(supplyBinPrefab, "SupplyBin_B",
            new Vector3(teamBCenter + 6f, 0f, -6f));

        PlaceStation(processorPrefab, "Processor_B",
            new Vector3(teamBCenter + 3f, 0f, 0f));

        PlaceStation(trashBinPrefab, "TrashBin_B",
            new Vector3(teamBCenter + 6f, 0f, 6f));

        PlaceStation(chassisPrefab, "RobotChassis_B",
            new Vector3(teamBCenter, 0f, 0f));
    }

    private GameObject PlaceStation(GameObject prefab, string stationName, Vector3 position)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[MapGenerator] {stationName} prefabı atanmamış!");
            return null;
        }

        GameObject station = Instantiate(prefab, position, Quaternion.identity);
        station.name = stationName;
        station.transform.SetParent(transform);
        return station;
    }

    // ── Temizlik ─────────────────────────────────────────────────────────

    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        // MapGenerator'ın tüm child objelerini sil
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        Debug.Log("[MapGenerator] Harita temizlendi.");
    }

    // ── Renk Uygulama ────────────────────────────────────────────────────

    private void ApplyColor(GameObject obj, Color color)
    {
        if (!obj.TryGetComponent<Renderer>(out Renderer rend)) return;

        // MaterialPropertyBlock yerine direkt materyal oluştur
        // Play modunda kaybolmaz
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        rend.material = mat;
    }

    // ── Gizmos ──────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        float tW  = garageWidth * 2 + scrapyardWidth;
        float tAC = -garageWidth / 2f - scrapyardWidth / 2f;
        float tBC = garageWidth / 2f + scrapyardWidth / 2f;

        // Takım A
        Gizmos.color = teamAColor;
        Gizmos.DrawWireCube(
            new Vector3(tAC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth)
        );

        // Scrapyard
        Gizmos.color = scrapyardColor;
        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(scrapyardWidth, 0.1f, garageDepth)
        );

        // Takım B
        Gizmos.color = teamBColor;
        Gizmos.DrawWireCube(
            new Vector3(tBC, 0f, 0f),
            new Vector3(garageWidth, 0.1f, garageDepth)
        );
    }
}