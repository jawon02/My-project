using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어와 적이 함께 쓰는 "체력 + 방어도 + 상태 효과" 계산 클래스.
/// 상태 효과(Phase 4)도 여기에 두므로, 플레이어와 적이 똑같은 규칙으로 계산된다.
/// MonoBehaviour가 아닌 일반 클래스라서 오브젝트에 직접 붙이지 않고,
/// PlayerController / EnemyController 안에 변수로 넣어서 사용한다.
/// [System.Serializable] 덕분에 인스펙터 창에서 값을 바로 볼 수 있다.
/// </summary>
[System.Serializable]
public class Combatant
{
    [Header("이름 (로그 표시용, 컨트롤러가 자동으로 채운다)")]
    public string displayName = "";   // 예: "플레이어", "1번 슬라임"

    [Header("체력")]
    public int maxHp = 70;       // 최대 체력
    public int currentHp = 70;   // 현재 체력

    [Header("방어도")]
    public int block = 0;        // 현재 방어도 (전투 시작 시 0)

    [Header("상태 효과 (확인용)")]
    public List<StatusEffect> statusEffects = new List<StatusEffect>();   // 지금 걸려 있는 버프·디버프

    /// <summary>체력이 0 이하이면 true (죽음)</summary>
    public bool IsDead
    {
        get { return currentHp <= 0; }
    }

    /// <summary>
    /// 전투 시작 전에 호출. 최대 체력을 정하고 체력을 가득 채우며 방어도를 0으로 만든다.
    /// 남아 있는 상태 효과도 모두 지운다. (안전장치)
    /// </summary>
    public void Init(int newMaxHp)
    {
        maxHp = newMaxHp;
        currentHp = newMaxHp;
        block = 0;
        statusEffects.Clear();
    }

    /// <summary>
    /// 피해를 받는다. 규칙: 방어도가 먼저 깎이고, 막지 못한 피해만 체력에 적용된다.
    /// 반환값 = 실제로 체력이 깎인 양 (방어도로 전부 막으면 0)
    /// </summary>
    public int TakeDamage(int amount)
    {
        // 0 이하의 피해는 무시 (회복 효과로 쓰이는 실수 방지)
        if (amount <= 0)
        {
            return 0;
        }

        // 1) 방어도가 흡수할 수 있는 양 = 방어도와 피해 중 작은 쪽
        int absorbed = Mathf.Min(block, amount);
        block -= absorbed;

        // 2) 남은 피해만 체력에 적용
        int hpDamage = amount - absorbed;
        currentHp = Mathf.Max(0, currentHp - hpDamage); // 체력은 0 아래로 내려가지 않음

        return hpDamage;
    }

    /// <summary>방어도를 얻는다. (0 이하의 값은 무시)</summary>
    public void GainBlock(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        block += amount;
    }

    /// <summary>
    /// 방어도를 0으로 만든다.
    /// 플레이어는 "다음 플레이어 턴이 시작될 때" 호출한다. (확정된 규칙)
    /// </summary>
    public void ResetBlock()
    {
        block = 0;
    }

    /// <summary>체력을 회복한다. 최대 체력을 넘을 수 없다.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }

    // ---------------------------------------------------------------
    // 상태 효과 (Phase 4)
    // 규칙(확정): 보정은 "그 효과를 가진 쪽"의 공격·방어에 적용한다.
    //  최종 피해 = Max(0, 기본 피해 + 공격력 보정) / 최종 방어도 = Max(0, 기본 방어도 + 방어도 보정)
    // ---------------------------------------------------------------

    /// <summary>
    /// 상태 효과를 건다.
    /// - 수치나 지속시간이 0 이하이면 무시한다.
    /// - 같은 종류의 효과가 이미 있으면: 기존 수치를 그대로 유지하고, 지속시간만 새 값으로 갱신한다. (중첩하지 않음, Phase 4-B 확정)
    ///   예) 공격력 +2 (1턴) 위에 공격력 +3 (2턴)을 걸면 → 공격력 +2 (2턴)
    /// - 반대 효과(증가/감소)는 다른 종류이므로 따로따로 유지된다.
    /// 효과를 걸었으면(새로 걸거나 갱신) true.
    /// </summary>
    public bool ApplyStatus(StatusEffectType type, int amount, int duration)
    {
        string ownerName = GetLogName();
        if (type == StatusEffectType.None || amount <= 0 || duration <= 0)
        {
            Debug.Log("[효과] " + ownerName + ": " + StatusEffect.GetDisplayName(type)
                      + " 무시 (수치 " + amount + ", 지속시간 " + duration + " - 0 이하 값은 걸지 않는다)");
            return false;
        }

        StatusEffect existing = FindStatus(type);
        if (existing == null)
        {
            // 처음 거는 효과 → 새로 추가
            StatusEffect effect = new StatusEffect(type, amount, duration);
            statusEffects.Add(effect);
            Debug.Log("[효과] " + ownerName + ": " + effect.ToShortText() + " 적용 (" + duration + "턴)");
        }
        else
        {
            // 같은 효과를 다시 건 경우 → 수치는 기존 값 그대로, 지속시간만 새로 갱신
            existing.turnsLeft = duration;
            Debug.Log("[효과] " + ownerName + ": " + existing.ToShortText() + " 갱신 (기존 수치 유지, " + duration + "턴)");
        }
        return true;
    }

    /// <summary>type 종류의 효과를 찾는다. 없으면 null.</summary>
    public StatusEffect FindStatus(StatusEffectType type)
    {
        foreach (StatusEffect effect in statusEffects)
        {
            if (effect.type == type)
            {
                return effect;
            }
        }
        return null;
    }

    /// <summary>공격력 보정의 합계 (공격력 증가 - 공격력 감소)</summary>
    public int GetAttackModifier()
    {
        return SumModifier(StatusEffectType.AttackUp, StatusEffectType.AttackDown);
    }

    /// <summary>방어도 보정의 합계 (방어도 증가 - 방어도 감소)</summary>
    public int GetDefenseModifier()
    {
        return SumModifier(StatusEffectType.DefenseUp, StatusEffectType.DefenseDown);
    }

    /// <summary>보정을 반영한 최종 피해. 최소 0.</summary>
    public int GetFinalAttack(int baseDamage)
    {
        return Mathf.Max(0, baseDamage + GetAttackModifier());
    }

    /// <summary>보정을 반영한 최종 방어도. 최소 0.</summary>
    public int GetFinalBlock(int baseBlock)
    {
        return Mathf.Max(0, baseBlock + GetDefenseModifier());
    }

    /// <summary>
    /// 공격력(isAttack = true) 또는 방어도(false)에 걸린 효과를 글로 나열한다. (행동 예고용)
    /// 예) "공격력 증가 +2, 공격력 감소 -2" / 관련 효과가 없으면 빈 문자열
    /// </summary>
    public string GetModifierText(bool isAttack)
    {
        StatusEffectType up = isAttack ? StatusEffectType.AttackUp : StatusEffectType.DefenseUp;
        StatusEffectType down = isAttack ? StatusEffectType.AttackDown : StatusEffectType.DefenseDown;

        string text = "";
        foreach (StatusEffect effect in statusEffects)
        {
            if (effect.type == up || effect.type == down)
            {
                text += (text.Length > 0 ? ", " : "") + effect.ToShortText();
            }
        }
        return text;
    }

    /// <summary>
    /// 모든 효과의 지속시간을 1 줄인다. 0이 된 효과는 제거한다(만료).
    /// 플레이어: 플레이어 턴 종료 시 / 적: 그 적의 행동이 끝날 때 호출한다.
    /// </summary>
    public void TickStatusEffects()
    {
        string ownerName = GetLogName();
        // 뒤에서부터 돌면 중간에 지워도 순서가 꼬이지 않는다.
        for (int i = statusEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = statusEffects[i];
            effect.turnsLeft--;
            if (effect.turnsLeft <= 0)
            {
                statusEffects.RemoveAt(i);
                Debug.Log("[효과] " + ownerName + ": " + effect.ToShortText() + " 만료");
            }
        }
    }

    /// <summary>모든 상태 효과를 지운다. (전투 시작·종료 시)</summary>
    public void ClearStatusEffects()
    {
        statusEffects.Clear();
    }

    /// <summary>로그에 쓸 이름. 이름이 비어 있으면 "(이름 없음)"</summary>
    private string GetLogName()
    {
        return string.IsNullOrEmpty(displayName) ? "(이름 없음)" : displayName;
    }

    /// <summary>증가 효과 수치 - 감소 효과 수치</summary>
    private int SumModifier(StatusEffectType up, StatusEffectType down)
    {
        int sum = 0;
        foreach (StatusEffect effect in statusEffects)
        {
            if (effect.type == up || effect.type == down)
            {
                sum += effect.SignedAmount;
            }
        }
        return sum;
    }
}
