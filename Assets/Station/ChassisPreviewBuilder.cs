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
        root.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        root.transform.localScale    = Vector3.one * 0.8f;
        root.AddComponent<StationDecorTag>();
        root.AddComponent<BeaconSpin>().SetSpeed(30f);

        // Hologram tabanı — projeksiyon diski
        Part(root, "HoloDisk", PrimitiveType.Cylinder,
            new Vector3(0f, -0.62f, 0f), new Vector3(1.0f, 0.012f, 1.0f), HoloDim);

        // Gövde — ilk yatırımla belirir
        Part(root, "Govde", PrimitiveType.Cube,
            Vector3.zero, new Vector3(0.60f, 0.72f, 0.45f), Holo);

        // HP → kafa + vizör
        if (s.HP > 0)
        {
            Part(root, "Kafa", PrimitiveType.Cube,
                new Vector3(0f, 0.56f, 0f), new Vector3(0.32f, 0.28f, 0.32f), Holo);
            Part(root, "Vizor", PrimitiveType.Cube,
                new Vector3(0f, 0.58f, 0.17f), new Vector3(0.24f, 0.07f, 0.03f),
                Color.white);
        }

        // ATK → omuzlar
        if (s.ATK > 0)
        {
            Part(root, "Omuz_Sag", PrimitiveType.Cube,
                new Vector3( 0.44f, 0.26f, 0f), Vector3.one * 0.22f, Holo);
            Part(root, "Omuz_Sol", PrimitiveType.Cube,
                new Vector3(-0.44f, 0.26f, 0f), Vector3.one * 0.22f, Holo);
        }

        // DEF → yan zırh plakaları
        if (s.DEF > 0)
        {
            Part(root, "Plaka_Sag", PrimitiveType.Cube,
                new Vector3( 0.38f, -0.10f, 0f), new Vector3(0.05f, 0.42f, 0.50f), Holo);
            Part(root, "Plaka_Sol", PrimitiveType.Cube,
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

    // ── Yapı Taşı ────────────────────────────────────────────────────────

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
