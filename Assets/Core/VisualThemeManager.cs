// VisualThemeManager.cs
// Görev: Start'ta sahneyi tarar, tüm istasyon ve oyunculara
//        MaterialPropertyBlock ile renk atar.
// Performans: new Material() yok → draw call artmaz.
// Kullanım: Sahnede boş bir GameObject'e ekle, Inspector'dan
//           renkleri ayarla, başka bir şey yapma.

using System.Collections.Generic;
using UnityEngine;

public class VisualThemeManager : MonoBehaviour
{
    // ── Renk Paleti (Inspector'dan değiştirilebilir) ─────────────────────

    [Header("İstasyon Renkleri")]
    [SerializeField] private Color supplyBinColor  = new Color(0.18f, 0.72f, 0.32f); // Yeşil
    [SerializeField] private Color trashBinColor   = new Color(0.15f, 0.15f, 0.15f); // Koyu Gri
    [SerializeField] private Color processorColor  = new Color(0.90f, 0.40f, 0.05f); // Turuncu

    [Header("Oyuncu Rengi")]
    [SerializeField] private Color playerColor     = new Color(0.10f, 0.45f, 0.95f); // Mavi

    [Header("Metalik Ayarlar — İstasyonlar")]
    [SerializeField] private float stationMetallic   = 0.3f;
    [SerializeField] private float stationSmoothness = 0.5f;

    // ── Shader property ID'leri (string lookup önleme) ───────────────────
    // Shader.PropertyToID bir kez hesaplanır, Update'de string karşılaştırması olmaz.
    // NOT: Built-in RP "_Color", URP Lit "_BaseColor" kullanır — ikisi de set
    // edilir, hangi pipeline aktifse o okur. (URP'de renkler bu yüzden çalışmıyordu.)
    private static readonly int PropColor      = Shader.PropertyToID("_Color");
    private static readonly int PropBaseColor  = Shader.PropertyToID("_BaseColor");
    private static readonly int PropMetallic   = Shader.PropertyToID("_Metallic");
    private static readonly int PropGlossiness = Shader.PropertyToID("_Glossiness"); // Smoothness
    private static readonly int PropSmoothness = Shader.PropertyToID("_Smoothness"); // URP

    private void Start()
    {
        ApplyStationThemes();
        ApplyPlayerTheme();
        ApplyItemThemes();
    }

    // ── İstasyon Renklendirme ────────────────────────────────────────────

    private void ApplyStationThemes()
    {
        // Sahnedeki tüm BaseStation türevlerini bul
        BaseStation[] stations = FindObjectsByType<BaseStation>();

        Debug.Log($"[VisualTheme] {stations.Length} istasyon bulundu.");

        foreach (BaseStation station in stations)
        {
            Color targetColor = station switch
            {
                // Kaynak istasyonları içerik rengine boyanır —
                // "hangi kutu ne veriyor" uzaktan okunsun
                SupplyBin bin           => StationVisuals.ItemColor(bin.SupplyType),
                ScrapyardStation yard   => StationVisuals.ItemColor(yard.SupplyType),
                WeaponCraftStation w    => StationVisuals.ItemColor(w.OutputWeapon),
                PlasmaSource            => StationVisuals.ItemColor(ItemType.RawPlasma),
                TrashBin              => trashBinColor,
                Processor             => processorColor,
                _                     => Color.white
            };

            // Fütüristik tema: gövde koyu metal, içerik rengi hafif ton verir.
            // Parlak renk kimliği dekorda taşınır (neon çerçeve, taban halkası,
            // işaret küpü, etiket) — StationVisuals kurar.
            Color darkBody = Color.Lerp(
                new Color(0.10f, 0.11f, 0.14f), targetColor, 0.30f);

            ApplyColorToObject(
                station.gameObject,
                darkBody,
                stationMetallic,
                stationSmoothness
            );
        }
    }

    // ── Oyuncu Renklendirme ──────────────────────────────────────────────

    private void ApplyPlayerTheme()
    {
        PlayerInteraction player =
            FindAnyObjectByType<PlayerInteraction>();

        if (player == null)
        {
            Debug.LogWarning("[VisualTheme] PlayerInteraction bulunamadı.");
            return;
        }

        ApplyColorToObject(player.gameObject, playerColor, 0.1f, 0.4f);
    }

    // ── Item Renklendirme ────────────────────────────────────────────────
    // Sahnede baştan var olan item'ları renklendirir.
    // Runtime'da spawn olan item'lar → ItemVisual.cs halleder.

    private void ApplyItemThemes()
    {
        PickupItem[] items = FindObjectsByType<PickupItem>();

        foreach (PickupItem item in items)
            ApplyItemColor(item);
    }

    // ── Merkezi Renk Uygulama ────────────────────────────────────────────

    /// <summary>
    /// Bir GameObject ve tüm child Renderer'larına
    /// MaterialPropertyBlock ile renk + metalik atar.
    /// </summary>
    public void ApplyColorToObject(
        GameObject target,
        Color color,
        float metallic   = 0f,
        float smoothness = 0.5f)
    {
        // Child dahil tüm Renderer'ları al
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[VisualTheme] '{target.name}' üzerinde Renderer yok.");
            return;
        }

        foreach (Renderer rend in renderers)
        {
            // Dekor parçaları (etiket, beacon, tabela) kendi rengini korur
            if (rend.GetComponentInParent<StationDecorTag>() != null) continue;

            MaterialPropertyBlock block = new MaterialPropertyBlock();

            // Mevcut değerleri koru (override etmek istediğimiz dışındakileri bozmaz)
            rend.GetPropertyBlock(block);

            block.SetColor(PropColor,      color);   // Built-in RP
            block.SetColor(PropBaseColor,  color);   // URP Lit
            block.SetFloat(PropMetallic,   metallic);
            block.SetFloat(PropGlossiness, smoothness);
            block.SetFloat(PropSmoothness, smoothness);

            rend.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// ItemType'a göre doğru renk + metalik preset'i uygular.
    /// ItemVisual.cs da bu metodu çağırır.
    /// </summary>
    public void ApplyItemColor(PickupItem item)
    {
        ItemVisualConfig config = GetItemConfig(item.Type);

        ApplyColorToObject(
            item.gameObject,
            config.color,
            config.metallic,
            config.smoothness
        );
    }

    // ── Item Görsel Konfigürasyonu ───────────────────────────────────────

    [System.Serializable]
    public struct ItemVisualConfig
    {
        public Color color;
        public float metallic;
        public float smoothness;
    }

    [Header("Item Renkleri")]
    [SerializeField] private ItemVisualConfig ironConfig = new ItemVisualConfig
    {
        color      = new Color(0.55f, 0.55f, 0.60f), // Soğuk gri
        metallic   = 0.6f,
        smoothness = 0.4f
    };

    [SerializeField] private ItemVisualConfig steelPlateConfig = new ItemVisualConfig
    {
        color      = new Color(0.75f, 0.80f, 0.85f), // Parlak gümüş
        metallic   = 0.8f,
        smoothness = 0.7f
    };

    [SerializeField] private ItemVisualConfig energyConfig = new ItemVisualConfig
    {
        color      = new Color(0.95f, 0.85f, 0.10f), // Elektrik sarısı
        metallic   = 0.0f,
        smoothness = 0.9f
    };

    [SerializeField] private ItemVisualConfig plasmaCoreConfig = new ItemVisualConfig
    {
        color      = new Color(0.60f, 0.10f, 0.95f), // Plazma moru
        metallic   = 0.2f,
        smoothness = 0.9f
    };

    [SerializeField] private ItemVisualConfig circuitConfig = new ItemVisualConfig
    {
        color      = new Color(0.10f, 0.80f, 0.30f), // Devre yeşili
        metallic   = 0.5f,
        smoothness = 0.6f
    };

    [SerializeField] private ItemVisualConfig microchipConfig = new ItemVisualConfig
    {
        color      = new Color(0.05f, 0.50f, 0.90f), // Chip mavisi
        metallic   = 0.7f,
        smoothness = 0.8f
    };

    public ItemVisualConfig GetItemConfig(ItemType type) => type switch
    {
        ItemType.Iron        => ironConfig,
        ItemType.SteelPlate  => steelPlateConfig,
        ItemType.RawPlasma      => energyConfig,
        ItemType.PlasmaCore  => plasmaCoreConfig,
        ItemType.Circuit     => circuitConfig,
        ItemType.Microchip   => microchipConfig,

        // Hurdalık maddeleri ve silahlar: merkezi paletten
        // (kutu rengi = item rengi = silah rengi — StationVisuals.ItemColor)
        _ => new ItemVisualConfig
             {
                 color      = StationVisuals.ItemColor(type),
                 metallic   = 0.4f,
                 smoothness = 0.6f
             }
    };
}