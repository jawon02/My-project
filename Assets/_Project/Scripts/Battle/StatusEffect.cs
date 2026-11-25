/// <summary>
/// 상태 효과(버프·디버프) 종류 (Phase 4 확정).
/// None은 "효과 없음"이다. 카드 데이터의 기본값으로 쓰여서, 실수로 다른 효과가 걸리는 것을 막는다.
/// </summary>
public enum StatusEffectType
{
    None,          // 효과 없음 (Buff 카드가 아닌 카드의 기본값)
    AttackUp,      // 공격력 증가: 이 효과를 가진 쪽의 공격 피해 +수치
    AttackDown,    // 공격력 감소: 이 효과를 가진 쪽의 공격 피해 -수치
    DefenseUp,     // 방어도 증가: 이 효과를 가진 쪽이 얻는 방어도 +수치
    DefenseDown    // 방어도 감소: 이 효과를 가진 쪽이 얻는 방어도 -수치
}

/// <summary>
/// 지금 걸려 있는 상태 효과 하나 = 종류 + 수치 + 남은 지속시간.
/// Combatant의 statusEffects 리스트에 들어가며,
/// [System.Serializable] 덕분에 인스펙터에서 현재 효과 목록을 눈으로 확인할 수 있다.
/// </summary>
[System.Serializable]
public class StatusEffect
{
    public StatusEffectType type = StatusEffectType.None;
    public int amount = 0;      // 보정 수치. 항상 양수로 저장한다. (증가인지 감소인지는 type이 정한다)
    public int turnsLeft = 0;   // 남은 지속시간 (0이 되면 제거된다)

    /// <summary>인스펙터 직렬화용 기본 생성자 (직접 호출하지 않는다)</summary>
    public StatusEffect()
    {
    }

    public StatusEffect(StatusEffectType type, int amount, int turnsLeft)
    {
        this.type = type;
        this.amount = amount;
        this.turnsLeft = turnsLeft;
    }

    /// <summary>
    /// 최종 계산에 더할 값. 증가 효과는 +수치, 감소 효과는 -수치.
    /// 예) 공격력 감소 2 → -2
    /// </summary>
    public int SignedAmount
    {
        get { return IsDecrease(type) ? -amount : amount; }
    }

    /// <summary>감소(디버프) 효과인지</summary>
    public static bool IsDecrease(StatusEffectType type)
    {
        return type == StatusEffectType.AttackDown || type == StatusEffectType.DefenseDown;
    }

    /// <summary>로그와 행동 예고에 쓰는 효과 이름. 예: "공격력 감소"</summary>
    public static string GetDisplayName(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.AttackUp: return "공격력 증가";
            case StatusEffectType.AttackDown: return "공격력 감소";
            case StatusEffectType.DefenseUp: return "방어도 증가";
            case StatusEffectType.DefenseDown: return "방어도 감소";
            default: return "효과 없음";
        }
    }

    /// <summary>
    /// 적 강화 행동 예고용: "공격력 +2", "방어도 -2"처럼 능력치 이름과 부호 붙은 수치. (Phase 4-B)
    /// </summary>
    public static string GetStatText(StatusEffectType type, int amount)
    {
        string stat;
        switch (type)
        {
            case StatusEffectType.AttackUp:
            case StatusEffectType.AttackDown:
                stat = "공격력";
                break;
            case StatusEffectType.DefenseUp:
            case StatusEffectType.DefenseDown:
                stat = "방어도";
                break;
            default:
                return "효과 없음";
        }
        return stat + " " + (IsDecrease(type) ? "-" : "+") + amount;
    }

    /// <summary>"공격력 감소 -2"처럼 이름과 부호 붙은 수치를 함께 돌려준다.</summary>
    public string ToShortText()
    {
        int signed = SignedAmount;
        return GetDisplayName(type) + " " + (signed >= 0 ? "+" : "") + signed;
    }
}
