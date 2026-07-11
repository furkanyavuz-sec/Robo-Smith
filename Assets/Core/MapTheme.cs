// MapTheme.cs — Sci-Fi kit görsel teması
// Görev: MapGenerator'ın primitif görsellerinin yerine geçecek kit
//   prefablarını tek asset'te toplar. Bir alan BOŞSA generator o parça
//   için primitife düşer — kit importlu olmayan makinede veya tema
//   atanmadan harita aynen eskisi gibi kurulur (MP dahil hiçbir oynanış
//   collider'ı temadan etkilenmez; kit modüllerinin collider'ları kapatılır).
// Kurulum: ThemeWirer (kit import edilince yazılacak) asset'i üretip
//   doldurur ve MapGenerator.theme alanına bağlar; sonra Generate Map.

using UnityEngine;

[CreateAssetMenu(fileName = "SciFiMapTheme", menuName = "RoboSmith/Map Theme")]
public class MapTheme : ScriptableObject
{
    [Header("Mimari Modüller (boş alan = primitif fallback)")]
    public GameObject floorTile;      // Zemin karosu — x/z'de döşenir
    public GameObject wallPanel;      // Düz duvar paneli — uzun eksende döşenir
    public GameObject pillar;         // Sütun/direk
    public GameObject barrierDoor;    // Enerji bariyeri / kapı görseli
    public GameObject crate;          // Hurdalık kasası

    [Header("İstasyon Kabukları (görsel gövde — etkileşim collider'ı bizde)")]
    public GameObject supplyShell;
    public GameObject processorShell;
    public GameObject assemblyShell;
    public GameObject trashShell;
    public GameObject consoleShell;

    [Header("Dekor (duvar kenarlarına serpiştirilecek proplar)")]
    public GameObject[] decorProps;
}
