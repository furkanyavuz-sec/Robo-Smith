// PlayerController.cs  — Unity New Input System uyumlu
// Okuma yöntemi: UnityEngine.InputSystem.Keyboard / Gamepad polling
// NGO notu: IsOwner kontrolü yorum satırı olarak hazır, NGO'ya geçince
//           MonoBehaviour → NetworkBehaviour yapılıp açılacak.

using UnityEngine;
using UnityEngine.InputSystem;   // ← Yeni paket namespace'i

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;

    private Rigidbody rb;
    private Camera mainCamera;
    private Vector3 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        mainCamera = Camera.main;
    }

    private void Update()
    {
        // NGO'ya geçince buraya eklenecek:
        // if (!IsOwner) return;
        ReadInput();
    }

    private void FixedUpdate()
    {
        // NGO'ya geçince buraya eklenecek:
        // if (!IsOwner) return;
        Move();
        Rotate();
    }

    private void ReadInput()
    {
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
        if (moveDirection == Vector3.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        rb.rotation = Quaternion.RotateTowards(
            rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime
        );
    }

    public bool IsMoving => moveDirection != Vector3.zero;
}