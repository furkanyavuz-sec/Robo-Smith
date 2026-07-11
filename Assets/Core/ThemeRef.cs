// ThemeRef.cs — Sahnedeki tema referans taşıyıcısı
// Görev: MapTheme editor'da Generate Map ile bağlanır ama RUNTIME kodu
//   (RobotBodyBuilder — arena robot gövdesi) statik editor alanlarına
//   erişemez. Bu bileşen temayı sahne objesi olarak taşır; MapGenerator
//   üretir. Tema yoksa/paket importsuzsa alanlar null → primitif fallback.

using UnityEngine;

public class ThemeRef : MonoBehaviour
{
    public MapTheme theme;

    public static ThemeRef Instance { get; private set; }

    private void Awake()  => Instance = this;
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Runtime güvenli erişim (sahnede/atamada yoksa GERÇEK null —
    /// Unity sahte-null'u ?. operatörünü yanıltmasın).</summary>
    public static MapTheme Current =>
        Instance != null && Instance.theme != null ? Instance.theme : null;
}
