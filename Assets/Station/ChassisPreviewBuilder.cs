// ChassisPreviewBuilder.cs
// Görev: Şasiye parça koydukça üstünde DÖNEN HOLOGRAFİK taslak robot belirir.
// Her yatırım türü bir parça açar — oyuncu emeğini gözle görür:
//   herhangi bir parça → gövde        HP  → kafa + vizör
//   ATK → omuzlar                     DEF → yan zırh plakaları
//   SPD → hover diski                 silahlar → montaj propları (kendi renginde)
//   modül → göğüs ışığı (modül renginde)
// Gövde parçaları hologram camgöbeği; eşyalar gerçek renklerinde (okunabilirlik).
// RobotChassis her değişiklikte Rebuild çağırır.

using UnityEngine;

public static class ChassisPreviewBuilder
{
    private static readonly Color Holo     = new Color(0.40f, 0.85f, 1f);
    private static readonly Color HoloDim  = new Color(0.22f, 0.48f, 0.60f);

    public static void Rebuild(RobotChassis chassis)
    {
        if (chassis == null) return;

        // Eski taslağı temizle
        Transform old = chassis.transform.Find("ChassisPreview");
        if (old != null) Object.Destroy(old.gameObject);

        RobotStatSheet s = chassis.StatSheet;
        bool anyInvestment = s.HP > 0 || s.ATK > 0 || s.SPD > 0 || s.DEF > 0
                          || s.weaponCount > 0
                          || s.equippedModule != ModuleType.None;
        if (!anyInvestment) return;

        // Turntable kök — şasinin üstünde süzülür, yavaşça döner
        GameObject root = new GameObject("ChassisPreview");
        root.transform.SetParent(chassis.transform, false);
        // Karakter boyuyla senkron: ~1.8m taslak (disk ~1.0'da, tepe ~2.8)
        root.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        root.transform.localScale    = Vector3.one * 1.3f;
        root.AddComponent<StationDecorTag>();
        root.AddComponent<BeaconSpin>().SetSpeed(30f);

        // Hologram tabanı — projeksiyon diski
        Part(root, "HoloDisk", PrimitiveType.Cylinder,
            new Vector3(0f, -0.62f, 0f), new Vector3(1.0f, 0.012f, 1.0f), HoloDim);

        // Gövde parçaları: tema varsa kit şekilleri (arena robotuyla aynı
        // silüet), hologram rengine boyanır — taslak/gerçek eşleşir
        MapTheme th = ThemeRef.Current;

        // Gövde — ilk yatırımla belirir
        BodyPart(root, "Govde", th?.robotCore, PrimitiveType.Cube,
            Vector3.zero, new Vector3(0.60f, 0.72f, 0.45f), Holo);

        // HP → kafa + vizör
        if (s.HP > 0)
        {
            BodyPart(root, "Kafa", th?.robotCore, PrimitiveType.Cube,
                new Vector3(0f, 0.56f, 0f), new Vector3(0.32f, 0.28f, 0.32f), Holo);
            Part(root, "Vizor", PrimitiveType.Cube,
                new Vector3(0f, 0.58f, 0.17f), new Vector3(0.24f, 0.07f, 0.03f),
                Color.white);
        }

        // ATK → omuzlar
        if (s.ATK > 0)
        {
            BodyPart(root, "Omuz_Sag", th?.robotJoint, PrimitiveType.Cube,
                new Vector3( 0.44f, 0.26f, 0f), Vector3.one * 0.22f, Holo);
            BodyPart(root, "Omuz_Sol", th?.robotJoint, PrimitiveType.Cube,
                new Vector3(-0.44f, 0.26f, 0f), Vector3.one * 0.22f, Holo);
        }

        // DEF → yan zırh plakaları
        if (s.DEF > 0)
        {
            BodyPart(root, "Plaka_Sag", th?.robotPlate, PrimitiveType.Cube,
                new Vector3( 0.38f, -0.10f, 0f), new Vector3(0.05f, 0.42f, 0.50f), Holo);
            BodyPart(root, "Plaka_Sol", th?.robotPlate, PrimitiveType.Cube,
                new Vector3(-0.38f, -0.10f, 0f), new Vector3(0.05f, 0.42f, 0.50f), Holo);
        }

        // SPD → hover diski
        if (s.SPD > 0)
            Part(root, "HoverDisk", PrimitiveType.Cylinder,
                new Vector3(0f, -0.48f, 0f), new Vector3(0.55f, 0.03f, 0.55f), Holo);

        // Silahlar → montaj propları (gerçek renklerinde)
        Vector3[] mounts =
        {
            new Vector3( 0.44f, 0.46f,  0f),
            new Vector3(-0.44f, 0.46f,  0f),
            new Vector3( 0f,    0.52f, -0.32f),
        };

        int mountIndex = 0;
        foreach (WeaponData w in s.equippedWeapons)
        {
            if (w == null || mountIndex >= mounts.Length) continue;
            BuildWeaponGhost(root, w, mounts[mountIndex]);
            mountIndex++;
        }

        // Modül → göğüs ışığı
        if (s.equippedModule != ModuleType.None)
        {
            Color moduleColor = StationVisuals.ItemColor(
                ModuleCatalog.ToItem(s.equippedModule));
            Part(root, "ModulIsigi", PrimitiveType.Cube,
                new Vector3(0f, 0.05f, 0.26f), Vector3.one * 0.13f, moduleColor);
        }
    }

    // ── Silah taslakları — küçültülmüş, kendi renginde ───────────────────

    private static void BuildWeaponGhost(GameObject root, WeaponData weapon, Vector3 mount)
    {
        Color c = StationVisuals.ItemColor(weapon.sourceItem);
        float scale = 1f + weapon.upgradeLevel * 0.08f;

        switch (weapon.category)
        {
            case WeaponCategory.Melee:
                Part(root, "Taslak_Kilic", PrimitiveType.Cube,
                    mount + new Vector3(0f, 0.24f * scale, 0f),
                    new Vector3(0.05f, 0.48f, 0.11f) * scale, c);
                break;

            case WeaponCategory.Ranged:
                Part(root, "Taslak_Lazer", PrimitiveType.Cylinder,
                    mount + new Vector3(0f, 0.07f, 0.10f),
                    new Vector3(0.06f, 0.20f, 0.06f) * scale, c,
                    new Vector3(90f, 0f, 0f));
                break;

            case WeaponCategory.AOE:
                Part(root, "Taslak_Roket", PrimitiveType.Cube,
                    mount + new Vector3(0f, 0.09f, 0f),
                    new Vector3(0.18f, 0.14f, 0.27f) * scale, c);
                break;

            case WeaponCategory.Defensive:
                Part(root, "Taslak_Kalkan", PrimitiveType.Cube,
                    mount + new Vector3(0f, 0.10f, 0f),
                    new Vector3(0.28f, 0.35f, 0.05f) * scale, c);
                break;

            case WeaponCategory.Debuff:
                Part(root, "Taslak_EMP", PrimitiveType.Sphere,
                    mount + new Vector3(0f, 0.18f, 0f),
                    Vector3.one * 0.15f * scale, c);
                break;
        }
    }

    // ── Yapı Taşları ─────────────────────────────────────────────────────

    /// <summary>
    /// Tema parçası varsa kit şeklini hologram rengiyle kullanır (tüm
    /// materyaller düz holo renge çevrilir), yoksa primitif. Turntable
    /// kökü ölçekli (0.8) — hedef boyut dünya ölçeğine çevrilerek sığdırılır.
    /// </summary>
    private static void BodyPart(GameObject parent, string name,
        GameObject prefab, PrimitiveType fallback, Vector3 localPos,
        Vector3 size, Color color)
    {
        if (prefab == null)
        {
            Part(parent, name, fallback, localPos, size, color);
            return;
        }

        GameObject obj = Object.Instantiate(prefab, parent.transform);
        obj.name = name;
        obj.transform.localRotation = Quaternion.identity;
        obj.AddComponent<StationDecorTag>();

        foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            Object.Destroy(c);

        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            Object.Destroy(obj);
            Part(parent, name, fallback, localPos, size, color);
            return;
        }

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        // Hedefi dünya ölçeğine çevir (kök 0.8 ölçekli)
        Vector3 lossy  = parent.transform.lossyScale;
        Vector3 target = Vector3.Scale(size, lossy);
        obj.transform.localScale = Vector3.Scale(obj.transform.localScale,
            new Vector3(
                target.x / Mathf.Max(b.size.x, 0.01f),
                target.y / Mathf.Max(b.size.y, 0.01f),
                target.z / Mathf.Max(b.size.z, 0.01f)));

        obj.transform.localPosition = localPos;
        b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        obj.transform.position +=
            parent.transform.TransformPoint(localPos) - b.center;

        Material holoMat = StationVisuals.GetMaterial(color);
        foreach (Renderer r in rends) r.sharedMaterial = holoMat;
    }

    private static void Part(GameObject parent, string name, PrimitiveType type,
        Vector3 localPos, Vector3 scale, Color color, Vector3? euler = null)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale    = scale;
        if (euler.HasValue)
            obj.transform.localRotation = Quaternion.Euler(euler.Value);

        if (obj.TryGetComponent<Collider>(out Collider col))
            Object.Destroy(col);

        obj.AddComponent<StationDecorTag>();
        obj.GetComponent<Renderer>().sharedMaterial =
            StationVisuals.GetMaterial(color);
    }
}
