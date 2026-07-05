// MissingScriptCleaner.cs — Editör aracı
// Görev: Açık sahnedeki TÜM objelerde "Missing Script" bileşenlerini söker.
// Ne zaman gerekir: MonoBehaviour sınıfları başka dosyaya taşındığında
//   (script kimliği değişir) sahnelerdeki eski kopyalar kör referans kalır
//   ve her yüklemede "The referenced script (Unknown)... is missing!" basar.
// Kullanım: Unity menü çubuğu → RoboSmith → Kayıp Script Temizliği
//   (her sahnede bir kez çalıştır, sonra Ctrl+S).

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptCleaner
{
    [MenuItem("RoboSmith/Kayıp Script Temizliği (Açık Sahne)")]
    private static void CleanActiveScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[MissingScriptCleaner] Play modunda çalıştırma — " +
                             "değişiklikler kaydedilmez. Önce Play'den çık.");
            return;
        }

        int removed = 0;
        foreach (GameObject root in
                 SceneManager.GetActiveScene().GetRootGameObjects())
            removed += CleanRecursive(root);

        if (removed > 0)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[MissingScriptCleaner] ✅ {removed} kayıp script bileşeni " +
                  "temizlendi." + (removed > 0 ? " Sahneyi kaydet (Ctrl+S)." : ""));
    }

    private static int CleanRecursive(GameObject go)
    {
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

        foreach (Transform child in go.transform)
            removed += CleanRecursive(child.gameObject);

        return removed;
    }
}
#endif
