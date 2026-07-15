// FirstPersonView.cs — Hurdalık Penceresi FPV kamerası
// Görev: Pencere açıkken oyuncu orta şeride girince kamera karakterin
//   gözüne iner: fare ile bakış (yaw + pitch), gövde yaw'ı takip eder,
//   WASD zaten kameraya göre çalıştığı için hareket doğal FPS hissi verir.
//   Bölgeden çıkınca / pencere kapanınca normal takip kamerasına döner.
// Kurulum: prefab düzenlemesi gerekmez — ScrapWindowZone oyuncuya
//   runtime'da iliştirir (PlayerMelee ile birlikte).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonView : MonoBehaviour
{
    /// <summary>PlayerMelee FPV'de Sol Tık yumruğunu buna bakarak açar.</summary>
    public static bool IsActive { get; private set; }

    // Tek tuş (V) kalıcı FPS modu — hurdalık zorlamasından bağımsız,
    // oyunun her fazında geçerli (lokal oyuncu; statik = tek yerel mod)
    private static bool manualFps;

    [Header("Bakış Ayarları")]
    [SerializeField] private float mouseSensitivity = 0.12f;  // derece / piksel
    [SerializeField] private float eyeHeight  = 1.55f;
    [SerializeField] private float pitchLimit = 60f;

    private PlayerController controller;
    private PlayerInteraction interaction;
    private Camera cam;

    private bool  active;
    private float yaw;
    private float pitch;
    private float blend;               // Giriş yumuşatması (0→1)
    private Vector3    blendFromPos;
    private Quaternion blendFromRot;

    private readonly List<Renderer> hiddenBody = new();

    private void Awake()
    {
        controller  = GetComponent<PlayerController>();
        interaction = GetComponent<PlayerInteraction>();
    }

    private void OnDisable()
    {
        if (active) Exit();
    }

    private void LateUpdate()
    {
        // MP: FPV yalnız kendi oyuncumuzda — uzak kopya kamerayı çalmasın
        if (interaction != null && !interaction.IsLocalPlayer) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // ── Tek tuş FPS/TPS geçişi (V) — oyunun tamamında ───────────────
        // Drone konsolundayken kapalı (kamera drone'da, kontrol kilitli);
        // sersemleme (yalnız controller kapanır) geçişi ENGELLEMEZ.
        bool consoleLocked = interaction != null && !interaction.enabled;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.vKey.wasPressedThisFrame && !consoleLocked)
            manualFps = !manualFps;

        // Hurdalık kapanı FPV'yi eskisi gibi zorlar; V her yerde açar
        ScrapWindowZone zone = ScrapWindowZone.Instance;
        bool zoneForced = zone != null && zone.IsOpen &&
                          zone.IsInside(transform.position);
        bool shouldBeActive = (manualFps || zoneForced) && !consoleLocked;

        if (shouldBeActive && !active)  Enter();
        if (!shouldBeActive && active)  Exit();
        if (!active) return;

        // ── Fare ile bakış ──────────────────────────────────────────────
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw   += delta.x * mouseSensitivity;
            pitch  = Mathf.Clamp(pitch - delta.y * mouseSensitivity,
                                 -pitchLimit, pitchLimit);
        }

        // Gövde bakışı takip eder — yumruk (transform.forward) doğru nişan alır
        controller?.SetLookYaw(yaw);

        // ── Kamerayı göze yerleştir ─────────────────────────────────────
        Quaternion lookRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3    flatFwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3    eyePos  = transform.position +
                             Vector3.up * eyeHeight + flatFwd * 0.12f;

        // Giriş anında eski kameradan göze yumuşak geçiş
        if (blend < 1f)
        {
            blend = Mathf.Min(1f, blend + Time.deltaTime * 3.5f);
            float t = Mathf.SmoothStep(0f, 1f, blend);
            cam.transform.position = Vector3.Lerp(blendFromPos, eyePos, t);
            cam.transform.rotation = Quaternion.Slerp(blendFromRot, lookRot, t);
        }
        else
        {
            cam.transform.position = eyePos;
            cam.transform.rotation = lookRot;
        }
    }

    // ── Mod geçişleri ────────────────────────────────────────────────────

    private void Enter()
    {
        active   = true;
        IsActive = true;

        // Mevcut gövde yönünden başla — savrulma olmasın
        yaw   = transform.eulerAngles.y;
        pitch = 0f;

        blend        = 0f;
        blendFromPos = cam.transform.position;
        blendFromRot = cam.transform.rotation;

        // Takip kamerası sussun — kontrol bizde
        if (CameraController.Instance != null)
            CameraController.Instance.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        controller?.EnableLookOverride(yaw);
        HideBody();
    }

    private void Exit()
    {
        active   = false;
        IsActive = false;

        controller?.DisableLookOverride();
        ShowBody();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Takip kamerası kaldığı hedeften yumuşakça devam eder
        if (CameraController.Instance != null)
            CameraController.Instance.enabled = true;
    }

    // ── Gövde gizleme ────────────────────────────────────────────────────
    // Kendi vücudunu kameranın içinden görmemek için render'lar kapanır.
    // Elindeki malzeme HARİÇ — ne taşıdığını görmek FPV'de geri bildirimdir.

    private void HideBody()
    {
        hiddenBody.Clear();
        Transform held = interaction != null && interaction.HeldObject != null
            ? interaction.HeldObject.transform
            : null;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (held != null && r.transform.IsChildOf(held)) continue;

            r.enabled = false;
            hiddenBody.Add(r);
        }
    }

    private void ShowBody()
    {
        foreach (Renderer r in hiddenBody)
            if (r != null) r.enabled = true;
        hiddenBody.Clear();
    }
}
