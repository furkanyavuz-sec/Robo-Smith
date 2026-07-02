// CameraController.cs
using UnityEngine;

public class CameraController : MonoBehaviour
{
    // Singleton kalıbı ile ağda doğan lokal oyuncu bu kameraya şak diye kendini bildirecek
    public static CameraController Instance { get; private set; }

    [Header("Hedef")]
    [SerializeField] private Transform target;          

    [Header("Pozisyon")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -6f);
    [SerializeField] private float smoothSpeed = 6f;    

    [Header("Rotasyon")]
    [SerializeField] private bool lookAtTarget = true;  
    [SerializeField] private float rotationSmoothSpeed = 5f;

    [Header("Dinamik Offset (Opsiyonel)")]
    [SerializeField] private bool useDynamicOffset = false; 
    [SerializeField] private float dynamicOffsetStrength = 1.5f;

    private Vector3 currentVelocity = Vector3.zero;  
    private PlayerController playerController;           

    private void Awake()
    {
        // Singleton Kurulumu
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Başlangıçta hedef yoksa hata fırlatma, çünkü oyuncu ağda birazdan doğacak!
        if (target != null)
        {
            playerController = target.GetComponent<PlayerController>();
        }
    }

    private void Start()
    {
        // Offline/tekli oyun: SetTarget ağdan gelmeyecek —
        // sahnede hazır bir oyuncu varsa ona kilitlen.
        // (OfflinePlayerSpawner spawn ettiğinde zaten SetTarget çağırır.)
        if (target == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) SetTarget(pc.transform);
        }
    }

    // 🌟 AĞDA DOĞAN LOKAL OYUNCUNUN KAMERAYI KENDİNE KİLİTLEMESİNİ SAĞLAYAN SİBER FONKSİYON
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            playerController = target.GetComponent<PlayerController>();
            Debug.Log($"[Camera] Kamera başarıyla lokal oyuncuya kilitlendi: {newTarget.name}");
        }
    }

    private void LateUpdate()
    {
        // Hedef henüz doğmadıysa kamerayı hareket ettirip hata fırlatma, beklemede kal
        if (target == null) return;

        MoveCamera();

        if (lookAtTarget)
            RotateCamera();
    }

    private void MoveCamera()
    {
        Vector3 desiredPosition = target.position + offset;

        if (useDynamicOffset && playerController != null && playerController.IsMoving)
        {
            Vector3 moveDir = (target.position - transform.position).normalized;
            moveDir.y = 0f;
            desiredPosition += moveDir * dynamicOffsetStrength;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            1f / smoothSpeed    
        );
    }

    private void RotateCamera()
    {
        Vector3 directionToTarget = target.position - transform.position;
        if (directionToTarget == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target.position + offset, 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
    }
}