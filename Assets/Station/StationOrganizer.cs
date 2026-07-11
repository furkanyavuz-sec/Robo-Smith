// StationOrganizer.cs
// Görev: Sahnedeki tüm istasyonları tek tıkla düzenler:
//   1. Her istasyonu içeriğine göre okunur şekilde adlandırır
//      ("SupplyBin_A" → "Tedarik Kutusu - Demir")
//   2. Eksik garaj tedarik kutularını oluşturur (Demir/Ham Plazma/Devre)
//   3. İşlemci tarif listelerini eşitler (birinde olan tarif hepsine gelir)
//   4. Üretilemeyen silah/malzeme boşluklarını Console'a raporlar
// Kullanım: Station_Organizer objesini seç → bileşen menüsü →
//           "Organize Stations". Sonra sahneyi kaydet.

using System.Collections.Generic;
using UnityEngine;

public class StationOrganizer : MonoBehaviour
{
    [Header("Yeni kutu yerleşimi")]
    [SerializeField] private float newBinSpacing = 2.5f;  // Şablonun yanına aralık

    [ContextMenu("Organize Stations")]
    public void Organize()
    {
        EnsureSupplyBins();       // Önce eksikleri oluştur ki isimlendirme kapsasın
        RenameAllStations();
        SyncProcessorRecipes();
        ReportCoverageGaps();
        MarkSceneDirty();

        Debug.Log("[StationOrganizer] ✅ Tamamlandı — sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    // ── 1. İsimlendirme ──────────────────────────────────────────────────

    private void RenameAllStations()
    {
        var nameCounts = new Dictionary<string, int>();

        foreach (SupplyBin bin in FindObjectsByType<SupplyBin>())
        {
            ItemType type = (ItemType)GetField(bin, "supplyItemType");
            Rename(bin.gameObject, $"Tedarik Kutusu - {TrName(type)}", nameCounts);
        }

        foreach (ScrapyardStation s in FindObjectsByType<ScrapyardStation>())
        {
            ItemType type = (ItemType)GetField(s, "supplyType");
            Rename(s.gameObject, $"Hurdalık - {TrName(type)}", nameCounts);
        }

        foreach (WeaponCraftStation w in FindObjectsByType<WeaponCraftStation>())
        {
            ItemType output = (ItemType)GetField(w, "outputWeaponType");
            ItemType input  = (ItemType)GetField(w, "inputType");
            Rename(w.gameObject, $"Silah Atölyesi - {TrName(output)} ({TrName(input)} ile)", nameCounts);
        }

        foreach (Processor p in FindObjectsByType<Processor>())
            Rename(p.gameObject, "İşleme Masası", nameCounts);

        foreach (AssemblyStation a in FindObjectsByType<AssemblyStation>())
            Rename(a.gameObject, "Montaj İstasyonu", nameCounts);

        foreach (PlasmaSource p in FindObjectsByType<PlasmaSource>())
            Rename(p.gameObject, "Plazma Kaynağı", nameCounts);

        foreach (TrashBin t in FindObjectsByType<TrashBin>())
            Rename(t.gameObject, "Çöp Kutusu", nameCounts);

        foreach (RobotChassis c in FindObjectsByType<RobotChassis>())
            Rename(c.gameObject, "Robot Şasisi", nameCounts);
    }

    private void Rename(GameObject obj, string baseName, Dictionary<string, int> counts)
    {
        counts.TryGetValue(baseName, out int n);
        counts[baseName] = n + 1;

        string finalName = n == 0 ? baseName : $"{baseName} ({n + 1})";
        if (obj.name != finalName)
        {
            Debug.Log($"[StationOrganizer] '{obj.name}' → '{finalName}'");
            obj.name = finalName;
        }
    }

    // ── 2. Eksik Tedarik Kutuları ────────────────────────────────────────

    private static readonly ItemType[] garageMaterials =
        { ItemType.Iron, ItemType.RawPlasma, ItemType.Circuit };

    private void EnsureSupplyBins()
    {
        SupplyBin[] bins = FindObjectsByType<SupplyBin>();
        if (bins.Length == 0)
        {
            Debug.LogError("[StationOrganizer] Sahnede hiç SupplyBin yok — " +
                           "şablon olmadan yenisi oluşturulamaz.");
            return;
        }

        var covered = new HashSet<ItemType>();
        foreach (SupplyBin bin in bins)
            covered.Add((ItemType)GetField(bin, "supplyItemType"));

        // Ham Plazma, Plazma Kaynağı'ndan da gelebilir
        if (FindObjectsByType<PlasmaSource>().Length > 0)
            covered.Add(ItemType.RawPlasma);

        SupplyBin template = bins[0];
        int created = 0;

        foreach (ItemType needed in garageMaterials)
        {
            if (covered.Contains(needed)) continue;

            created++;
            Vector3 pos = template.transform.position
                        + template.transform.right * (newBinSpacing * created);

            GameObject clone = Instantiate(template.gameObject, pos,
                template.transform.rotation, template.transform.parent);
            clone.name = $"Tedarik Kutusu - {TrName(needed)}";

            SupplyBin newBin = clone.GetComponent<SupplyBin>();
            UIFactory.SetField(newBin, "supplyItemType", needed);

            GameObject prefab = FindItemPrefab(needed);
            if (prefab != null)
                UIFactory.SetField(newBin, "itemPrefab", prefab);
            // Prefab bulunamazsa şablonunki kalır — SupplyBin zaten SetType ile
            // tipi ezer, ItemVisual da rengi tipe göre boyar.

            Debug.Log($"<color=cyan>[StationOrganizer] ➕ Yeni kutu: " +
                      $"'{clone.name}' ({pos})</color>");
        }

        if (created == 0)
            Debug.Log("[StationOrganizer] Tüm garaj ham maddelerinin kaynağı mevcut.");
    }

    private GameObject FindItemPrefab(ItemType type)
    {
#if UNITY_EDITOR
        string prefabName = type switch
        {
            ItemType.Iron      => "Iron_Prefab",
            ItemType.RawPlasma => "RawPlasma_Prefab",
            ItemType.Circuit   => "Circuit_Prefab",
            _                  => null
        };
        if (prefabName == null) return null;

        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{prefabName} t:Prefab");
        if (guids.Length == 0) return null;

        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    // ── 3. İşlemci Tariflerini Eşitle ────────────────────────────────────

    private void SyncProcessorRecipes()
    {
        Processor[] processors = FindObjectsByType<Processor>();
        if (processors.Length < 2) return;

        // Tüm işlemcilerdeki tariflerin birleşimi (inputType başına ilk bulunan)
        var union = new Dictionary<ItemType, ProcessorRecipe>();
        foreach (Processor p in processors)
        {
            var recipes = (List<ProcessorRecipe>)GetField(p, "recipes");
            foreach (ProcessorRecipe r in recipes)
                if (r.outputPrefab != null && !union.ContainsKey(r.inputType))
                    union[r.inputType] = r;
        }

        // Eksik tarifi olan işlemcilere kopyala
        foreach (Processor p in processors)
        {
            var recipes = (List<ProcessorRecipe>)GetField(p, "recipes");
            foreach (var kvp in union)
            {
                if (recipes.Exists(r => r.inputType == kvp.Key)) continue;

                recipes.Add(new ProcessorRecipe
                {
                    recipeName         = kvp.Value.recipeName,
                    inputType          = kvp.Value.inputType,
                    outputPrefab       = kvp.Value.outputPrefab,
                    processingDuration = kvp.Value.processingDuration
                });
                Debug.Log($"<color=cyan>[StationOrganizer] ➕ '{p.name}' işlemcisine " +
                          $"'{kvp.Value.recipeName}' tarifi eklendi.</color>");
            }
        }
    }

    // ── 4. Kapsam Raporu ─────────────────────────────────────────────────

    private void ReportCoverageGaps()
    {
        // Hangi silahlar üretilebiliyor?
        var craftable = new HashSet<ItemType>();
        var craftInputs = new HashSet<ItemType>();
        foreach (WeaponCraftStation w in FindObjectsByType<WeaponCraftStation>())
        {
            craftable.Add((ItemType)GetField(w, "outputWeaponType"));
            craftInputs.Add((ItemType)GetField(w, "inputType"));
        }

        ItemType[] allWeapons = { ItemType.Sword, ItemType.Laser, ItemType.Rocket,
                                  ItemType.Shield, ItemType.EMP };
        foreach (ItemType weapon in allWeapons)
            if (!craftable.Contains(weapon))
                Debug.LogWarning($"[StationOrganizer] ⚠️ {TrName(weapon)} üretecek " +
                                 $"Silah Atölyesi yok!");

        // Atölye girdilerinin kaynağı var mı?
        var scrapSources = new HashSet<ItemType>();
        foreach (ScrapyardStation s in FindObjectsByType<ScrapyardStation>())
            scrapSources.Add((ItemType)GetField(s, "supplyType"));

        foreach (ItemType input in craftInputs)
            if (input.IsScrapyardMaterial() && !scrapSources.Contains(input))
                Debug.LogWarning($"[StationOrganizer] ⚠️ Atölyeler {TrName(input)} " +
                                 $"istiyor ama onu veren Hurdalık yok!");
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────

    private static string TrName(ItemType type) => type switch
    {
        ItemType.Iron         => "Demir",
        ItemType.RawPlasma    => "Ham Plazma",
        ItemType.Circuit      => "Devre",
        ItemType.SteelPlate   => "Çelik Plaka",
        ItemType.PlasmaCore   => "Plazma Çekirdeği",
        ItemType.Microchip    => "Mikroçip",
        ItemType.ScrapMetal   => "Hurda Metal",
        ItemType.CrystalShard => "Kristal Kıymık",
        ItemType.RocketFuel   => "Roket Yakıtı",
        ItemType.ShieldAlloy  => "Kalkan Alaşımı",
        ItemType.EMPCore      => "EMP Çekirdeği",
        ItemType.Sword        => "Kılıç",
        ItemType.Laser        => "Lazer",
        ItemType.Rocket       => "Roket",
        ItemType.Shield       => "Kalkan",
        ItemType.EMP          => "EMP",
        _                     => type.ToString()
    };

    private static object GetField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field == null)
        {
            Debug.LogError($"[StationOrganizer] '{target.GetType().Name}.{fieldName}' " +
                           $"alanı bulunamadı!");
            return null;
        }
        return field.GetValue(target);
    }

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
