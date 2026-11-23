using UnityEngine;

/// <summary>
/// 플레이어 관리자: 체력/방어도(Combatant) + 에너지 + 카드 사용 처리.
/// 빈 오브젝트(예: "Player")에 붙여서 사용한다.
/// 턴 진행 순서(드로우 포함)는 나중에 만들 CombatManager가 담당하고,
/// 이 클래스는 "플레이어 쪽 상태 변화"만 책임진다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("기본 설정 (명세 초기값)")]
    public int startMaxHp = 70;    // 런 시작 시 최대 체력
    public int maxEnergy = 3;      // 기본 최대 에너지

    [Header("연결")]
    public CardManager cardManager;   // 손패를 가진 CardManager를 인스펙터에서 연결

    [Header("현재 상태 (확인용)")]
    public Combatant combatant = new Combatant();   // 체력, 방어도
    public int energy = 0;                          // 현재 에너지

    /// <summary>플레이어가 죽었는지 (체력 0 이하)</summary>
    public bool IsDead
    {
        get { return combatant.IsDead; }
    }

    // ---------------------------------------------------------------
    // 런 / 전투 / 턴 시작과 종료
    // ---------------------------------------------------------------

    /// <summary>
    /// 런(한 판 전체)이 시작될 때 딱 한 번 호출. 체력을 최대로 채운다.
    /// 전투가 끝나도 체력은 자동으로 회복되지 않으므로, 전투마다 호출하면 안 된다.
    /// </summary>
    public void InitRun()
    {
        combatant.Init(startMaxHp);
        energy = 0;
        Debug.Log("[Player] 런 시작 - 체력 " + combatant.currentHp + "/" + combatant.maxHp);
    }

    /// <summary>전투 시작 시 호출. 방어도를 0으로 만든다. (체력은 유지)</summary>
    public void StartBattle()
    {
        combatant.ResetBlock();
        energy = 0;
    }

    /// <summary>
    /// 플레이어 턴 시작 시 호출. 순서(확정): 방어도 초기화 → 에너지 최대치까지 회복.
    /// (카드 5장 드로우는 CombatManager가 이 다음에 호출한다.)
    /// </summary>
    public void StartTurn()
    {
        combatant.ResetBlock();   // 지난 턴에 얻은 방어도는 여기서 사라진다
        energy = maxEnergy;       // 에너지 최대치까지 회복
        Debug.Log("[Player] 턴 시작 - 방어도 초기화, 에너지 " + energy + "/" + maxEnergy);
    }

    /// <summary>플레이어 턴 종료 시 호출. 남은 에너지는 소멸한다.</summary>
    public void EndTurn()
    {
        Debug.Log("[Player] 턴 종료 - 남은 에너지 " + energy + " 소멸");
        energy = 0;
    }

    /// <summary>
    /// 최대 에너지를 올린다. (카드/유물 효과용)
    /// 지금은 최대치만 올라가고, 늘어난 에너지는 다음 턴 시작부터 적용된다.
    /// </summary>
    public void IncreaseMaxEnergy(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        maxEnergy += amount;
    }

    // ---------------------------------------------------------------
    // 에너지 사용 (카드와 스킬이 똑같이 사용 - 스킬은 나중에 이 함수를 재사용)
    // ---------------------------------------------------------------

    /// <summary>현재 에너지로 cost를 낼 수 있으면 true</summary>
    public bool CanAfford(int cost)
    {
        return energy >= cost;
    }

    /// <summary>에너지가 충분하면 cost만큼 차감하고 true, 부족하면 아무것도 안 하고 false</summary>
    public bool TrySpendEnergy(int cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }
        energy -= cost;
        return true;
    }

    // ---------------------------------------------------------------
    // 카드 사용
    // ---------------------------------------------------------------

    /// <summary>
    /// 손패의 handIndex번째 카드를 사용한다. 성공하면 true, 실패하면 false.
    /// 순서(확정): 에너지 확인 및 지불 → 효과 적용 → 버림 더미로 이동.
    /// target: 공격 카드가 때릴 대상의 Combatant (적). 방어 카드에는 필요 없다.
    /// 실패하면(에너지 부족, 잘못된 번호, 효과 미구현 카드 등) 에너지도 카드도 그대로다.
    /// </summary>
    public bool TryPlayCard(int handIndex, Combatant target)
    {
        if (cardManager == null)
        {
            Debug.LogError("[Player] CardManager가 연결되지 않았다.");
            return false;
        }

        // 1) 손패에 그 카드가 있는지 확인
        if (handIndex < 0 || handIndex >= cardManager.hand.Count)
        {
            Debug.LogWarning("[Player] 손패에 " + handIndex + "번 카드가 없다.");
            return false;
        }
        CardInstance card = cardManager.hand[handIndex];   // 손패의 카드 한 장 (강화 상태 포함)

        // 2) 효과가 아직 구현되지 않은 카드(Buff, Special)는 사용할 수 없다.
        //    에너지를 쓰기 전에 막으므로 에너지도 그대로, 카드도 손패에 그대로 남는다.
        if (card.Type == CardType.Buff || card.Type == CardType.Special)
        {
            Debug.Log("[Player] '" + card.Name + "': 아직 구현되지 않은 카드 효과입니다.");
            return false;
        }

        // 3) 공격 카드인데 대상이 없으면 사용할 수 없다. (에너지를 쓰기 전에 막는다)
        if (card.Type == CardType.Attack && target == null)
        {
            Debug.LogWarning("[Player] 공격 카드는 대상이 필요하다.");
            return false;
        }

        // 4) 에너지 확인 및 지불
        if (!TrySpendEnergy(card.Cost))
        {
            Debug.Log("[Player] 에너지 부족 - '" + card.Name + "' (비용 " + card.Cost + ") / 현재 에너지 " + energy);
            return false;
        }

        // 5) 효과 적용
        ApplyCardEffect(card, target);

        // 6) 사용한 카드를 버림 더미로 이동 (소멸 없음)
        cardManager.DiscardFromHand(handIndex);

        Debug.Log("[Player] 카드 사용: " + card.Name + " (비용 " + card.Cost + ") → 에너지 " + energy + "/" + maxEnergy);
        return true;
    }

    /// <summary>카드 종류에 따라 효과를 적용한다.</summary>
    private void ApplyCardEffect(CardInstance card, Combatant target)
    {
        switch (card.Type)
        {
            case CardType.Attack:
                // 대상의 방어도가 먼저 깎이고, 막지 못한 피해만 체력에 들어간다. (Combatant가 처리)
                int hpLost = target.TakeDamage(card.Value);   // 강화된 카드면 강화 수치가 적용된다
                Debug.Log("[Player] 공격 " + card.Value + " → 적 체력 -" + hpLost + " (남은 체력 " + target.currentHp + ", 남은 방어도 " + target.block + ")");
                break;

            case CardType.Defend:
                combatant.GainBlock(card.Value);   // 강화된 카드면 강화 수치가 적용된다
                Debug.Log("[Player] 방어도 +" + card.Value + " → 현재 방어도 " + combatant.block);
                break;

            case CardType.Buff:
            case CardType.Special:
                // 이곳에는 도달하지 않는다. 미구현 카드는 TryPlayCard에서 미리 막는다.
                // 효과를 구현할 때 여기에 코드를 추가하고, TryPlayCard의 제한 조건도 함께 풀어준다.
                break;
        }
    }

    // ---------------------------------------------------------------
    // 디버그
    // ---------------------------------------------------------------

    /// <summary>현재 상태를 콘솔에 출력한다. (테스트용)</summary>
    public void LogState()
    {
        Debug.Log("[Player 상태] 체력 " + combatant.currentHp + "/" + combatant.maxHp
                  + " / 방어도 " + combatant.block
                  + " / 에너지 " + energy + "/" + maxEnergy);
    }
}
