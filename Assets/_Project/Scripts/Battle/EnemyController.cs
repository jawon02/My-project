using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 한 마리를 관리한다: 체력/방어도(Combatant) + 행동 예고 + 행동 실행.
/// 적마다 빈 오브젝트(예: "Enemy_Slime")를 만들어 붙여서 사용한다.
/// 적이 여러 마리면 오브젝트를 여러 개 만들고, CombatManager의 리스트 순서대로
/// (= 화면 왼쪽부터) 행동시킨다.
/// 사망한 적을 리스트에서 빼는 일은 CombatManager가 담당하고,
/// 이 클래스는 "죽은 적은 행동하지 않는다"는 것만 보장한다.
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("적 데이터 (EnemyData 에셋을 연결)")]
    public EnemyData data;

    [Header("현재 상태 (확인용)")]
    public Combatant combatant = new Combatant();   // 체력, 방어도
    public int turnIndex = 0;                       // 지금까지 실행한 행동 횟수 (다음 행동 번호)
    public string nextActionPreview = "";           // 인스펙터에서 보는 행동 예고 문구

    /// <summary>
    /// 같은 전투에 참가 중인 적 목록 (자기 자신 포함, 왼쪽부터). AllyBuff·AllyHeal 대상을 고를 때 쓴다.
    /// CombatManager가 전투 시작 시 "살아 있는 적 리스트"를 그대로 넣어 준다.
    /// 같은 리스트를 함께 쓰므로 적이 쓰러져 리스트에서 빠지면 여기에도 바로 반영된다.
    /// 비어 있으면(null) 아군이 없는 것으로 본다. 씬 파일에는 저장하지 않는다(NonSerialized).
    /// </summary>
    [System.NonSerialized]
    public List<EnemyController> allies = null;

    /// <summary>아군 행동의 대상이 없을 때 예고 문구</summary>
    public const string NoTargetPreview = "행동 없음(대상 없음)";

    /// <summary>적이 죽었는지 (체력 0 이하)</summary>
    public bool IsDead
    {
        get { return combatant.IsDead; }
    }

    /// <summary>
    /// 다음에 실행할 행동. 이것이 곧 "행동 예고"다.
    /// 예고와 실제 실행이 항상 같은 데이터를 쓰므로 둘이 어긋날 수 없다.
    /// 행동 패턴이 비어 있으면 null.
    /// </summary>
    public EnemyActionData NextAction
    {
        get
        {
            if (data == null)
            {
                return null;
            }
            return data.GetAction(turnIndex);
        }
    }

    // ---------------------------------------------------------------
    // 전투 시작
    // ---------------------------------------------------------------

    /// <summary>
    /// 전투 시작 시 호출. 체력을 최대로 채우고, 방어도 0, 행동 순서를 처음으로 되돌린다.
    /// </summary>
    public void InitBattle()
    {
        if (data == null)
        {
            Debug.LogError("[Enemy] EnemyData가 연결되지 않았다: " + gameObject.name);
            return;
        }

        combatant.Init(data.maxHp);   // 체력 가득, 방어도 0, 상태 효과 없음
        combatant.displayName = data.enemyName;   // 로그용 이름 (CombatManager가 "1번 슬라임"처럼 번호를 붙여 덮어쓴다)
        turnIndex = 0;
        RefreshPreview();
        Debug.Log("[Enemy] " + data.enemyName + " 등장 - 체력 " + combatant.currentHp + "/" + combatant.maxHp
                  + " / 행동 예고: " + nextActionPreview);
    }

    // ---------------------------------------------------------------
    // 행동 실행
    // ---------------------------------------------------------------

    /// <summary>
    /// 이 적의 행동을 실행하고, 실행한 행동을 돌려준다.
    /// 순서(확정): ① 이 적의 방어도 초기화 → ② 예고했던 행동 실행(상태 효과 보정 반영)
    ///           → ③ 이 적의 상태 효과 지속시간 1 감소 (단, 이번 행동에 자신에게 건 효과는 제외)
    ///           → ④ 다음 행동으로 넘어감.
    /// 아군 행동(AllyBuff·AllyHeal)은 실행하는 순간에 대상을 다시 고른다. 대상이 없으면 그 행동만 생략하고
    /// ①③④는 평소대로 진행한다. (행동 패턴이 멈추지 않도록)
    /// 따라서 방어 행동으로 얻은 방어도는 다음 플레이어 턴 동안 유지되고,
    /// 이 적의 다음 행동 직전에 사라진다.
    /// playerTarget: 공격 행동이 때릴 대상 (플레이어의 Combatant)
    /// 죽은 적이거나 행동 패턴이 없으면 아무것도 하지 않고 null을 돌려준다.
    /// </summary>
    public EnemyActionData ExecuteAction(Combatant playerTarget)
    {
        // 죽은 적은 행동하지 않는다.
        if (IsDead)
        {
            Debug.Log("[Enemy] " + GetName() + "은(는) 쓰러져서 행동하지 않는다.");
            return null;
        }

        EnemyActionData action = NextAction;
        if (action == null)
        {
            Debug.LogWarning("[Enemy] " + GetName() + "의 행동 패턴이 비어 있다.");
            return null;
        }

        // ① 이 적의 방어도를 먼저 초기화한다. (지난 행동에서 얻은 방어도가 여기서 사라진다)
        combatant.ResetBlock();

        // ② 예고했던 행동을 그대로 실행한다.
        //    실행 수치는 예고와 같은 함수(GetFinalValue)로 계산하므로 예고와 실제 결과가 항상 같다.
        int finalValue = GetFinalValue(action);
        string previewText = GetPreviewText(action);   // 로그용 (효과를 걸기 전의 예고 문구)
        StatusEffectType skipTick = StatusEffectType.None;   // ③에서 이번에는 줄이지 않을 효과 (SelfBuff용)
        switch (action.type)
        {
            case EnemyActionType.Attack:
                if (playerTarget == null)
                {
                    Debug.LogWarning("[Enemy] 공격 대상이 없다.");
                }
                else
                {
                    int hpLost = playerTarget.TakeDamage(finalValue);
                    Debug.Log("[Enemy] " + GetName() + " " + GetPreviewText(action)
                              + " → 플레이어 체력 -" + hpLost + " (남은 체력 " + playerTarget.currentHp
                              + ", 남은 방어도 " + playerTarget.block + ")");
                }
                break;

            case EnemyActionType.Defend:
                combatant.GainBlock(finalValue);
                Debug.Log("[Enemy] " + GetName() + " " + GetPreviewText(action) + " → 현재 방어도 " + combatant.block);
                break;

            case EnemyActionType.SelfBuff:
                Debug.Log("[Enemy] " + GetName() + " " + previewText);
                // 새로 걸었거나 지속시간을 갱신했으면, 이 효과는 이번 행동이 끝날 때 줄이지 않는다.
                if (combatant.ApplyStatus(action.statusEffect, action.value, action.duration))
                {
                    skipTick = action.statusEffect;
                }
                break;

            case EnemyActionType.AllyBuff:
                {
                    EnemyController buffTarget = FindAllyBuffTarget();   // 실행 순간에 다시 고른다
                    if (buffTarget == null)
                    {
                        Debug.Log("[Enemy] " + GetName() + " 아군 강화 → " + NoTargetPreview + " - 행동 생략");
                    }
                    else
                    {
                        Debug.Log("[Enemy] " + GetName() + " " + previewText);
                        // 대상의 지속시간은 대상 자신의 행동이 끝날 때 줄어든다. (기존 규칙 그대로)
                        buffTarget.combatant.ApplyStatus(action.statusEffect, action.value, action.duration);
                    }
                }
                break;

            case EnemyActionType.AllyHeal:
                {
                    EnemyController healTarget = FindAllyHealTarget();   // 실행 순간에 다시 고른다
                    if (healTarget == null)
                    {
                        Debug.Log("[Enemy] " + GetName() + " 아군 회복 → " + NoTargetPreview + " - 행동 생략");
                    }
                    else
                    {
                        int hpBefore = healTarget.combatant.currentHp;
                        healTarget.combatant.Heal(action.value);   // 최대 체력을 넘지 않는다
                        Debug.Log("[Enemy] " + GetName() + " " + previewText
                                  + " → 실제 회복 " + (healTarget.combatant.currentHp - hpBefore)
                                  + " (체력 " + healTarget.combatant.currentHp + "/" + healTarget.combatant.maxHp + ")");
                    }
                }
                break;
        }

        // ③ 이 적에게 걸린 상태 효과의 지속시간을 1 줄인다. (0이 되면 만료)
        //    이번 행동에 자기 자신에게 건 효과(skipTick)만 이번에는 줄이지 않는다.
        combatant.TickStatusEffects(skipTick);

        // ④ 다음 행동으로 넘어가고, 행동 예고를 갱신한다. (만료된 효과도 여기서 예고에 반영된다)
        turnIndex++;
        RefreshPreview();

        return action;
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    /// <summary>
    /// 행동 예고 문구를 갱신한다. (인스펙터 표시용)
    /// 상태 효과가 바뀌면(카드로 효과가 걸리거나 만료되면) 다시 호출해서 예고를 새로 계산한다.
    /// </summary>
    public void RefreshPreview()
    {
        EnemyActionData next = NextAction;
        if (next == null)
        {
            nextActionPreview = "(행동 없음)";
        }
        else
        {
            nextActionPreview = GetPreviewText(next);
        }
    }

    /// <summary>
    /// 행동의 최종 수치 = 기본 수치에 이 적의 상태 효과 보정을 더한 값 (최소 0).
    /// 공격 행동은 공격력 보정, 방어 행동은 방어도 보정을 쓴다.
    /// 예고와 실제 실행이 모두 이 함수를 쓴다.
    /// </summary>
    public int GetFinalValue(EnemyActionData action)
    {
        switch (action.type)
        {
            case EnemyActionType.Attack: return combatant.GetFinalAttack(action.value);
            case EnemyActionType.Defend: return combatant.GetFinalBlock(action.value);
            default: return action.value;
        }
    }

    /// <summary>
    /// AllyBuff 대상: 자기 자신을 제외한 살아 있는 아군 중 가장 왼쪽. 없으면 null.
    /// 예고와 실제 실행이 모두 이 함수를 쓴다.
    /// </summary>
    public EnemyController FindAllyBuffTarget()
    {
        if (allies == null)
        {
            return null;
        }
        foreach (EnemyController ally in allies)   // 리스트 순서 = 왼쪽부터
        {
            if (ally == null || ally == this || ally.IsDead)
            {
                continue;
            }
            return ally;
        }
        return null;
    }

    /// <summary>
    /// AllyHeal 대상: 자기 자신을 제외한 살아 있는 아군 중 체력이 줄어든(최대 체력 미만) 가장 왼쪽. 없으면 null.
    /// 체력이 가득 찬 아군은 고르지 않는다. 예고와 실제 실행이 모두 이 함수를 쓴다.
    /// </summary>
    public EnemyController FindAllyHealTarget()
    {
        if (allies == null)
        {
            return null;
        }
        foreach (EnemyController ally in allies)
        {
            if (ally == null || ally == this || ally.IsDead)
            {
                continue;
            }
            if (ally.combatant.currentHp < ally.combatant.maxHp)
            {
                return ally;
            }
        }
        return null;
    }

    /// <summary>
    /// 보정을 반영한 예고 문구.
    /// 보정이 없으면 "공격 7" (기존과 같음)
    /// 보정이 있으면 "공격 5 (기본 7, 공격력 감소 -2)"
    /// 강화·회복 행동 (Phase 4-B):
    ///   "자기 강화: 공격력 +2 (2턴)" / "아군 강화: 2번 슬라임 공격력 +2 (2턴)" / "아군 회복: 2번 슬라임 +6"
    ///   아군 대상이 없으면 "행동 없음(대상 없음)"
    /// </summary>
    private string GetPreviewText(EnemyActionData action)
    {
        switch (action.type)
        {
            case EnemyActionType.SelfBuff:
                return "자기 강화: " + StatusEffect.GetStatText(action.statusEffect, action.value)
                       + " (" + action.duration + "턴)";

            case EnemyActionType.AllyBuff:
                {
                    EnemyController target = FindAllyBuffTarget();
                    if (target == null)
                    {
                        return NoTargetPreview;
                    }
                    return "아군 강화: " + target.GetLabel() + " "
                           + StatusEffect.GetStatText(action.statusEffect, action.value) + " (" + action.duration + "턴)";
                }

            case EnemyActionType.AllyHeal:
                {
                    EnemyController target = FindAllyHealTarget();
                    if (target == null)
                    {
                        return NoTargetPreview;
                    }
                    return "아군 회복: " + target.GetLabel() + " +" + action.value;
                }
        }

        bool isAttack = action.type == EnemyActionType.Attack;
        string modifierText = "";
        if (action.type == EnemyActionType.Attack || action.type == EnemyActionType.Defend)
        {
            modifierText = combatant.GetModifierText(isAttack);
        }

        if (modifierText.Length == 0)
        {
            return action.GetPreviewText();   // 보정이 없으면 원래 문구 그대로
        }

        string label = isAttack ? "공격" : "방어";
        return label + " " + GetFinalValue(action) + " (기본 " + action.value + ", " + modifierText + ")";
    }

    /// <summary>예고에 쓰는 번호 붙은 이름. 예: "2번 슬라임" (CombatManager가 붙여 준 이름이 없으면 적 이름)</summary>
    public string GetLabel()
    {
        if (!string.IsNullOrEmpty(combatant.displayName))
        {
            return combatant.displayName;
        }
        return GetName();
    }

    private string GetName()
    {
        if (data != null)
        {
            return data.enemyName;
        }
        return gameObject.name;
    }

    /// <summary>현재 상태를 콘솔에 출력한다. (테스트용)</summary>
    public void LogState()
    {
        Debug.Log("[Enemy 상태] " + GetName() + " 체력 " + combatant.currentHp + "/" + combatant.maxHp
                  + " / 방어도 " + combatant.block
                  + " / 행동 예고: " + nextActionPreview);
    }
}
