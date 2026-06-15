// ProcessorRecipe.cs
// Görev: "Şu input gelirse şu output çıkar, bu kadar sürede" kuralını saklar.
// Processor.cs bu tarife bakarak ne yapacağına karar verir.
// Yeni madde eklemek = sadece Inspector'da listeye bir satır eklemek.

using UnityEngine;

[System.Serializable]
public class ProcessorRecipe
{
    [Tooltip("Fırına bu item tipi atılırsa bu tarif çalışır.")]
    public ItemType inputType;

    [Tooltip("İşlem sonucunda spawn edilecek prefab.")]
    public GameObject outputPrefab;

    [Tooltip("İşlem süresi (saniye).")]
    public float processingDuration = 3f;

    [Tooltip("Tarif adı — Inspector okunabilirliği için.")]
    public string recipeName = "Tarif";
}