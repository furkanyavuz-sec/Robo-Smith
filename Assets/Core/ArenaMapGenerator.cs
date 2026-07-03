// ArenaMapGenerator.cs — v2: SampleScene ile aynı görsel dil
// Görev: Arena sahnesinde harita oluşturur.
// v2 yenilikleri:
//   • Magenta sorunu yok: materyaller StationVisuals'tan (RP uyumlu)
//   • Gri tonlu zemin + iç katman + orta çizgi (tarafsız bölge dili)
//   • Mavi/kırmızı spawn bölgeleri: renkli ped + kenar şeridi
//   • Yeni siper sandıkları NavMeshObstacle (carve) taşır — NavMesh'i
//     yeniden pişirmeden robotlar etrafından dolaşır
// NOT: Zemin boyutu ve sütun konumları v1 ile AYNI bırakıldı —
//      sahnedeki pişmiş NavMesh ve elle bağlanmış spawn noktaları
//      geçerliliğini korur. "Generate Arena" sadece görselleri tazeler.

using UnityEngine;
using UnityEngine.AI;

public class ArenaMapGenerator : MonoBehaviour
{
    [Header("Boyutlar")]
    [SerializeField] private float arenaWidth  = 20f;
    [SerializeField] private float arenaDepth  = 20f;
    [SerializeField] private float wallHeight  = 3f;
    [SerializeField] private float wallThickness = 0.5f;

    [Header("Renk Paleti — Zemin (gri tonlar)")]
    [SerializeField] private Color arenaFloor   = new Color(0.14f, 0.14f, 0.16f);
    [SerializeField] private Color arenaInner   = new Color(0.20f, 0.20f, 0.22f);
    [SerializeField] private Color arenaLine    = new Color(0.32f, 0.32f, 0.35f);
    [SerializeField] private Color arenaWallTone = new Color(0.19f, 0.19f, 0.21f);
    [SerializeField] private Color arenaPillar  = new Color(0.30f, 0.30f, 0.33f);
    [SerializeField] private Color arenaCrate   = new Color(0.26f, 0.26f, 0.28f);

    [Header("Renk Paleti — Takım Bölgeleri")]
    [SerializeField] private Color blueZone   = new Color(0.15f, 0.22f, 0.40f);
    [SerializeField] private Color blueEdge   = new Color(0.25f, 0.50f, 0.95f);
    [SerializeField] private Color redZone    = new Color(0.36f, 0.14f, 0.14f);
    [SerializeField] private Color redEdge    = new Color(0.95f, 0.32f, 0.26f);

    [Header("Engel Ayarları")]
    [SerializeField] private float pillarSize   = 1.5f;
    [SerializeField] private float pillarHeight = 2.5f;

    [ContextMenu("Generate Arena")]
    public void GenerateArena()
    {
        ClearArena();

        CreateFloor();
        CreateWalls();
        CreateObstacles();
        CreateSpawnZones();

        Debug.Log("[ArenaMapGenerator] ✅ Arena görselleri kuruldu. " +
                  "Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    // ── Zemin ─────────────────────────────────────────────────────────

    private void CreateFloor()
    {
        // Taban — v1 ile aynı boyut/konum (NavMesh uyumu)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Arena Zemini";
        floor.transform.SetParent(transform);
        floor.transform.position   = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(arenaWidth, 1f, arenaDepth);
        ApplyColor(floor, arenaFloor);

        // İç katman — bir tık açık gri (SampleScene hurdalık dili)
        CreatePad("Arena İç Zemin", Vector3.zero,
            new Vector2(arenaWidth - 2f, arenaDepth - 2f), arenaInner, 0.012f);

        // Orta çizgi — sahayı ikiye böler
        CreatePad("Orta Çizgi", Vector3.zero,
            new Vector2(arenaWidth - 1f, 0.35f), arenaLine, 0.025f);

        // Orta daire hissi: merkez ped
        CreatePad("Merkez Ped", Vector3.zero,
            new Vector2(3.2f, 3.2f), arenaLine, 0.02f);
    }

    // ── Duvarlar ──────────────────────────────────────────────────────

    private void CreateWalls()
    {
        float halfW = arenaWidth  / 2f;
        float halfD = arenaDepth  / 2f;
        float wallY = wallHeight  / 2f;

        CreateWall("Duvar - Sol",
            new Vector3(-halfW - wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, arenaDepth + wallThickness * 2f));

        CreateWall("Duvar - Sağ",
            new Vector3(halfW + wallThickness / 2f, wallY, 0f),
            new Vector3(wallThickness, wallHeight, arenaDepth + wallThickness * 2f));

        CreateWall("Duvar - Ön",
            new Vector3(0f, wallY, halfD + wallThickness / 2f),
            new Vector3(arenaWidth + wallThickness * 2f, wallHeight, wallThickness));

        CreateWall("Duvar - Arka",
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
        ApplyColor(wall, arenaWallTone);
    }

    // ── Engeller ──────────────────────────────────────────────────────

    private void CreateObstacles()
    {
        // 4 sütun — v1 ile AYNI konumlar (pişmiş NavMesh'te zaten oyulmuş)
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
            pillar.name = $"Sütun {i + 1}";
            pillar.transform.SetParent(transform);
            pillar.transform.position   = pillarPositions[i];
            pillar.transform.localScale = new Vector3(pillarSize, pillarHeight, pillarSize);
            ApplyColor(pillar, arenaPillar);
        }

        // Orta duvar parçaları — v1 ile aynı konumlar
        CreateWall("Orta Duvar - Sol",
            new Vector3(-6f, wallHeight * 0.3f, 0f),
            new Vector3(2f, wallHeight * 0.6f, wallThickness * 2f));

        CreateWall("Orta Duvar - Sağ",
            new Vector3(6f, wallHeight * 0.3f, 0f),
            new Vector3(2f, wallHeight * 0.6f, wallThickness * 2f));

        // YENİ siper sandıkları — NavMesh'te yoklar, bu yüzden carve ederler
        CreateCrate("Sandık 1", new Vector3(-7.2f, 0f,  5.5f), 1.1f,  20f);
        CreateCrate("Sandık 2", new Vector3( 7.2f, 0f,  5.5f), 1.1f, -30f);
        CreateCrate("Sandık 3", new Vector3(-7.2f, 0f, -5.5f), 1.1f, -15f);
        CreateCrate("Sandık 4", new Vector3( 7.2f, 0f, -5.5f), 1.1f,  35f);
    }

    private void CreateCrate(string crateName, Vector3 pos, float size, float yRot)
    {
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = crateName;
        crate.transform.SetParent(transform);
        crate.transform.position   = new Vector3(pos.x, size / 2f, pos.z);
        crate.transform.rotation   = Quaternion.Euler(0f, yRot, 0f);
        crate.transform.localScale = Vector3.one * size;
        ApplyColor(crate, arenaCrate);

        // NavMesh'i yeniden pişirmeden robotlar etrafından dolaşsın
        NavMeshObstacle obstacle = crate.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
    }

    // ── Spawn Bölgeleri ───────────────────────────────────────────────

    private void CreateSpawnZones()
    {
        // Mavi (oyuncu, arka) — bölge pedi + parlak kenar şeridi
        CreatePad("Spawn Bölgesi - Mavi", new Vector3(0f, 0f, -7f),
            new Vector2(8f, 4.5f), blueZone, 0.018f);
        CreatePad("Spawn Şeridi - Mavi", new Vector3(0f, 0f, -4.6f),
            new Vector2(8f, 0.35f), blueEdge, 0.03f);

        // Kırmızı (rakip, ön)
        CreatePad("Spawn Bölgesi - Kırmızı", new Vector3(0f, 0f, 7f),
            new Vector2(8f, 4.5f), redZone, 0.018f);
        CreatePad("Spawn Şeridi - Kırmızı", new Vector3(0f, 0f, 4.6f),
            new Vector2(8f, 0.35f), redEdge, 0.03f);
    }

    // ── Yardımcılar ───────────────────────────────────────────────────

    /// <summary>Zemin üstünde ince renkli plaka — collider'sız dekor.</summary>
    private void CreatePad(string padName, Vector3 pos, Vector2 size,
        Color color, float yOffset)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = padName;
        pad.transform.SetParent(transform);
        pad.transform.position   = new Vector3(pos.x, yOffset, pos.z);
        pad.transform.localScale = new Vector3(size.x, 0.04f, size.y);

        if (pad.TryGetComponent<Collider>(out Collider col))
        {
            if (Application.isPlaying) Destroy(col);
            else                       DestroyImmediate(col);
        }

        ApplyColor(pad, color);
    }

    private void ApplyColor(GameObject obj, Color color)
    {
        if (!obj.TryGetComponent<Renderer>(out Renderer rend)) return;
        rend.sharedMaterial = StationVisuals.GetMaterial(color);
    }

    [ContextMenu("Clear Arena")]
    public void ClearArena()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Debug.Log("[ArenaMapGenerator] Arena temizlendi.");
    }

    // ── Gizmos ───────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero,
            new Vector3(arenaWidth, 1f, arenaDepth));

        Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, -7f), new Vector3(8f, 1f, 4.5f));

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, 7f), new Vector3(8f, 1f, 4.5f));
    }
}
