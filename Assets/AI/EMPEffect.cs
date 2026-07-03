    // EMPEffect.cs
// Görev: BattleRobot'a eklenir, EMP çarptığında robotu
//        belirli süre tamamen dondurur.
// BattleRobot her Update başında IsFrozen kontrolü yapar.

using UnityEngine;

public class EMPEffect : MonoBehaviour
{
    private float     freezeTimer    = 0f;
    private bool      isFrozen       = false;
    private Renderer  bodyRenderer;
    private BattleRobot robot;   // Çok parçalı gövdeyi bunun üzerinden boyarız

    // Shader property — Built-in "_Color", URP "_BaseColor"
    private static readonly int PropColor     = Shader.PropertyToID("_Color");
    private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock propBlock;

    public bool IsFrozen => isFrozen;

    private void Awake()
    {
        robot        = GetComponent<BattleRobot>();
        bodyRenderer = GetComponentInChildren<Renderer>();
        propBlock    = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (!isFrozen) return;

        freezeTimer -= Time.deltaTime;

        // Dondurulmuş efekt: mavi titreme
        float pulse = Mathf.PingPong(Time.time * 4f, 1f);
        ApplyFreezeColor(Color.Lerp(Color.blue, Color.cyan, pulse));

        if (freezeTimer <= 0f)
            Unfreeze();
    }

    /// <summary>EMP çarptığında BattleRobot çağırır.</summary>
    public void ApplyFreeze(float duration)
    {
        isFrozen    = true;
        freezeTimer = duration;

        Debug.Log($"<color=blue>[EMP] {gameObject.name} {duration}s donduruldu!</color>");
    }

    private void Unfreeze()
    {
        isFrozen = false;

        // HP'ye uygun gövde rengine geri dön
        if (robot != null) robot.RefreshBodyColor();
        else               ApplyFreezeColor(Color.white);

        Debug.Log($"<color=cyan>[EMP] {gameObject.name} EMP etkisinden çıktı.</color>");
    }

    private void ApplyFreezeColor(Color color)
    {
        // Çok parçalı gövde: BattleRobot tüm parçaları boyar
        if (robot != null) { robot.TintBody(color); return; }

        if (bodyRenderer == null) return;
        bodyRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(PropColor,     color);
        propBlock.SetColor(PropBaseColor, color);
        bodyRenderer.SetPropertyBlock(propBlock);
    }
}