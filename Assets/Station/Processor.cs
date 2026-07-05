// Processor.cs — Çoklu tarife (multi-recipe) yükseltildi.
// Durum makinesi (Idle → Processing → Ready) değişmedi.
// Sadece "hangi input, hangi output?" sorusu artık recipes listesinden okunuyor.

using System.Collections.Generic;
using UnityEngine;

public class Processor : BaseStation, IProgressReporter
{
    // ── IProgressReporter (StationProgressSync client bar'ı için) ────────
    public int ProgressStage => currentState switch
    {
        State.Processing => StationProgressSync.STAGE_WORKING,
        State.Ready      => StationProgressSync.STAGE_READY,
        _                => StationProgressSync.STAGE_IDLE
    };
    public float Progress01  => currentState == State.Processing
        ? 1f - (timer / currentDuration) : 0f;
    public float SecondsLeft => currentState == State.Processing ? timer : 0f;

    // ── Durum Makinesi ───────────────────────────────────────────────────
    private enum State { Idle, Processing, Ready }

    // ── Inspector Ayarları ───────────────────────────────────────────────
    [Header("Tarifler")]
    [SerializeField] private List<ProcessorRecipe> recipes = new()
    {
        new ProcessorRecipe
        {
            recipeName          = "Demir → Çelik Plaka",
            inputType           = ItemType.Iron,
            processingDuration  = 3f
            // outputPrefab Inspector'dan bağlanacak
        },
        new ProcessorRecipe
        {
            recipeName          = "Ham Plazma → Plazma Çekirdeği",
            inputType           = ItemType.RawPlasma,
            processingDuration  = 4f
            // outputPrefab Inspector'dan bağlanacak
        },
        new ProcessorRecipe
        {
            recipeName          = "Devre → Mikroçip",
            inputType           = ItemType.Circuit,
            processingDuration  = 3.5f
            // outputPrefab Inspector'dan bağlanacak
        }
    };

    [Header("Görsel Referanslar (Opsiyonel)")]
    [SerializeField] private Transform itemDisplayPoint;
    [SerializeField] private Renderer  progressRenderer;

    // ── Dahili Durum ─────────────────────────────────────────────────────
    private State           currentState   = State.Idle;
    private float           timer          = 0f;
    private float           currentDuration = 3f;  // Aktif tarifte belirlenir
    private GameObject      inputItem      = null;
    private GameObject      outputItem     = null;
    private ProcessorRecipe activeRecipe   = null;

    // ── BaseStation Sözleşmesi ───────────────────────────────────────────

    public override bool CanInteract(PlayerInteraction player)
    {
        switch (currentState)
        {
            case State.Idle:
                // Eli boş gelirse ret
                if (player.HeldObject == null) return false;

                // Elindeki item için geçerli tarif var mı?
                if (!player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
                    return false;

                return FindRecipe(item.Type) != null;

            case State.Processing:
                return false; // Çalışırken kimse dokunamaz

            case State.Ready:
                return player.HeldObject == null; // El boşsa ürünü alabilir

            default: return false;
        }
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        switch (currentState)
        {
            case State.Idle:    AcceptInput(player);  break;
            case State.Ready:   GiveOutput(player);   break;
        }
    }

    // ── Durum Geçiş Metodları ────────────────────────────────────────────

    private void AcceptInput(PlayerInteraction player)
    {
        player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item);
        activeRecipe    = FindRecipe(item.Type);
        inputItem       = player.HeldObject;
        currentDuration = activeRecipe.processingDuration;

        player.ForceDropFromStation();
        PlaceItemOnDisplay(inputItem);

        timer        = currentDuration;
        currentState = State.Processing;

        Debug.Log($"[Processor] '{activeRecipe.recipeName}' başladı. " +
                  $"Süre: {currentDuration}s");
    }

    private void FinishProcessing()
    {
        // Input yok et
        if (inputItem != null)
        {
            Destroy(inputItem);
            inputItem = null;
        }

        // Output spawn et
        if (activeRecipe?.outputPrefab == null)
        {
            Debug.LogError($"[Processor] '{activeRecipe?.recipeName}' tarifinin " +
                           $"outputPrefab'ı atanmamış!");
            currentState = State.Idle;
            StationProgressBar.Hide(gameObject);
            return;
        }

        Vector3 spawnPos = itemDisplayPoint != null
                         ? itemDisplayPoint.position
                         : transform.position + Vector3.up * 1.2f;

        outputItem   = Instantiate(activeRecipe.outputPrefab, spawnPos, Quaternion.identity);
        currentState = State.Ready;
        activeRecipe = null;

        StationProgressBar.Hide(gameObject);
        Debug.Log("[Processor] İşlem tamamlandı. Ürün hazır.");
    }

    private void GiveOutput(PlayerInteraction player)
    {
        if (outputItem != null)
        {
            outputItem.transform.SetParent(null);
            player.PickupFromStation(outputItem);
            outputItem = null;
        }

        currentState = State.Idle;
        Debug.Log("[Processor] Ürün alındı. Masa boşta.");
    }

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    private void Update()
    {
        if (currentState != State.Processing) return;

        timer -= Time.deltaTime;
        float progress = 1f - (timer / currentDuration);

        UpdateProgressVisual(progress);
        StationProgressBar.Show(gameObject, progress, timer);

        if (timer <= 0f)
            FinishProcessing();
    }

    // ── Yardımcı Metodlar ────────────────────────────────────────────────

    /// <summary>Verilen ItemType için tarif listesinde eşleşen ilk tarifi döndürür.</summary>
    private ProcessorRecipe FindRecipe(ItemType type)
    {
        foreach (ProcessorRecipe recipe in recipes)
            if (recipe.inputType == type) return recipe;

        return null;
    }

    private void PlaceItemOnDisplay(GameObject item)
    {
        if (item == null) return;

        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        if (item.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = true;

        // MP'de NetworkObject parent'lanamaz — yardımcı pozisyona sabitler
        if (itemDisplayPoint != null)
            NetworkItem.PlaceAtAnchor(item, itemDisplayPoint, Vector3.zero);
    }

    private void UpdateProgressVisual(float progress)
    {
        if (progressRenderer == null) return;
        progressRenderer.material.color = Color.Lerp(Color.yellow, Color.green, progress);
    }

    // ── Gizmos ──────────────────────────────────────────────────────────

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = currentState switch
        {
            State.Idle       => Color.white,
            State.Processing => Color.yellow,
            State.Ready      => Color.green,
            _                => Color.white
        };

        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.6f, Vector3.one * 0.5f);
    }
}