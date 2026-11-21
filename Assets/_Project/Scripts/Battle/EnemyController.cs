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

        combatant.Init(data.maxHp);   // 체력 가득, 방어도 0
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
    /// 순서(확정): ① 이 적의 방어도 초기화 → ② 예고했던 행동 실행 → ③ 다음 행동으로 넘어감.
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
        switch (action.type)
        {
            case EnemyActionType.Attack:
                if (playerTarget == null)
                {
                    Debug.LogWarning("[Enemy] 공격 대상이 없다.");
                }
                else
                {
                    int hpLost = playerTarget.TakeDamage(action.value);
                    Debug.Log("[Enemy] " + GetName() + " 공격 " + action.value
                              + " → 플레이어 체력 -" + hpLost + " (남은 체력 " + playerTarget.currentHp
                              + ", 남은 방어도 " + playerTarget.block + ")");
                }
                break;

            case EnemyActionType.Defend:
                combatant.GainBlock(action.value);
                Debug.Log("[Enemy] " + GetName() + " 방어도 +" + action.value + " → 현재 방어도 " + combatant.block);
                break;
        }

        // ③ 다음 행동으로 넘어가고, 행동 예고를 갱신한다.
        turnIndex++;
        RefreshPreview();

        return action;
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    /// <summary>행동 예고 문구를 갱신한다. (인스펙터 표시용)</summary>
    private void RefreshPreview()
    {
        EnemyActionData next = NextAction;
        if (next == null)
        {
            nextActionPreview = "(행동 없음)";
        }
        else
        {
            nextActionPreview = next.GetPreviewText();
        }
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
