// CameraController.cs
// Görev: Hedefi yukarıdan/arkadan pürüzsüzce takip eden kamera.
// Yöntem: SmoothDamp — Lerp'ten üstün çünkü hız sürekliliği var,
//         ani yön değişimlerinde "zıplama" yapmaz.

using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Hedef")]
    [SerializeField] private Transform target;          // Player'ı sürükle

    [Header("Pozisyon")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -6f);
    [SerializeField] private float smoothSpeed = 6f;    // Büyük = sert, Küçük = gecikmeli

    [Header("Rotasyon")]
    [SerializeField] private bool lookAtTarget = true;  // Her zaman hedefe baksın mı?
    [SerializeField] private float rotationSmoothSpeed = 5f;

    [Header("Dinamik Offset (Opsiyonel)")]
    [SerializeField] private bool useDynamicOffset = false; // Hareket yönüne göre hafif kaydır
    [SerializeField] private float dynamicOffsetStrength = 1.5f;

    // Dahili
    private Vector3   currentVelocity = Vector3.zero;  // SmoothDamp için zorunlu ref
    private PlayerController playerController;           // Hareket durumu için

    private void Awake()
    {
        if (target == null)
        {
            Debug.LogError("[Camera] Target atanmamış! Player'ı Inspector'dan sürükle.");
            return;
        }

        // PlayerController'a opsiyonel referans — dinamik offset için
        playerController = target.GetComponent<PlayerController>();
    }

    // Kamera fiziği LateUpdate'de: önce tüm hareket hesaplanır, sonra kamera konumlanır.
    // Update veya FixedUpdate'de yaparsan kamera titrer.
    private void LateUpdate()
    {
        if (target == null) return;

        MoveCamera();

        if (lookAtTarget)
            RotateCamera();
    }

    private void MoveCamera()
    {
        // Temel hedef pozisyon: oyuncu + sabit offset
        Vector3 desiredPosition = target.position + offset;

        // Dinamik offset: oyuncu hareket ediyorsa kamera hafif öne kayar
        if (useDynamicOffset && playerController != null && playerController.IsMoving)
        {
            // Oyuncunun gittiği yönde ekstra kaydırma
            Vector3 moveDir = (target.position - transform.position).normalized;
            moveDir.y = 0f;
            desiredPosition += moveDir * dynamicOffsetStrength;
        }

        // SmoothDamp: fizik tabanlı yumuşatma, deltaTime'a göre frame-rate bağımsız
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            1f / smoothSpeed    // smoothTime: küçük = hızlı yaklaşma
        );
    }

    private void RotateCamera()
    {
        // Kameranın bakması gereken yön
        Vector3 directionToTarget = target.position - transform.position;

        if (directionToTarget == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        // Rotasyonu da yumuşat
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    // Inspector'da offset'i ve dinamik kaydırmayı görselleştir
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        // Hedef pozisyon noktası
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target.position + offset, 0.2f);

        // Kamera → hedef çizgisi
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
    }
}