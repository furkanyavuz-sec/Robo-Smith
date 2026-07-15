// MapTheme.cs — Sci-Fi kit görsel teması (tam kapsam)
// Görev: Haritanın TÜM görünür öğelerinin kit karşılıklarını tek asset'te
//   toplar: mimari (zemin/duvar/bariyer), istasyon gövdeleri (kabuklar),
//   depo/platform ve dekor. Bir alan BOŞSA generator o parça için eski
//   primitife düşer — kit importlu olmayan makinede harita aynen kurulur.
// Oynanış collider'ları temadan etkilenmez: kit modüllerinin collider'ları
//   kapatılır, fizik container BoxCollider'larında / istasyon prefabında kalır.
// Kurulum: MapGenerator sağ tık > Wire SciFi Theme doldurur ve bağlar.

using UnityEngine;

[CreateAssetMenu(fileName = "SciFiMapTheme", menuName = "RoboSmith/Map Theme")]
public class MapTheme : ScriptableObject
{
    [Header("Zeminler (boş alan = primitif fallback)")]
    public GameObject floorTile;      // Garaj zemini — x/z'de döşenir
    public GameObject scrapFloorTile; // Hurdalık zemini (farklı doku)
    public GameObject platformFloor;  // Çekirdek bölge platformu
    public GameObject depotBase;      // Takım deposu taban plakası

    [Header("Duvar & Bariyer")]
    public GameObject wallPanel;      // Dış duvar paneli — uzun eksende döşenir
    public GameObject windowPanel;    // Pencereli panel (dış duvarlar — mahalle görünür)
    public GameObject ceilingTile;    // Tavan karosu (ters döşenir — üstten görünmez)
    public GameObject ceilingBeam;    // Tavan kirişi
    public GameObject pillar;         // Sütun/direk
    public GameObject barrierFence;   // Zone bariyeri — y'de istiflenir,
                                      // tepesine barrierColor enerji şeridi eklenir

    [Header("Proplar")]
    public GameObject crate;          // Hurdalık kasası

    [Header("İstasyon Kabukları (gövdeyi değiştirir; neon çerçeve/etiket/")]
    // beacon renk dili aynen kalır — hangi istasyon ne üretiyor okunur)
    public GameObject supplyShell;    // Tedarik + hurdalık kutuları
    public GameObject processorShell; // İşleme masası
    public GameObject weaponShell;    // Silah atölyeleri
    public GameObject assemblyShell;  // Montaj istasyonu
    public GameObject trashShell;     // Çöp kutusu
    public GameObject consoleShell;   // Drone konsolu
    public GameObject plasmaShell;    // Plazma kaynağı

    [Header("Oyuncu Karakteri (sevimli low-poly robot — boşsa primitif)")]
    public GameObject playerCharacter;

    [Header("Arena Savaş Robotu (polyart karakter — boşsa primitif gövde)")]
    public GameObject battleCharacter;
    public Material   playerCharacterMaterial;  // Paket shader'ı URP'de
                                                // derlenmezse (mor) bu basılır

    [Header("Robot Gövde Parçaları (arena robotu — boşsa primitif)")]
    public GameObject robotCore;     // Kafa/gövde blokları (pahlı küp)
    public GameObject robotPlate;    // Yan plakalar (uzun pahlı küp)
    public GameObject robotJoint;    // Omuzlar (pahlı küre)
    public GameObject robotBackpack; // Sırt çantası (küçük batarya vb.)

    [Header("Kaideler")]
    public GameObject stationBase;     // İstasyon kabuğu altı kaide plakası
    public GameObject chassisPedestal; // Şasi sergi platformu

    [Header("Mahalle — Sevimli Şehir (Pandazole + SimplePoly)")]
    public GameObject[] cityBuildings; // Cadde çevresi binalar
    public GameObject   roadStraight;  // Yol karosu (düz)
    public GameObject   streetLight;   // Sokak lambası
    public GameObject[] cityCars;      // Park halinde araçlar
    public GameObject[] cityTrees;     // Ağaçlar (çevre + park)
    public GameObject[] cityBushes;    // Çalı/kaya dolgusu
    public GameObject[] cityProps;     // Durak, hidrant, bank, tabela, koni

    [Header("Item Şekilleri (tip → paket propu; listede olmayan tip küp kalır)")]
    // Renk zaten tipe göre otomatik biner (VisualThemeManager property
    // block) — şekil çeşitliliği taşınan malzemeyi uzaktan okutur.
    public ItemShape[] itemShapes;

    [System.Serializable]
    public struct ItemShape
    {
        public ItemType   type;
        public GameObject prefab;
    }

    [Header("Atmosfer")]
    public GameObject[] decorProps;   // Duvar diplerine serpiştirilen proplar
    public Material skybox;           // Sahne gökyüzü (RenderSettings.skybox)
}
