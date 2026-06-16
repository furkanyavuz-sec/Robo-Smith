// ArenaMapGenerator.cs
// Görev: Arena sahnesinde otomatik harita oluşturur.
// 20x20 alan, ortada engeller, iki tarafta spawn noktaları.

using UnityEngine;

public class ArenaMapGenerator : MonoBehaviour
{
    [Header("Boyutlar")]
    [SerializeField] private float arenaWidth  = 20f;
    [SerializeField] private float arenaDepth  = 20f;
    [SerializeField] private float wallHeight  = 3f;
    [SerializeField] private float wallThickness = 0.5f;

    [Header("Renkler")]
    [SerializeField] private Color floorColor  = new Color(0.2f, 0.2f, 0.2f);
    [SerializeField] private Color wallColor   = new Color(0.15f, 0.15f, 0.15f);
    [SerializeField] private Color pillarColor = new Color(0.3f, 0.3f, 0.35f);
    [SerializeField] private Color teamAColor  = new Color(0.2f, 0.4f, 1f, 0.5f);
    [SerializeField] private Color teamBColor  = new Color(1f, 0.2f, 0.2f, 0.5f);

    [Header("Engel Ayarları")]
    [SerializeField] private float pillarSize   = 1.5f;
    [SerializeField] private float pillarHeight = 2.5f;

    [ContextMenu("Generate Arena")]
    public void GenerateArena()
    {
        ClearArena();

        CreateFloor();
        CreateWalls();
        CreatePillars();
        CreateSpawnZones();

        Debug.Log("[ArenaMapGenerator] Arena olusturuldu!");
    }

    // ── Zemin ─────────────────────────────────────────────────────────

    private void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "ArenaFloor";
        floor.transform.SetParent(transform);
        floor.transform.position   = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(arenaWidth, 1f, arenaDepth);
        ApplyColor(floor, floorColor);
    }

    // ── Duvarlar ──────────────────────────────────────────────────────

    private void CreateWalls()
    {
        float halfW = arenaWidth  / 2f;
        float halfD = arenaDepth  / 2f;
        float wallY = wallHeight  / 2f;

        // Sol duvar
        CreateWall("WallLeft",
            new Vector3(-halfW - wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, arenaDepth + wallThickness * 2f));

        // Sag duvar
        CreateWall("WallRight",
            new Vector3(halfW + wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, arenaDepth + wallThickness * 2f));

        // On duvar
        CreateWall("WallFront",
            new Vector3(0f, wallY, halfD + wallThickness / 2f),
            new Vector3(arenaWidth + wallThickness * 2f, wallHeight, wallThickness));

        // Arka duvar
        CreateWall("WallBack",
            new Vector3(0f, wallY, -halfD - wallThickness / 2f),
            new Vector3(arenaWidth + wallThickness * 2f, wallHeight, wallThickness));
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

    // ── Engeller (Sutunlar) ───────────────────────────────────────────

    private void CreatePillars()
    {
        // Ortada 4 sutun — 2x2 duzeni
        Vector3[] pillarPositions =
        {
            new Vector3(-3f, pillarHeight / 2f - 0.5f,  3f),
            new Vector3( 3f, pillarHeight / 2f - 0.5f,  3f),
            new Vector3(-3f, pillarHeight / 2f - 0.5f, -3f),
            new Vector3( 3f, pillarHeight / 2f - 0.5f, -3f),
        };

        for (int i = 0; i < pillarPositions.Length; i++)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"Pillar_{i}";
            pillar.transform.SetParent(transform);
            pillar.transform.position   = pillarPositions[i];
            pillar.transform.localScale = new Vector3(pillarSize, pillarHeight, pillarSize);
            ApplyColor(pillar, pillarColor);
        }

        // Orta kucuk duvar parcalari — yatay
        CreateWall("CenterWall_Left",
            new Vector3(-6f, wallHeight * 0.3f, 0f),
            new Vector3(2f, wallHeight * 0.6f, wallThickness * 2f));

        CreateWall("CenterWall_Right",
            new Vector3(6f, wallHeight * 0.3f, 0f),
            new Vector3(2f, wallHeight * 0.6f, wallThickness * 2f));
    }

    // ── Spawn Zonlari ─────────────────────────────────────────────────

    private void CreateSpawnZones()
    {
        // Takim A spawn zonu (mavi, arka taraf)
        GameObject zoneA = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoneA.name = "SpawnZone_A";
        zoneA.transform.SetParent(transform);
        zoneA.transform.position   = new Vector3(0f, -0.45f, -7f);
        zoneA.transform.localScale = new Vector3(6f, 0.1f, 4f);
        ApplyColor(zoneA, teamAColor);

        // Takim B spawn zonu (kirmizi, on taraf)
        GameObject zoneB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoneB.name = "SpawnZone_B";
        zoneB.transform.SetParent(transform);
        zoneB.transform.position   = new Vector3(0f, -0.45f, 7f);
        zoneB.transform.localScale = new Vector3(6f, 0.1f, 4f);
        ApplyColor(zoneB, teamBColor);
    }

    [ContextMenu("Clear Arena")]
    public void ClearArena()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Debug.Log("[ArenaMapGenerator] Arena temizlendi.");
    }

    // ── Renk ─────────────────────────────────────────────────────────

    private void ApplyColor(GameObject obj, Color color)
    {
        if (!obj.TryGetComponent<Renderer>(out Renderer rend)) return;
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        rend.material = mat;
    }

    // ── Gizmos ───────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero,
            new Vector3(arenaWidth, 1f, arenaDepth));

        // Spawn zonlari
        Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, -7f), new Vector3(6f, 1f, 4f));

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, 7f), new Vector3(6f, 1f, 4f));
    }
}