// SupplyDrone.cs — Çekirdek bölge tedarik drone'u
// Görev: Konsoldan sürülen (veya AI pilotlu) uçan taşıyıcı.
//   - Sabit irtifada uçar, harita + (pencere açıksa) çekirdek platform
//     sınırları içinde kalır
//   - Serbest ödüllerin üstünden geçince otomatik kapar (tek slot)
//   - Kendi pedine dönünce yükü otomatik teslim eder
//   - Çarpışmada (DroneRaidZone karar verir) yükünü düşürür + sersemler
// Görsel: prefab gerektirmez, gövde + 4 rotor koddan kurulur.

using UnityEngine;
using UnityEngine.InputSystem;

public class SupplyDrone : MonoBehaviour
{
    public enum DroneMode { Docked, Piloted, AI, Returning }

    [Header("Uçuş Ayarları")]
    [SerializeField] private float pilotSpeed = 8.5f;
    [SerializeField] private float flyHeight  = 4.2f;
    [SerializeField] private float turnSpeed  = 540f;

    [Header("Kapma / Teslim")]
    [SerializeField] private float grabRadius    = 1.5f;
    [SerializeField] private float deliverRadius = 2f;

    [Header("Bağlantılar (MapGenerator bağlar)")]
    [SerializeField] private Vector3 homePosition;       // Park pedi (dünya)
    [SerializeField] private Vector4 flightBounds;        // minX, maxX, minZ, maxZ*
    [SerializeField] private bool    isPlayerTeam = true; // Mavi = oyuncu
    // *maxZ pencere kapalıyken DroneRaidZone.MaxAllowedZ ile daraltılır

    // ── Çalışma durumu ───────────────────────────────────────────────────
    private DroneMode mode = DroneMode.Docked;
    private PickupItem carried;
    private DroneConsole console;         // Piloted modda geri bildirmek için
    private float stunTimer;
    private float pilotGrace;             // Konsola giriş E'si çıkışı tetiklemesin
    private float aiSpeed = 4.5f;
    private Vector3 aiTarget;
    private bool aiHasTarget;

    // Görsel parçalar
    private Transform body;
    private Transform[] rotors;
    private Transform attachPoint;
    private float bobPhase;

    public DroneMode Mode      => mode;
    public bool IsFlying       => mode != DroneMode.Docked;
    public bool IsCarrying     => carried != null;
    public bool IsPlayerTeam   => isPlayerTeam;
    public Vector3 HomePosition => homePosition;

    private void Start()
    {
        // Görsel yoksa kur (generator sahnede kurmuş olabilir — iki kez kurma)
        if (transform.Find("Govde") == null)
            BuildVisual();

        body        = transform.Find("Govde");
        attachPoint = transform.Find("Kanca");
        rotors      = new Transform[4];
        for (int i = 0; i < 4; i++)
            rotors[i] = transform.Find($"Rotor{i}");

        transform.position = homePosition + Vector3.up * 0.9f;
    }

    private void Update()
    {
        stunTimer  -= Time.deltaTime;
        pilotGrace -= Time.deltaTime;
        bobPhase   += Time.deltaTime;

        SpinRotors();

        switch (mode)
        {
            case DroneMode.Docked:
                HoverAt(homePosition, dockHeight: 0.9f);
                break;

            case DroneMode.Piloted:
                if (stunTimer <= 0f) HandlePilotInput();
                TryGrab();
                TryDeliver();
                break;

            case DroneMode.AI:
                if (stunTimer <= 0f && aiHasTarget)
                    MoveToward(aiTarget, aiSpeed);
                TryGrab();
                TryDeliver();
                break;

            case DroneMode.Returning:
                MoveToward(homePosition + Vector3.up * flyHeight, aiSpeed + 2f);
                if (FlatDistance(transform.position, homePosition) < 0.4f)
                {
                    TryDeliver();
                    mode = DroneMode.Docked;
                }
                break;
        }
    }

    // ── Pilot kontrolü (oyuncu) ──────────────────────────────────────────

    /// <summary>DroneConsole çağırır: oyuncu kontrolü devralır.</summary>
    public void BeginPiloting(DroneConsole fromConsole)
    {
        console    = fromConsole;
        mode       = DroneMode.Piloted;
        pilotGrace = 0.3f;
    }

    /// <summary>Kontrol biter; drone eve döner (yükü varsa evde teslim eder).</summary>
    public void EndPiloting()
    {
        if (mode == DroneMode.Piloted)
            mode = DroneMode.Returning;
        console = null;
    }

    /// <summary>DroneRaidZone pencere kapatınca çağırır.</summary>
    public void ForceReturnHome()
    {
        if (mode == DroneMode.Docked) return;

        if (mode == DroneMode.Piloted && console != null)
            console.EndPiloting();   // Oyuncuyu serbest bırak (EndPiloting'i çağırır)

        if (mode != DroneMode.Docked)
            mode = DroneMode.Returning;
        aiHasTarget = false;
    }

    private void HandlePilotInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // E veya ESC → konsoldan çık (giriş E'sini yutmak için grace süresi)
        if (pilotGrace <= 0f &&
            (keyboard.eKey.wasPressedThisFrame ||
             keyboard.escapeKey.wasPressedThisFrame))
        {
            console?.EndPiloting();
            return;
        }

        float h = 0f, v = 0f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  h -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)    v += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  v -= 1f;

        Vector2 input = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
        if (input == Vector2.zero) { TiltBody(Vector3.zero); return; }

        // Kameraya göre dünya yönü (PlayerController ile aynı his)
        Camera cam = Camera.main;
        Vector3 fwd   = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 right = cam != null ? cam.transform.right   : Vector3.right;
        fwd.y = 0f; right.y = 0f;
        fwd.Normalize(); right.Normalize();

        Vector3 dir = (fwd * input.y + right * input.x).normalized;
        Vector3 next = transform.position + dir * pilotSpeed * Time.deltaTime;
        transform.position = ClampToBounds(new Vector3(next.x, flyHeight, next.z));

        FaceDirection(dir);
        TiltBody(dir);
    }

    // ── AI sürüşü (DroneAIPilot çağırır) ─────────────────────────────────

    public void BeginAIFlight(float speed)
    {
        aiSpeed = speed;
        mode    = DroneMode.AI;
    }

    public void SetAITarget(Vector3 target)
    {
        aiTarget    = target;
        aiHasTarget = true;
    }

    private void MoveToward(Vector3 target, float speed)
    {
        Vector3 flat = new Vector3(target.x, flyHeight, target.z);
        Vector3 dir  = flat - transform.position;
        dir.y = 0f;

        if (dir.magnitude < 0.05f) { TiltBody(Vector3.zero); return; }
        dir.Normalize();

        Vector3 next = transform.position + dir * speed * Time.deltaTime;
        transform.position = ClampToBounds(new Vector3(next.x, flyHeight, next.z));

        FaceDirection(dir);
        TiltBody(dir);
    }

    // ── Kapma / Teslim / Çalma ───────────────────────────────────────────

    private void TryGrab()
    {
        if (carried != null || DroneRaidZone.Instance == null) return;

        PickupItem item = DroneRaidZone.Instance.TryGrabNearby(
            transform.position, grabRadius);
        if (item == null) return;

        carried = item;

        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
        if (item.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = true;

        item.transform.SetParent(attachPoint != null ? attachPoint : transform);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
    }

    private void TryDeliver()
    {
        if (carried == null) return;
        if (FlatDistance(transform.position, homePosition) > deliverRadius) return;

        PickupItem item = carried;
        carried = null;

        DroneRaidZone.Instance?.ReleaseReward(item);

        if (isPlayerTeam)
        {
            // Pedin üstüne bırak — oyuncu E ile alır
            item.transform.SetParent(null);
            item.transform.position = homePosition + Vector3.up * 0.5f;
            if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
            }
            if (item.TryGetComponent<Collider>(out Collider col))
                col.isTrigger = false;

            RaidAnnouncer.Show($"TESLİMAT: {ItemName(item.Type)} PEDDE!",
                StationVisuals.ItemColor(item.Type), 2.5f);
        }
        else
        {
            // Rakip teslimatı DirectorAI'ye stat olarak işlenir
            FindFirstObjectByType<DirectorAI>()?.ReceiveDroneReward(item.Type);
            RaidAnnouncer.Show($"RAKİP DRONE {ItemName(item.Type)} KAÇIRDI!",
                new Color(0.95f, 0.32f, 0.26f), 2.5f);
            Destroy(item.gameObject);
        }
    }

    /// <summary>
    /// DroneRaidZone çarpışmada çağırır. Yük taşıyorsa düşürür.
    /// Dönüş: yük düştü mü (anons için).
    /// </summary>
    public bool OnRammed(Vector3 knockDir)
    {
        stunTimer = 0.7f;
        transform.position = ClampToBounds(
            transform.position + knockDir * 1.4f + Vector3.up * 0f);

        if (carried == null) return false;

        PickupItem item = carried;
        carried = null;

        item.transform.SetParent(null);
        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
            rb.AddForce(knockDir * 2f + Vector3.up * 1.5f, ForceMode.Impulse);
        }
        if (item.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = false;

        return true;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────

    private Vector3 ClampToBounds(Vector3 pos)
    {
        float maxZ = DroneRaidZone.Instance != null
            ? DroneRaidZone.Instance.MaxAllowedZ
            : flightBounds.w;

        pos.x = Mathf.Clamp(pos.x, flightBounds.x, flightBounds.y);
        pos.z = Mathf.Clamp(pos.z, flightBounds.z, Mathf.Min(flightBounds.w, maxZ));
        return pos;
    }

    private void HoverAt(Vector3 groundPos, float dockHeight)
    {
        Vector3 target = groundPos + Vector3.up *
            (dockHeight + Mathf.Sin(bobPhase * 2f) * 0.08f);
        transform.position = Vector3.Lerp(
            transform.position, target, Time.deltaTime * 3f);
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir == Vector3.zero) return;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    private void TiltBody(Vector3 moveDir)
    {
        if (body == null) return;
        float tilt = moveDir == Vector3.zero ? 0f : 12f;
        body.localRotation = Quaternion.Slerp(body.localRotation,
            Quaternion.Euler(tilt, 0f, 0f), Time.deltaTime * 6f);
    }

    private void SpinRotors()
    {
        if (rotors == null) return;
        float speed = IsFlying ? 1400f : 500f;
        foreach (Transform r in rotors)
            if (r != null) r.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static string ItemName(ItemType t) => t switch
    {
        ItemType.SteelPlate => "ÇELİK PLAKA",
        ItemType.PlasmaCore => "PLAZMA ÇEKİRDEĞİ",
        ItemType.Microchip  => "MİKROÇİP",
        _                   => t.ToString().ToUpperInvariant()
    };

    // ── Prosedürel görsel ────────────────────────────────────────────────

    /// <summary>MapGenerator üretim sırasında da çağırabilir (editor'de görünsün).</summary>
    public void BuildVisual()
    {
        Color accent = isPlayerTeam
            ? new Color(0.25f, 0.50f, 0.95f)    // Mavi takım
            : new Color(0.95f, 0.32f, 0.26f);   // Kırmızı takım
        Color dark = new Color(0.16f, 0.17f, 0.20f);

        // Gövde
        GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bodyObj.name = "Govde";
        PrepPart(bodyObj, transform, Vector3.zero,
            new Vector3(0.65f, 0.28f, 0.65f), dark);

        // Burun ışığı — yön okunur
        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Burun";
        PrepPart(nose, bodyObj.transform, new Vector3(0f, 0f, 0.55f),
            new Vector3(0.30f, 0.55f, 0.35f), accent);

        // 4 kol + rotor
        Vector2[] corners =
            { new(-1, -1), new(1, -1), new(-1, 1), new(1, 1) };
        for (int i = 0; i < 4; i++)
        {
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = $"Kol{i}";
            PrepPart(arm, transform,
                new Vector3(corners[i].x * 0.42f, 0f, corners[i].y * 0.42f),
                new Vector3(0.14f, 0.10f, 0.14f), dark);

            GameObject rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rotor.name = $"Rotor{i}";
            PrepPart(rotor, transform,
                new Vector3(corners[i].x * 0.52f, 0.18f, corners[i].y * 0.52f),
                new Vector3(0.45f, 0.02f, 0.45f), accent);
        }

        // Kanca — taşınan item buraya asılır
        GameObject hook = new GameObject("Kanca");
        hook.transform.SetParent(transform);
        hook.transform.localPosition = new Vector3(0f, -0.55f, 0f);
    }

    private static void PrepPart(GameObject part, Transform parent,
        Vector3 localPos, Vector3 scale, Color color)
    {
        if (part.TryGetComponent<Collider>(out Collider col))
        {
            if (Application.isPlaying) Destroy(col);
            else                       DestroyImmediate(col);
        }
        part.transform.SetParent(parent);
        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale    = scale;
        part.GetComponent<Renderer>().sharedMaterial =
            StationVisuals.GetMaterial(color);
    }
}
