// StationVisuals.cs
// Görev: İstasyonların oyun içinde "ne olduğunun" bir bakışta anlaşılması.
//   • Her istasyona içerik renginde ayırt edici dekor (kasa direkleri,
//     hurda yığını, atölye tabelası, baca...)
//   • Tepede kameraya dönük yüzen isim etiketi (TMP)
//   • Kaynak istasyonlarında dönen renkli "işaret küpü" (beacon)
// MapGenerator, istasyonları yerleştirirken Decorate* metodlarını çağırır.
// Dekorlar StationDecorTag taşır — VisualThemeManager onları boyamaz.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Dekor işareti: VisualThemeManager bu objelerin rengini ezmesin.</summary>
public class StationDecorTag : MonoBehaviour { }

/// <summary>Yüzen etiket — her karede kameraya döner. Sci-fi fontu runtime'da yükler.</summary>
public class StationLabel : MonoBehaviour
{
    private static Camera cam;

    private void Start()
    {
        // Fütüristik font (Audiowide) — runtime'da uygulanır, sahneye gömülmez
        TMP_FontAsset font = DisplayFontApplier.GetFont();
        if (font != null && TryGetComponent<TMP_Text>(out TMP_Text tmp))
            tmp.font = font;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position);
    }
}

/// <summary>Kaynak işaret küpü — yavaşça döner, dikkat çeker.</summary>
public class BeaconSpin : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 60f;

    public void SetSpeed(float dps) => degreesPerSecond = dps;

    private void Update() =>
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
}

public static class StationVisuals
{
    // ── Merkezi içerik paleti ────────────────────────────────────────────
    // VisualThemeManager'daki item renkleriyle aynı dil — kutu rengi,
    // içindeki item'ın rengiyle eşleşir, oyuncu ezberler.
    public static Color ItemColor(ItemType type) => type switch
    {
        // Garaj ham maddeleri
        ItemType.Iron         => new Color(0.55f, 0.55f, 0.60f), // Soğuk gri
        ItemType.RawPlasma    => new Color(0.95f, 0.85f, 0.10f), // Elektrik sarısı
        ItemType.Circuit      => new Color(0.10f, 0.80f, 0.30f), // Devre yeşili
        // İşlenmiş ürünler
        ItemType.SteelPlate   => new Color(0.75f, 0.80f, 0.85f), // Parlak gümüş
        ItemType.PlasmaCore   => new Color(0.60f, 0.10f, 0.95f), // Plazma moru
        ItemType.Microchip    => new Color(0.05f, 0.50f, 0.90f), // Çip mavisi
        // Hurdalık maddeleri ↔ ürettikleri silahla aynı ton
        ItemType.ScrapMetal   => new Color(0.78f, 0.52f, 0.22f), // Bronz
        ItemType.CrystalShard => new Color(0.20f, 0.85f, 0.90f), // Camgöbeği
        ItemType.RocketFuel   => new Color(0.95f, 0.35f, 0.10f), // Alev turuncusu
        ItemType.ShieldAlloy  => new Color(0.55f, 0.70f, 0.95f), // Çelik mavisi
        ItemType.EMPCore      => new Color(0.72f, 0.30f, 0.95f), // EMP moru
        // Silahlar
        ItemType.Sword        => new Color(0.78f, 0.52f, 0.22f),
        ItemType.Laser        => new Color(0.20f, 0.85f, 0.90f),
        ItemType.Rocket       => new Color(0.95f, 0.35f, 0.10f),
        ItemType.Shield       => new Color(0.55f, 0.70f, 0.95f),
        ItemType.EMP          => new Color(0.72f, 0.30f, 0.95f),
        // Modüller
        ItemType.RepairModule      => new Color(0.30f, 0.95f, 0.55f), // Nane yeşili
        ItemType.OverdriveModule   => new Color(0.95f, 0.20f, 0.35f), // Kızıl
        ItemType.TargetingComputer => new Color(0.95f, 0.35f, 0.75f), // Pembe
        _                     => Color.white
    };

    // ── Materyal önbelleği (render pipeline uyumlu) ──────────────────────
    private static readonly Dictionary<Color, Material> matCache = new();

    public static Material GetMaterial(Color color)
    {
        if (matCache.TryGetValue(color, out Material cached) && cached != null)
            return cached;

        RenderPipelineAsset rp = GraphicsSettings.defaultRenderPipeline;
        Shader shader = rp != null ? rp.defaultShader : null;
        if (shader == null) shader = Shader.Find("Standard");

#if UNITY_EDITOR
        // Edit modunda (Generate Map vb.) materyal ASSET olarak kalıcılaşır.
        // Bellek materyali sahneye gömülemez: sahne diskten başka bir editör
        // süreci/build tarafından yüklenince referans kopar ve URP
        // materyalsiz renderer'ı hiç çizmez → "görünmez harita" hatası.
        if (!Application.isPlaying)
        {
            const string dir = "Assets/GeneratedMaterials";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "GeneratedMaterials");

            string path = $"{dir}/mat_{ColorUtility.ToHtmlStringRGBA(color)}.mat";

            Material asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (asset == null)
            {
                asset = new Material(shader) { color = color };
                UnityEditor.AssetDatabase.CreateAsset(asset, path);
            }

            matCache[color] = asset;
            return asset;
        }
#endif

        // Runtime (arena robotları, mermiler, drone görselleri): bellek
        // materyali yeterli — aynı oturumda üretilir ve kullanılır
        Material mat = new Material(shader) { color = color };
        matCache[color] = mat;
        return mat;
    }

    // ── İstasyon Dekorları ───────────────────────────────────────────────

    public static void DecorateSupplyBin(GameObject station, ItemType content, string trName)
    {
        if (station == null) return;
        Color c = ItemColor(content);

        // Neon kasa çerçevesi — içerik renginde
        AddTechFrame(station, c);
        AddUnderGlow(station, c);
        AddBeacon(station, c);
        AddLabel(station, $"Tedarik\n<size=75%>{trName}</size>", c);
    }

    public static void DecorateScrapyard(GameObject station, ItemType content, string trName)
    {
        if (station == null) return;
        Color c = ItemColor(content);

        // Hurda yığını — içerik renginde eğik küpler
        Primitive(station, PrimitiveType.Cube,
            new Vector3(-0.35f, 0.55f,  0.25f), Vector3.one * 0.45f, c,
            new Vector3(15f, 25f, 10f));
        Primitive(station, PrimitiveType.Cube,
            new Vector3( 0.35f, 0.60f, -0.20f), Vector3.one * 0.38f, c,
            new Vector3(-10f, 50f, 15f));
        Primitive(station, PrimitiveType.Cube,
            new Vector3( 0.05f, 0.95f,  0.05f), Vector3.one * 0.30f, c,
            new Vector3(30f, 10f, -20f));

        AddTechFrame(station, c);
        AddUnderGlow(station, c);
        AddBeacon(station, c);
        AddLabel(station, $"Hurdalık\n<size=75%>{trName}</size>", c);
    }

    public static void DecorateWeaponCraft(GameObject station,
        ItemType input, ItemType output, string weaponName, string inputName)
    {
        if (station == null) return;
        Color weaponColor = ItemColor(output);
        Color inputColor  = ItemColor(input);

        // Tabela: silah renginde dikey levha
        Primitive(station, PrimitiveType.Cube,
            new Vector3(0f, 1.35f, -0.55f),
            new Vector3(0.9f, 0.9f, 0.08f), weaponColor);

        // Girdi göstergesi: tabelanın altında küçük küp — hangi maddeyle çalışır
        Primitive(station, PrimitiveType.Cube,
            new Vector3(0f, 0.72f, -0.55f),
            Vector3.one * 0.24f, inputColor, new Vector3(0f, 45f, 0f));

        AddTechFrame(station, weaponColor);
        AddUnderGlow(station, weaponColor);
        AddLabel(station,
            $"{weaponName} Atölyesi\n<size=70%>Girdi: {inputName}</size>",
            weaponColor);
    }

    public static void DecorateProcessor(GameObject station)
    {
        if (station == null) return;
        Color orange = new Color(0.90f, 0.40f, 0.05f);

        // Baca — işleme makinesi kimliği
        Primitive(station, PrimitiveType.Cylinder,
            new Vector3(0.35f, 1.35f, -0.35f),
            new Vector3(0.22f, 0.45f, 0.22f), orange);

        AddTechFrame(station, orange);
        AddUnderGlow(station, orange);
        AddLabel(station,
            "İşleme Masası\n<size=70%>Demir · Plazma · Devre</size>", orange);
    }

    public static void DecorateAssembly(GameObject station)
    {
        if (station == null) return;
        Color purple = new Color(0.55f, 0.25f, 0.95f);

        // Birleşme sembolü: iki küp üst üste, hafif çapraz
        Primitive(station, PrimitiveType.Cube,
            new Vector3(-0.15f, 1.15f, -0.45f),
            Vector3.one * 0.30f, ItemColor(ItemType.SteelPlate),
            new Vector3(0f, 30f, 0f));
        Primitive(station, PrimitiveType.Cube,
            new Vector3(0.15f, 1.40f, -0.45f),
            Vector3.one * 0.30f, ItemColor(ItemType.Microchip),
            new Vector3(0f, -20f, 0f));

        AddTechFrame(station, purple);
        AddUnderGlow(station, purple);
        AddBeacon(station, purple);
        AddLabel(station,
            "Montaj İstasyonu\n<size=70%>2 farklı ürün → Modül</size>", purple);
    }

    public static void DecorateTrashBin(GameObject station)
    {
        if (station == null) return;
        AddTechFrame(station, new Color(0.45f, 0.45f, 0.50f), cornerPosts: false);
        AddUnderGlow(station, new Color(0.35f, 0.35f, 0.38f));
        AddLabel(station, "Çöp Kutusu", new Color(0.55f, 0.55f, 0.55f));
    }

    public static void DecoratePlasmaSource(GameObject station)
    {
        if (station == null) return;
        Color c = ItemColor(ItemType.RawPlasma);

        // Enerji sütunu — parlak sarı kapsül
        Primitive(station, PrimitiveType.Capsule,
            new Vector3(0f, 1.3f, 0f),
            new Vector3(0.35f, 0.6f, 0.35f), c);

        AddTechFrame(station, c, cornerPosts: false);
        AddUnderGlow(station, c);
        AddBeacon(station, c);
        AddLabel(station, "Plazma Kaynağı", c);
    }

    public static void DecorateChassis(GameObject station, string label, Color teamAccent)
    {
        if (station == null) return;
        AddUnderGlow(station, teamAccent);
        AddLabel(station, label, teamAccent, 2.6f);
    }

    // ── Yapı Taşları ─────────────────────────────────────────────────────

    /// <summary>Neon taban halkası — istasyonun altında parlayan ince plaka.</summary>
    /// <summary>
    /// Yağma ışık huzmesi — yerdeki ödülün konumunu uzaktan belli eder.
    /// Item alınınca "Beam" child'ını yok etmek yeterli.
    /// </summary>
    public static void AddLootBeam(GameObject item, Color color)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Beam";
        if (beam.TryGetComponent<Collider>(out Collider col))
        {
            if (Application.isPlaying) Object.Destroy(col);
            else                       Object.DestroyImmediate(col);
        }
        beam.transform.SetParent(item.transform, worldPositionStays: false);
        beam.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        beam.transform.localScale    = new Vector3(0.12f, 2.2f, 0.12f);
        beam.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
    }

    /// <summary>Drone Konsolu — takım renginde çerçeve + anten + etiket.</summary>
    public static void DecorateDroneConsole(GameObject station, Color teamAccent)
    {
        if (station == null) return;

        // Anten — konsol kimliği (drone'la haberleşme hissi)
        Primitive(station, PrimitiveType.Cylinder,
            new Vector3(-0.32f, 1.55f, -0.32f),
            new Vector3(0.05f, 0.55f, 0.05f), teamAccent);
        Primitive(station, PrimitiveType.Sphere,
            new Vector3(-0.32f, 2.15f, -0.32f),
            Vector3.one * 0.16f, Color.Lerp(teamAccent, Color.white, 0.4f));

        // Ekran — hafif eğik holo panel
        Primitive(station, PrimitiveType.Cube,
            new Vector3(0f, 1.25f, 0.10f),
            new Vector3(0.75f, 0.45f, 0.06f), teamAccent * 0.9f,
            new Vector3(-25f, 0f, 0f));

        AddTechFrame(station, teamAccent);
        AddUnderGlow(station, teamAccent);
        AddLabel(station,
            "Drone Konsolu\n<size=70%>Çekirdek Bölge [E]</size>", teamAccent);
    }

    public static void AddUnderGlow(GameObject station, Color color)
    {
        Primitive(station, PrimitiveType.Cube,
            new Vector3(0f, 0.03f, 0f), new Vector3(1.55f, 0.05f, 1.55f),
            Color.Lerp(color, Color.white, 0.15f));
    }

    /// <summary>
    /// Tekno çerçeve: küpün üst kenarlarında neon şeritler (+ isteğe bağlı
    /// köşe direkleri). Koyu gövde + neon çerçeve = fütüristik istasyon.
    /// </summary>
    public static void AddTechFrame(GameObject station, Color color,
        bool cornerPosts = true)
    {
        Color neon = Color.Lerp(color, Color.white, 0.20f);
        const float half = 0.53f;
        const float topY = 1.04f;

        // Üst çerçeve — 4 kenar şeridi
        Primitive(station, PrimitiveType.Cube,
            new Vector3(0f, topY,  half), new Vector3(1.14f, 0.06f, 0.06f), neon);
        Primitive(station, PrimitiveType.Cube,
            new Vector3(0f, topY, -half), new Vector3(1.14f, 0.06f, 0.06f), neon);
        Primitive(station, PrimitiveType.Cube,
            new Vector3( half, topY, 0f), new Vector3(0.06f, 0.06f, 1.14f), neon);
        Primitive(station, PrimitiveType.Cube,
            new Vector3(-half, topY, 0f), new Vector3(0.06f, 0.06f, 1.14f), neon);

        if (!cornerPosts) return;

        // Köşe direkleri — zeminden çerçeveye
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Primitive(station, PrimitiveType.Cube,
                    new Vector3(x * half, topY / 2f, z * half),
                    new Vector3(0.07f, topY, 0.07f), neon * 0.85f);
    }

    /// <summary>Dönen içerik işaret küpü — istasyonun üstünde süzülür.</summary>
    public static void AddBeacon(GameObject station, Color color,
        float height = 1.7f, float size = 0.32f)
    {
        GameObject beacon = Primitive(station, PrimitiveType.Cube,
            new Vector3(0f, height, 0f), Vector3.one * size, color,
            new Vector3(35f, 45f, 0f));
        beacon.name = "Decor_Beacon";
        beacon.AddComponent<BeaconSpin>();
    }

    /// <summary>Kameraya dönük yüzen isim etiketi. Rich text destekler.</summary>
    public static void AddLabel(GameObject station, string text, Color color,
        float height = 2.3f)
    {
        GameObject obj = new GameObject("Decor_Label");
        obj.transform.SetParent(station.transform, false);
        obj.transform.localPosition = new Vector3(0f, height, 0f);
        obj.AddComponent<StationDecorTag>();
        obj.AddComponent<StationLabel>();

        TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
        tmp.text      = text;
        tmp.fontSize  = 2.6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.Lerp(color, Color.white, 0.35f); // Okunur parlaklık
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = tmp.rectTransform;
        rect.sizeDelta = new Vector2(4f, 1.4f);
    }

    private static GameObject Primitive(GameObject parent, PrimitiveType type,
        Vector3 localPos, Vector3 scale, Color color, Vector3? euler = null)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = $"Decor_{type}";
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale    = scale;
        if (euler.HasValue)
            obj.transform.localRotation = Quaternion.Euler(euler.Value);

        // Dekor — etkileşimi ve fiziği bozmasın
        if (obj.TryGetComponent<Collider>(out Collider col))
        {
            if (Application.isPlaying) Object.Destroy(col);
            else                       Object.DestroyImmediate(col);
        }

        obj.AddComponent<StationDecorTag>();

        if (obj.TryGetComponent<Renderer>(out Renderer rend))
            rend.sharedMaterial = GetMaterial(color);

        return obj;
    }
}
