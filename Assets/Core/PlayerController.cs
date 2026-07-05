// PlayerController.cs  — Unity New Input System uyumlu
// Okuma yöntemi: UnityEngine.InputSystem.Keyboard / Gamepad polling
// NGO: NetworkBehaviour — multiplayer'da sadece owner input okur/hareket eder.
//      Offline modda (NGO dinlemiyorken) normal çalışır, tekli oyun bozulmaz.

using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;   // ← Yeni paket namespace'i

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 6.5f;
    [SerializeField] private float rotationSpeed = 720f;

    private Rigidbody rb;
    private Camera mainCamera;
    private Vector3 moveDirection;

    // FPV bakış kilidi (FirstPersonView yönetir): gövde, hareket yönü yerine
    // fare bakışını (yaw) takip eder — yumruk transform.forward'la nişan alır.
    private bool  lookOverride;
    private float lookYaw;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        mainCamera = Camera.main;
    }

    /// <summary>
    /// Offline modda her zaman true; multiplayer'da sadece objenin sahibi.
    /// NetworkManager yokken NGO API'lerine dokunmaz — tekli oyun güvenli.
    /// </summary>
    private bool HasControl
    {
        get
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;
            return IsSpawned && IsOwner;
        }
    }

    private void Update()
    {
        if (!HasControl) return;
        ReadInput();
    }

    private void FixedUpdate()
    {
        if (!HasControl) return;
        Move();
        Rotate();
    }

    private void ReadInput()
    {
        // MP: oyuncu lobby sahnesinde doğar, oyun sahnesine taşınır —
        // Awake'te cache'lenen menü kamerası geçişte yok olur. Ölü/boş
        // referansı her karede tazele (Unity'de destroyed obje == null).
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;   // Sahnede henüz kamera yok
        }

        var keyboard = Keyboard.current;
        var gamepad  = Gamepad.current;

        // --- KLAVYE ---
        float horizontal = 0f;
        float vertical   = 0f;

        if (keyboard != null)
        {
            // WASD
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  horizontal -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)    vertical   += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  vertical   -= 1f;
        }

        // --- GAMEPAD (varsa klavyenin üstüne ekle, ikisi aynı anda çalışır) ---
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            // Deadzone: küçük titremeleri yut
            if (stick.magnitude > 0.15f)
            {
                horizontal += stick.x;
                vertical   += stick.y;
            }
        }

        // -1 / 0 / 1 aralığına sabitle (köşegen harekette hız artmasın)
        Vector2 rawInput = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);

        // Kameraya göre dünya yönü
        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight   = mainCamera.transform.right;
        camForward.y = 0f;
        camRight.y   = 0f;
        camForward.Normalize();
        camRight.Normalize();

        moveDirection = (camForward * rawInput.y + camRight * rawInput.x).normalized;
    }

    private void Move()
    {
        if (moveDirection == Vector3.zero) return;
        Vector3 targetPosition = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }

    private void Rotate()
    {
        if (lookOverride)
        {
            rb.rotation = Quaternion.RotateTowards(
                rb.rotation, Quaternion.Euler(0f, lookYaw, 0f),
                rotationSpeed * 2f * Time.fixedDeltaTime
            );
            return;
        }

        if (moveDirection == Vector3.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        rb.rotation = Quaternion.RotateTowards(
            rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime
        );
    }

    public bool IsMoving => moveDirection != Vector3.zero;

    // ── FPV bakış kilidi API'si (FirstPersonView çağırır) ────────────────

    public void EnableLookOverride(float yaw) { lookOverride = true; lookYaw = yaw; }
    public void SetLookYaw(float yaw)         { lookYaw = yaw; }
    public void DisableLookOverride()         { lookOverride = false; }
}