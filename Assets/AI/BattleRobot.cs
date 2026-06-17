// BattleRobot.cs — Yeni silah/zırh/EMP/Kalkan sistemi entegre

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EMPEffect))]
public class BattleRobot : MonoBehaviour
{
    public enum RobotState { Chase, Attack, Flee, Repair }

    // ── Inspector ────────────────────────────────────────────────────────
    [Header("Hareket")]
    [SerializeField] private float chaseSpeed   = 4f;
    [SerializeField] private float fleeSpeed    = 5.5f;
    [SerializeField] private float fleeDistance = 10f;

    [Header("Onarım")]
    [SerializeField] private float repairHPThreshold  = 0.30f;
    [SerializeField] private float repairRate          = 5f;
    [SerializeField] private float repairExitThreshold = 0.60f;

    [Header("Görsel")]
    [SerializeField] private RobotHealthBar healthBar;
    [SerializeField] private Renderer       bodyRenderer;

    // ── Runtime Veri ─────────────────────────────────────────────────────
    private RobotStatSheet statSheet;
    private ArmorType      equippedArmor = ArmorType.None;

    private RobotState   currentState = RobotState.Chase;
    private NavMeshAgent agent;
    private BattleRobot  currentTarget;
    private EMPEffect    empEffect;
    private ShieldController shieldController;

    private int  maxHP;
    private int  currentHP;

    public RobotStatSheet StatSheet => statSheet;

    // Silah cooldown takibi
    private Dictionary<WeaponData, float> lastAttackTimes = new();

    // Renk
    private MaterialPropertyBlock propBlock;
    private static readonly int   PropColor = Shader.PropertyToID("_Color");
    private Coroutine             flashCoroutine;

    public bool      IsDead    => currentHP <= 0;
    public int TeamID { get; private set; }
    public int       CurrentHP => currentHP;

    public int       MaxHP     => maxHP;

    private RobotMemory        memory;
    private TargetSelector     targetSelector;
    private TacticalPositioner positioner;
    private WeaponBehavior     weaponBehavior;

    // ── Başlatma ─────────────────────────────────────────────────────────

    public void Initialize(RobotStatSheet sheet, ArmorType armor, int teamId)
    {
        statSheet     = sheet;
        equippedArmor = armor;
        TeamID        = teamId;

        maxHP      = Mathf.Max(sheet.HP, 1);
        currentHP  = maxHP;

        agent             = GetComponent<NavMeshAgent>();
        agent.speed       = chaseSpeed;
        empEffect         = GetComponent<EMPEffect>();
        propBlock         = new MaterialPropertyBlock();

        memory         = GetComponent<RobotMemory>();
        targetSelector = GetComponent<TargetSelector>();
        positioner     = GetComponent<TacticalPositioner>();
        weaponBehavior = GetComponent<WeaponBehavior>();

        memory?.Reset();

        // Kalkan silahı varsa ShieldController başlat
        WeaponData shieldWeapon = GetWeaponOfCategory(WeaponCategory.Defensive);
        if (shieldWeapon != null)
        {
            shieldController = gameObject.AddComponent<ShieldController>();
            shieldController.Initialize(this, shieldWeapon.reflectRatio);
        }

        healthBar?.UpdateBar(currentHP, maxHP);

        Debug.Log($"[BattleRobot] Takım {teamId} → HP:{maxHP} " +
                  $"ATK:{sheet.ATK} SPD:{sheet.SPD} " +
                  $"Zırh:{armor} Silah:{sheet.weaponCount}");
    }

    // ── Update / FSM ─────────────────────────────────────────────────────

    private void Update()
{
    if (IsDead) return;
    if (empEffect != null && empEffect.IsFrozen) return;

    // Hedef seçimini TargetSelector'a bırak
    currentTarget = targetSelector?.CurrentTarget
                 ?? ArenaManager.Instance?.GetClosestEnemy(this);

    if (currentTarget == null) return;

    // Role göre davranış
    RobotRole role = TeamCoordinator.Instance?.GetRole(this)
                  ?? RobotRole.Aggressor;

    // Kalkan aktifleştirme
    if (weaponBehavior != null && weaponBehavior.ShouldActivateShield())
        shieldController?.TryActivate();

    if (ShouldRepair() && currentState != RobotState.Repair)
        EnterRepair();

    switch (currentState)
    {
        case RobotState.Chase:  TickChase(role);  break;
        case RobotState.Attack: TickAttack();      break;
        case RobotState.Flee:   TickFlee();        break;
        case RobotState.Repair: TickRepair();      break;
    }

    UpdateBodyColor();
}

    // ── FSM State'leri ───────────────────────────────────────────────────

    private void TickChase(RobotRole role)
{
    agent.speed = chaseSpeed;

    Vector3 destination;

    switch (role)
    {
        case RobotRole.Aggressor:
            // Flanklama hareketi
            destination = positioner != null
                ? positioner.GetFlankPosition(currentTarget)
                : currentTarget.transform.position;
            break;

        case RobotRole.Support:
            // Takım arkadaşının yanında konumlan
            BattleRobot ally = TeamCoordinator.Instance?.GetAllyToProtect(this);
            destination = ally != null && positioner != null
                ? positioner.GetSupportPosition(ally)
                : currentTarget.transform.position;
            break;

        case RobotRole.Defender:
            // Engel arkasına çekil
            destination = positioner != null
                ? positioner.GetCoverPosition(currentTarget)
                : transform.position;
            break;

        default:
            destination = currentTarget.transform.position;
            break;
    }

    agent.SetDestination(destination);

    float dist = DistanceTo(currentTarget);

    bool hasRanged = GetWeaponOfCategory(WeaponCategory.Ranged) != null
                  || GetWeaponOfCategory(WeaponCategory.AOE)    != null;

    if (hasRanged && dist < fleeDistance * 0.5f)
    {
        currentState = RobotState.Flee;
        return;
    }

    WeaponData best = GetBestWeapon(dist);
    if (best != null && dist <= best.effectiveRange)
        currentState = RobotState.Attack;
}


    private void TickAttack()
{
    float      dist   = DistanceTo(currentTarget);
    WeaponData weapon = GetBestWeaponForDistance(dist);

    if (weapon == null) { currentState = RobotState.Chase; return; }

    if (dist <= weapon.effectiveRange)
        agent.ResetPath();
    else
        agent.SetDestination(currentTarget.transform.position);

    if (currentTarget == null || currentTarget.IsDead)
    {
        currentState = RobotState.Chase;
        return;
    }

    FaceTarget(currentTarget.transform);

    if (dist <= weapon.effectiveRange)
    {
        // Cooldown kontrolü — lastAttackTimes'dan direkt oku
        float lastTime = 0f;
        lastAttackTimes.TryGetValue(weapon, out lastTime);
        bool ready = Time.time - lastTime >= weapon.attackCooldown;

        if (ready)
            FireWeapon(weapon, currentTarget);
    }

    bool hasRanged = GetWeaponOfCategory(WeaponCategory.Ranged) != null;
    if (hasRanged && dist < fleeDistance * 0.4f)
        currentState = RobotState.Flee;
}

    private void TickFlee()
    {
        agent.speed = fleeSpeed;

        Vector3 fleeDir    = (transform.position - currentTarget.transform.position).normalized;
        Vector3 fleeTarget = transform.position + fleeDir * fleeDistance;

        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
            agent.SetDestination(hit.position);

        if (DistanceTo(currentTarget) >= fleeDistance)
            currentState = RobotState.Attack;
    }

    private void TickRepair()
    {
        agent.speed = chaseSpeed * 0.5f;

        if (currentTarget != null)
        {
            Vector3 dir = (transform.position - currentTarget.transform.position).normalized;
            Vector3 pos = transform.position + dir * 8f;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        Heal(Mathf.RoundToInt(repairRate * Time.deltaTime));

        if ((float)currentHP / maxHP >= repairExitThreshold)
        {
            Debug.Log($"[BattleRobot] Takım {TeamID} onarım bitti, savaşa dönüyor.");
            currentState = RobotState.Chase;
        }
    }

    // ── Silah Sistemi ────────────────────────────────────────────────────

    private void FireWeapon(WeaponData weapon, BattleRobot target)
    {
        SetWeaponCooldown(weapon);

        switch (weapon.category)
        {
            case WeaponCategory.Melee:     FireMelee(weapon);          break;
            case WeaponCategory.Ranged:    FireRanged(weapon, target);  break;
            case WeaponCategory.AOE:       FireRocket(weapon, target);  break;
            case WeaponCategory.Debuff:    FireEMP(weapon, target);     break;
            case WeaponCategory.Defensive: break; // Pasif — otomatik
        }
    }

    private void FireMelee(WeaponData weapon)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, weapon.effectiveRange);
        foreach (Collider col in hits)
        {
            BattleRobot hit = col.GetComponentInParent<BattleRobot>();
            if (hit == null || hit == this || hit.TeamID == TeamID) continue;
            hit.TakeDamage(GetFinalDamage(weapon), WeaponCategory.Melee);
        }
    }

    private void FireRanged(WeaponData weapon, BattleRobot target)
{
    if (weapon.projectilePrefab != null)
    {
        GameObject proj = Instantiate(
            weapon.projectilePrefab,
            transform.position + Vector3.up,
            Quaternion.identity
        );
        if (proj.TryGetComponent<Projectile>(out Projectile p))
            p.Initialize((int)TeamID, target.transform, GetFinalDamage(weapon));
        return;
    }

    // Prefab yoksa anlık hasar
    target.TakeDamage(GetFinalDamage(weapon), WeaponCategory.Ranged, this);
}

    private void FireRocket(WeaponData weapon, BattleRobot target)
{
    if (weapon.projectilePrefab != null)
    {
        GameObject proj = Instantiate(
            weapon.projectilePrefab,
            transform.position + Vector3.up,
            Quaternion.identity
        );
        if (proj.TryGetComponent<RocketProjectile>(out RocketProjectile r))
            r.Initialize((int)TeamID, target.transform, GetFinalDamage(weapon), weapon.aoeRadius);
        return;
    }

    // Prefab yoksa AOE anlık hasar
    Collider[] hits = Physics.OverlapSphere(target.transform.position, weapon.aoeRadius);
    foreach (Collider col in hits)
    {
        BattleRobot hit = col.GetComponentInParent<BattleRobot>();
        if (hit == null || hit.TeamID == TeamID) continue;
        hit.TakeDamage(GetFinalDamage(weapon), WeaponCategory.AOE, this);
    }
}

    private void FireEMP(WeaponData weapon, BattleRobot target)
{
    if (weapon.projectilePrefab != null)
    {
        GameObject proj = Instantiate(
            weapon.projectilePrefab,
            transform.position + Vector3.up,
            Quaternion.identity
        );
        if (proj.TryGetComponent<Projectile>(out Projectile p))
            p.Initialize((int)TeamID, target.transform, GetFinalDamage(weapon));
    }

    // Prefab olsa da olmasa da EMP etkisini uygula
    float dist = DistanceTo(target);
    if (dist <= weapon.effectiveRange)
        target.ApplyEMP(weapon.debuffDuration);
}

    // ── Hasar Alma ───────────────────────────────────────────────────────

    /// <summary>
    /// Zırh direncini uygulayarak hasar al.
    /// Kalkan varsa önce TryBlock'a sor.
    /// </summary>
    public void TakeDamage(int rawDamage, WeaponCategory attackerCategory,
                       BattleRobot attacker = null)
{
    if (IsDead) return;

    // Belleğe kaydet
    if (attacker != null)
        memory?.RecordDamage(attacker, rawDamage);

        // Kalkan kontrolü
        if (shieldController != null &&
            shieldController.TryBlock(rawDamage, attacker))
            return; // Hasar engellendi

        // Zırh direnci uygula
        float resistance  = ArmorResistanceTable.GetResistance(equippedArmor, attackerCategory);
        int   finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * resistance));

        currentHP = Mathf.Max(0, currentHP - finalDamage);
        healthBar?.UpdateBar(currentHP, maxHP);

        if (resistance < 1f)
            Debug.Log($"<color=green>[Zırh] Direnç! {rawDamage} → {finalDamage}</color>");
        else if (resistance > 1f)
            Debug.Log($"<color=red>[Zırh] Zayıflık! {rawDamage} → {finalDamage}</color>");

        if (IsDead) { OnDeath(); return; }

        FlashColor(Color.white, 0.1f);
    }

    // Geriye dönük uyumluluk — eski TakeDamage(int) çağrıları için
    public void TakeDamage(int damage) =>
        TakeDamage(damage, WeaponCategory.Melee, null);

    // ── Efektler ─────────────────────────────────────────────────────────

    public void ApplyEMP(float duration)
    {
        if (empEffect != null)
            empEffect.ApplyFreeze(duration);
    }

    private void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        healthBar?.UpdateBar(currentHP, maxHP);
    }

    private void OnDeath()
    {
        agent.ResetPath();
        agent.enabled = false;
        FlashColor(Color.red, 0f);
        Debug.Log($"[BattleRobot] Takım {TeamID} robotu yok edildi!");
        ArenaManager.Instance?.OnRobotDestroyed(this);
        Destroy(gameObject, 1.2f);
    }

    // ── Silah Yardımcıları ───────────────────────────────────────────────
    private WeaponData GetBestWeaponForDistance(float distance)
{
    WeaponData melee  = GetWeaponOfCategory(WeaponCategory.Melee);
    WeaponData ranged = GetWeaponOfCategory(WeaponCategory.Ranged);
    WeaponData aoe    = GetWeaponOfCategory(WeaponCategory.AOE);
    WeaponData emp    = GetWeaponOfCategory(WeaponCategory.Debuff);

    if (melee  != null && distance <= melee.effectiveRange)  return melee;
    if (ranged != null && distance <= ranged.effectiveRange) return ranged;
    if (aoe    != null && distance <= aoe.effectiveRange)    return aoe;
    if (emp    != null && distance <= emp.effectiveRange)    return emp;

    // Menzil dışı — en uzun menzilli
    WeaponData longest  = null;
    float      maxRange = 0f;
    foreach (WeaponData w in GetAllWeapons())
        if (w != null && w.effectiveRange > maxRange)
        {
            maxRange = w.effectiveRange;
            longest  = w;
        }
    return longest;
}
    private WeaponData GetBestWeapon(float distance)
{
    // WeaponBehavior varsa ona sor
    if (weaponBehavior != null)
    {
        List<BattleRobot> enemies = ArenaManager.Instance?.GetEnemiesOf(TeamID);
        return weaponBehavior.SelectWeapon(currentTarget, distance, enemies);
    }

    // Fallback — eski sistem
    WeaponData melee  = GetWeaponOfCategory(WeaponCategory.Melee);
    WeaponData ranged = GetWeaponOfCategory(WeaponCategory.Ranged);
    WeaponData aoe    = GetWeaponOfCategory(WeaponCategory.AOE);
    WeaponData emp    = GetWeaponOfCategory(WeaponCategory.Debuff);

    if (melee  != null && distance <= melee.effectiveRange)  return melee;
    if (ranged != null && distance <= ranged.effectiveRange) return ranged;
    if (aoe    != null && distance <= aoe.effectiveRange)    return aoe;
    if (emp    != null && distance <= emp.effectiveRange)    return emp;

    return GetLongestRangeWeaponFallback();
}
    private WeaponData GetLongestRangeWeaponFallback()
{
    WeaponData longest = null;
    float      maxRange = 0f;
    foreach (WeaponData w in GetAllWeapons())
        if (w.effectiveRange > maxRange) { maxRange = w.effectiveRange; longest = w; }
    return longest;
}
    private WeaponData GetWeaponOfCategory(WeaponCategory cat)
    {
        if (statSheet == null) return null;
        foreach (WeaponData w in statSheet.equippedWeapons)
            if (w != null && w.category == cat) return w;
        return null;
    }

    private IEnumerable<WeaponData> GetAllWeapons()
{
    if (statSheet == null) yield break;
    foreach (WeaponData w in statSheet.equippedWeapons)
        if (w != null) yield return w;  // ← yield break değil yield return
}

    private int GetFinalDamage(WeaponData weapon)
    {
        float overtimeMult = MatchData.Instance != null
                           ? MatchData.Instance.OvertimeDamageMultiplier
                           : 1f;
        return Mathf.RoundToInt(weapon.damage * overtimeMult);
    }

    public bool IsWeaponReady(WeaponData weapon)
{
    if (weapon == null) return false;
    
    if (!lastAttackTimes.TryGetValue(weapon, out float last))
    {
        return true;
    }
    
    float elapsed = Time.time - last;
    Debug.Log($"[WeaponReady] {weapon.weaponName} | Gecen: {elapsed:F1}s | " +
              $"Cooldown: {weapon.attackCooldown}s | Hazir: {elapsed >= weapon.attackCooldown}");
    
    return elapsed >= weapon.attackCooldown;
}

    private void SetWeaponCooldown(WeaponData weapon)
    {
        lastAttackTimes[weapon] = Time.time;
    }

    // ── Görsel ──────────────────────────────────────────────────────────

    private void UpdateBodyColor()
    {
        if (bodyRenderer == null) return;
        if (empEffect != null && empEffect.IsFrozen) return; // EMP rengi öncelikli

        float ratio = (float)currentHP / maxHP;
        Color c     = Color.Lerp(Color.red, new Color(0.2f, 0.6f, 1f), ratio);

        bodyRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(PropColor, c);
        bodyRenderer.SetPropertyBlock(propBlock);
    }

    private void FlashColor(Color color, float duration)
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        if (bodyRenderer == null) yield break;
        bodyRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(PropColor, color);
        bodyRenderer.SetPropertyBlock(propBlock);
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
            UpdateBodyColor();
        }
    }

    // ── Yardımcı ────────────────────────────────────────────────────────

    private void FaceTarget(Transform t)
    {
        Vector3 dir = t.position - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            10f * Time.deltaTime
        );
    }

    private float DistanceTo(BattleRobot other) =>
        Vector3.Distance(transform.position, other.transform.position);

    private bool ShouldRepair() =>
        (float)currentHP / maxHP < repairHPThreshold;

    private void EnterRepair()
    {
        currentState = RobotState.Repair;
        Debug.Log($"[BattleRobot] Takım {TeamID} kritik HP, onarım modu.");
    }

    private void OnDrawGizmosSelected()
    {
        if (statSheet == null) return;
        foreach (WeaponData w in statSheet.equippedWeapons)
        {
            if (w == null) continue;
            Gizmos.color = w.category == WeaponCategory.Melee
                         ? new Color(1f, 0.3f, 0.3f, 0.4f)
                         : new Color(0.3f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, w.effectiveRange);
        }
    }
}