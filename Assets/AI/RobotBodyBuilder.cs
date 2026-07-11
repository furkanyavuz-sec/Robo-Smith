// RobotBodyBuilder.cs
// Görev: BattleRobot'a prosedürel gövde inşa eder — küp yerine robot görünsün.
//   • Kafa + takım renginde vizör, omuzlar, sırt çantası, yan plakalar, anten
//   • Takılı HER silah kendi montaj noktasında görünür (renkli prop) —
//     arenada kimin roketli kimin kılıçlı olduğu uzaktan okunur
//   • Silah propları upgrade seviyesiyle büyür (Lv5 kılıç heybetli durur)
//   • Modül varsa göğüste renkli ışık küpü
// Gövde parçaları "tint" listesine eklenir — BattleRobot HP rengini
// (takım rengi → karanlık) tüm parçalara uygular. Vizör/silah/modül
// kendi renklerini korur.

using System.Collections.Generic;
using UnityEngine;

public static class RobotBodyBuilder
{
    public static Color TeamAccent(int teamId) =>
        teamId == 0 ? new Color(0.30f, 0.60f, 1f)
                    : new Color(1f, 0.35f, 0.28f);

    /// <summary>
    /// Gövdeyi kurar; HP rengine boyanacak renderer'ları tintOut'a ekler.
    /// </summary>
    public static void Build(GameObject robot, RobotStatSheet sheet,
        int teamId, List<Renderer> tintOut)
    {
        // Yeniden başlatmada eski gövdeyi temizle
        Transform old = robot.transform.Find("Body");
        if (old != null) Object.Destroy(old.gameObject);

        GameObject body = new GameObject("Body");
        body.transform.SetParent(robot.transform, false);

        Color accent = TeamAccent(teamId);

        // Tema parçaları (SciFi kit): varsa gövde pahlı kit parçalarından
        // kurulur — dokulu, harita temasıyla uyumlu. Yoksa primitif.
        MapTheme th = ThemeRef.Current;

        // ── Gövde parçaları (HP rengine boyanır) ─────────────────────────
        ThemedOr(body, "Kafa", th?.robotCore, PrimitiveType.Cube,
            new Vector3(0f, 0.74f, 0f), new Vector3(0.45f, 0.4f, 0.45f), tintOut);

        ThemedOr(body, "Omuz_Sag", th?.robotJoint, PrimitiveType.Cube,
            new Vector3(0.62f, 0.32f, 0f), new Vector3(0.3f, 0.3f, 0.3f), tintOut);

        ThemedOr(body, "Omuz_Sol", th?.robotJoint, PrimitiveType.Cube,
            new Vector3(-0.62f, 0.32f, 0f), new Vector3(0.3f, 0.3f, 0.3f), tintOut);

        ThemedOr(body, "SirtCantasi", th?.robotBackpack, PrimitiveType.Cube,
            new Vector3(0f, 0.12f, -0.56f), new Vector3(0.52f, 0.55f, 0.22f),
            tintOut);

        ThemedOr(body, "YanPlaka_Sag", th?.robotPlate, PrimitiveType.Cube,
            new Vector3(0.54f, -0.08f, 0f), new Vector3(0.07f, 0.55f, 0.75f),
            tintOut);

        ThemedOr(body, "YanPlaka_Sol", th?.robotPlate, PrimitiveType.Cube,
            new Vector3(-0.54f, -0.08f, 0f), new Vector3(0.07f, 0.55f, 0.75f),
            tintOut);

        // ── Sabit renkli detaylar ────────────────────────────────────────
        // Vizör — takım rengi, öne bakar (FaceTarget +z'yi döndürür)
        Tint(Part(body, "Vizor", PrimitiveType.Cube,
            new Vector3(0f, 0.76f, 0.235f), new Vector3(0.34f, 0.1f, 0.04f)), accent);

        // Anten + ucu
        Tint(Part(body, "Anten", PrimitiveType.Cylinder,
            new Vector3(0.17f, 1.06f, -0.12f), new Vector3(0.03f, 0.14f, 0.03f)),
            new Color(0.2f, 0.2f, 0.22f));
        Tint(Part(body, "AntenUcu", PrimitiveType.Sphere,
            new Vector3(0.17f, 1.22f, -0.12f), Vector3.one * 0.09f), accent);

        // ── Silah propları (3 montaj noktası) ────────────────────────────
        Vector3[] mounts =
        {
            new Vector3( 0.62f, 0.58f,  0f),     // Sağ omuz üstü
            new Vector3(-0.62f, 0.58f,  0f),     // Sol omuz üstü
            new Vector3( 0f,    0.72f, -0.48f),  // Sırt üstü
        };

        int mountIndex = 0;
        foreach (WeaponData w in sheet.equippedWeapons)
        {
            if (w == null || mountIndex >= mounts.Length) continue;
            BuildWeaponProp(body, w, mounts[mountIndex]);
            mountIndex++;
        }

        // ── Modül ışığı — göğüste ────────────────────────────────────────
        if (sheet.equippedModule != ModuleType.None)
        {
            Color moduleColor = StationVisuals.ItemColor(
                ModuleCatalog.ToItem(sheet.equippedModule));
            Tint(PartRotated(body, "ModulIsigi", PrimitiveType.Cube,
                new Vector3(0f, 0.18f, 0.53f), Vector3.one * 0.16f,
                new Vector3(0f, 0f, 45f)), moduleColor);
        }
    }

    // ── Silah Propları ───────────────────────────────────────────────────

    private static void BuildWeaponProp(GameObject body, WeaponData weapon, Vector3 mount)
    {
        Color color = StationVisuals.ItemColor(weapon.sourceItem);

        // Upgrade seviyesi propu büyütür: Lv5 %40 daha heybetli
        float scale = 1f + weapon.upgradeLevel * 0.08f;

        switch (weapon.category)
        {
            case WeaponCategory.Melee:      // Kılıç: dik bıçak + balçak
                Tint(Part(body, "Prop_Kilic", PrimitiveType.Cube,
                    mount + new Vector3(0f, 0.34f * scale, 0f),
                    new Vector3(0.07f, 0.68f, 0.16f) * scale), color);
                Tint(Part(body, "Prop_KilicBalcak", PrimitiveType.Cube,
                    mount + new Vector3(0f, 0.05f, 0f),
                    new Vector3(0.2f, 0.06f, 0.22f) * scale), color * 0.7f);
                break;

            case WeaponCategory.Ranged:     // Lazer: öne bakan namlu
                Tint(PartRotated(body, "Prop_Lazer", PrimitiveType.Cylinder,
                    mount + new Vector3(0f, 0.1f, 0.15f),
                    new Vector3(0.08f, 0.28f, 0.08f) * scale,
                    new Vector3(90f, 0f, 0f)), color);
                break;

            case WeaponCategory.AOE:        // Roket: pod + iki tüp
                Tint(Part(body, "Prop_RoketPod", PrimitiveType.Cube,
                    mount + new Vector3(0f, 0.12f, 0f),
                    new Vector3(0.26f, 0.2f, 0.38f) * scale), color * 0.75f);
                Tint(PartRotated(body, "Prop_RoketTup1", PrimitiveType.Cylinder,
                    mount + new Vector3(0.07f, 0.12f, 0.2f) * scale,
                    new Vector3(0.07f, 0.1f, 0.07f) * scale,
                    new Vector3(90f, 0f, 0f)), color);
                Tint(PartRotated(body, "Prop_RoketTup2", PrimitiveType.Cylinder,
                    mount + new Vector3(-0.07f, 0.12f, 0.2f) * scale,
                    new Vector3(0.07f, 0.1f, 0.07f) * scale,
                    new Vector3(90f, 0f, 0f)), color);
                break;

            case WeaponCategory.Defensive:  // Kalkan: yan plaka
                Tint(Part(body, "Prop_Kalkan", PrimitiveType.Cube,
                    mount + new Vector3(0f, 0.15f, 0f),
                    new Vector3(0.4f, 0.5f, 0.07f) * scale), color);
                break;

            case WeaponCategory.Debuff:     // EMP: sap + küre
                Tint(Part(body, "Prop_EMPSap", PrimitiveType.Cylinder,
                    mount + new Vector3(0f, 0.12f, 0f),
                    new Vector3(0.04f, 0.12f, 0.04f)), new Color(0.2f, 0.2f, 0.22f));
                Tint(Part(body, "Prop_EMPKure", PrimitiveType.Sphere,
                    mount + new Vector3(0f, 0.3f, 0f),
                    Vector3.one * 0.2f * scale), color);
                break;
        }
    }

    // ── Yapı Taşları ─────────────────────────────────────────────────────

    /// <summary>
    /// Tema parçası varsa kit prefabını hedef kutuya sığdırıp kullanır,
    /// yoksa primitife düşer. Dönen renderer HP tint listesine girer
    /// (MaterialPropertyBlock kit dokusunun üstüne renk bindirir).
    /// </summary>
    private static void ThemedOr(GameObject parent, string name,
        GameObject prefab, PrimitiveType fallback, Vector3 localPos,
        Vector3 size, List<Renderer> tintOut)
    {
        if (prefab == null)
        {
            tintOut.Add(Part(parent, name, fallback, localPos, size));
            return;
        }

        GameObject obj = Object.Instantiate(prefab, parent.transform);
        obj.name = name;
        obj.transform.localRotation = Quaternion.identity;

        foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            Object.Destroy(c);

        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            Object.Destroy(obj);
            tintOut.Add(Part(parent, name, fallback, localPos, size));
            return;
        }

        // Bounds'u hedef kutuya eksen bazında ölçekle, merkezi localPos'a getir
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        obj.transform.localScale = Vector3.Scale(obj.transform.localScale,
            new Vector3(
                size.x / Mathf.Max(b.size.x, 0.01f),
                size.y / Mathf.Max(b.size.y, 0.01f),
                size.z / Mathf.Max(b.size.z, 0.01f)));

        obj.transform.localPosition = localPos;
        b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        obj.transform.position +=
            parent.transform.TransformPoint(localPos) - b.center;

        // Tüm parçalar HP rengine boyanır (çok renderer'lı prefablar dahil)
        tintOut.AddRange(rends);
    }

    private static Renderer Part(GameObject parent, string name, PrimitiveType type,
        Vector3 localPos, Vector3 scale) =>
        PartRotated(parent, name, type, localPos, scale, Vector3.zero);

    private static Renderer PartRotated(GameObject parent, string name,
        PrimitiveType type, Vector3 localPos, Vector3 scale, Vector3 euler)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale    = scale;
        obj.transform.localRotation = Quaternion.Euler(euler);

        // Dekor — fizik/NavMesh'e karışmasın
        if (obj.TryGetComponent<Collider>(out Collider col))
            Object.Destroy(col);

        Renderer rend = obj.GetComponent<Renderer>();
        rend.sharedMaterial = StationVisuals.GetMaterial(Color.white);
        return rend;
    }

    /// <summary>Sabit renk: MPB ile boya (paylaşılan materyali kirletme).</summary>
    private static void Tint(Renderer rend, Color color)
    {
        var mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_Color",     color);
        mpb.SetColor("_BaseColor", color);
        rend.SetPropertyBlock(mpb);
    }
}
