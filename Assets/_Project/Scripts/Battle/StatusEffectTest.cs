using UnityEngine;

/// <summary>
/// Phase 4-A (상태 효과 + Buff 카드 + 재정렬) 테스트용 스크립트. CombatManager와 같은 오브젝트에 붙인다.
/// 컴포넌트 우클릭 → Run Auto Test 로 실행한다.
///
/// 전제: CombatManager의 Enemies 리스트에 적 2마리(슬라임: 체력 40, 공격 7 → 방어 5 → 공격 10)가 들어 있다.
///       플레이어 최대 체력 70, 최대 에너지 3, 시작 덱 10장. (기존 테스트와 같은 구성)
/// 테스트에 쓰는 카드(집중, 약화, 견고, 균열, 재정렬 등)는 이 스크립트가 임시로 만들고 끝나면 지운다.
/// 그래서 카드 에셋을 따로 만들거나 연결하지 않아도 된다. (시작 덱도 건드리지 않는다)
/// 수치는 테스트용 임시 수치이며 최종 밸런스가 아니다.
/// </summary>
public class StatusEffectTest : MonoBehaviour
{
    [Header("연결")]
    public CombatManager combat;

    private int passCount = 0;
    private int failCount = 0;

    // 테스트 중에만 쓰는 임시 카드 원본들
    private CardData focus;        // 집중: Buff / Self / 공격력 증가 2 (강화 3) / 2턴
    private CardData weaken;       // 약화: Buff / SingleEnemy / 공격력 감소 2 (강화 3) / 2턴
    private CardData sturdy;       // 견고: Buff / Self / 방어도 증가 2 (강화 3) / 2턴
    private CardData crack;        // 균열: Buff / SingleEnemy / 방어도 감소 2 (강화 3) / 2턴
    private CardData reorder;      // 재정렬: Special / Self / 드로우 2 (강화 3)
    private CardData noneBuff;     // 잘못된 데이터: Buff인데 효과가 None
    private CardData zeroBuff;     // 지속시간이 0인 Buff (효과는 무시되고 카드 사용은 성공)
    private CardData single;       // 단일 공격 6 (타격과 같은 수치)
    private CardData sweep;        // 전체 공격 4 (휩쓸기)
    private CardData randomShot;   // 무작위 공격 7 (난사)
    private CardData guardSelf;    // 방어 5 (수비와 같은 수치)

    // ---------------------------------------------------------------
    // 자동 테스트
    // ---------------------------------------------------------------
    [ContextMenu("Run Auto Test")]
    public void RunAutoTest()
    {
        if (!FindReferences())
        {
            return;
        }

        passCount = 0;
        failCount = 0;
        Debug.Log("===== Phase 4-A 상태 효과 자동 테스트 시작 =====");
        CreateTempCards();

        CombatManager c = combat;
        PlayerController p = c.player;
        EnemyController enemyA = c.enemies[0];   // 1번 적 (왼쪽, 먼저 행동)
        EnemyController enemyB = c.enemies[1];   // 2번 적 (오른쪽)

        // ===== A. Combatant 단위: 효과 규칙 =====
        Combatant unit = new Combatant();
        unit.Init(50);
        unit.displayName = "테스트 대상";
        CheckBool("A. 효과 적용 성공", unit.ApplyStatus(StatusEffectType.AttackUp, 2, 2), true);
        Check("A. 공격력 보정 +2", unit.GetAttackModifier(), 2);
        Check("A. 최종 피해 6+2=8", unit.GetFinalAttack(6), 8);
        unit.TickStatusEffects();   // 지속시간 2 → 1
        Check("A. 지속시간 1 감소 → 1", Turns(unit, StatusEffectType.AttackUp), 1);
        unit.ApplyStatus(StatusEffectType.AttackUp, 1, 2);   // 더 작은 수치로 다시 걸기
        Check("A. 재적용(작은 수치) - 수치는 큰 쪽 2 유지", Amount(unit, StatusEffectType.AttackUp), 2);
        Check("A. 재적용 - 지속시간 2로 갱신", Turns(unit, StatusEffectType.AttackUp), 2);
        Check("A. 재적용 - 효과는 1개 (중첩 없음)", unit.statusEffects.Count, 1);
        unit.ApplyStatus(StatusEffectType.AttackUp, 3, 2);   // 더 큰 수치로 다시 걸기
        Check("A. 재적용(큰 수치) - 수치 3으로", Amount(unit, StatusEffectType.AttackUp), 3);
        unit.ApplyStatus(StatusEffectType.AttackDown, 2, 2);   // 반대 효과
        Check("A. 반대 효과는 따로 유지 - 효과 2개", unit.statusEffects.Count, 2);
        Check("A. 반대 효과 합산 - 보정 3-2=+1", unit.GetAttackModifier(), 1);
        unit.ApplyStatus(StatusEffectType.AttackDown, 10, 2);
        Check("A. 최종 피해는 최소 0 (6+3-10)", unit.GetFinalAttack(6), 0);
        unit.ApplyStatus(StatusEffectType.DefenseUp, 2, 2);
        Check("A. 최종 방어도 5+2=7", unit.GetFinalBlock(5), 7);
        Check("A. 방어도 효과는 공격력에 영향 없음 (보정 그대로 -7)", unit.GetAttackModifier(), -7);
        CheckBool("A. 수치 0인 효과는 무시", unit.ApplyStatus(StatusEffectType.DefenseDown, 0, 2), false);
        CheckBool("A. 지속시간 0인 효과는 무시", unit.ApplyStatus(StatusEffectType.DefenseDown, 2, 0), false);
        CheckBool("A. 무시된 효과는 목록에 없음", unit.FindStatus(StatusEffectType.DefenseDown) == null, true);
        unit.ClearStatusEffects();
        Check("A. 전부 제거", unit.statusEffects.Count, 0);
        unit.ApplyStatus(StatusEffectType.AttackUp, 2, 2);
        unit.TickStatusEffects();
        unit.TickStatusEffects();   // 2 → 1 → 0 (만료)
        CheckBool("A. 지속시간 0 - 만료되어 제거", unit.FindStatus(StatusEffectType.AttackUp) == null, true);
        unit.ApplyStatus(StatusEffectType.AttackUp, 2, 2);
        unit.Init(50);
        Check("A. Init - 효과도 초기화", unit.statusEffects.Count, 0);

        // ===== B. 집중 (플레이어 공격력 +2, 2턴) =====
        Prepare(focus, sweep, randomShot);
        CheckBool("B. 집중 사용 성공", c.PlayCard(0), true);
        Check("B. 집중 - 에너지 3→2", p.energy, 2);
        Check("B. 집중 - 플레이어 공격력 증가 +2", Amount(p.combatant, StatusEffectType.AttackUp), 2);
        Check("B. 집중 - 지속시간 2", Turns(p.combatant, StatusEffectType.AttackUp), 2);
        Check("B. 집중 - 버림 더미로 이동", c.cardManager.discardPile.Count, 1);
        c.PlayCard(0);   // 휩쓸기 4+2 = 6, 적마다 따로
        Check("B. 전체 공격 - 1번 적 40→34 (4+2)", enemyA.combatant.currentHp, 34);
        Check("B. 전체 공격 - 2번 적 40→34 (4+2)", enemyB.combatant.currentHp, 34);
        c.PlayCard(0);   // 난사 7+2 = 9, 무작위 1명
        Check("B. 무작위 공격 - 맞은 적에게 9 (두 적 체력 합 68→59)",
              enemyA.combatant.currentHp + enemyB.combatant.currentHp, 59);
        c.EndPlayerTurn();   // 플레이어 턴 종료 → 지속시간 2 → 1
        Check("B. 1턴 종료 후 지속시간 1", Turns(p.combatant, StatusEffectType.AttackUp), 1);
        SetHand(single);
        int hpBefore = enemyA.combatant.currentHp;
        c.SelectEnemy(0);
        c.PlayCard(0);   // 2턴째에도 적용: 6+2 = 8
        Check("B. 2턴째에도 적용 - 단일 공격 8", hpBefore - enemyA.combatant.currentHp, 8);
        c.EndPlayerTurn();   // 지속시간 1 → 0 → 만료
        CheckBool("B. 2턴 종료 후 만료", p.combatant.FindStatus(StatusEffectType.AttackUp) == null, true);
        Check("B. 만료 후 피해는 다시 6", p.combatant.GetFinalAttack(6), 6);

        // ===== C. 견고 (플레이어 방어도 +2) / 강화 =====
        Prepare(sturdy, guardSelf);
        c.PlayCard(0);   // 견고
        c.PlayCard(0);   // 수비 5+2
        Check("C. 견고 후 수비 - 방어도 7", p.combatant.block, 7);
        Check("C. 견고 - 지속시간 2", Turns(p.combatant, StatusEffectType.DefenseUp), 2);

        Prepare();
        AddUpgraded(sturdy);
        c.cardManager.hand.Add(new CardInstance(guardSelf));
        Check("C. 견고+ 카드 수치 3", c.cardManager.hand[0].Value, 3);
        Check("C. 견고+ 카드 지속시간은 2 그대로", c.cardManager.hand[0].Duration, 2);
        c.PlayCard(0);
        Check("C. 견고+ - 방어도 증가 +3", Amount(p.combatant, StatusEffectType.DefenseUp), 3);
        Check("C. 견고+ - 지속시간 2 (강화해도 그대로)", Turns(p.combatant, StatusEffectType.DefenseUp), 2);
        c.PlayCard(0);   // 수비 5+3
        Check("C. 견고+ 후 수비 - 방어도 8", p.combatant.block, 8);

        // ===== D. 약화 (적 공격력 -2) / 예고 반영 / 만료 =====
        Prepare(weaken);
        c.SelectEnemy(0);
        CheckBool("D. 약화 사용 성공", c.PlayCard(0), true);
        Check("D. 약화 - 1번 적 공격력 감소 2", Amount(enemyA.combatant, StatusEffectType.AttackDown), 2);
        Check("D. 약화 - 2번 적에게는 효과 없음", enemyB.combatant.statusEffects.Count, 0);
        Check("D. 약화 - 플레이어에게는 효과 없음", p.combatant.statusEffects.Count, 0);
        CheckText("D. 약화 - 1번 적 예고 재계산", enemyA.nextActionPreview, "공격 5 (기본 7, 공격력 감소 -2)");
        CheckText("D. 약화 - 2번 적 예고 그대로", enemyB.nextActionPreview, "공격 7");
        c.EndPlayerTurn();   // 1번 적 공격 5 (예고대로), 2번 적 공격 7
        Check("D. 예고와 실제 일치 - 플레이어 체력 70-5-7=58", p.combatant.currentHp, 58);
        Check("D. 1번 적 행동 후 지속시간 1", Turns(enemyA.combatant, StatusEffectType.AttackDown), 1);
        CheckText("D. 방어 행동 예고에는 공격력 감소가 붙지 않음", enemyA.nextActionPreview, "방어 5");
        c.EndPlayerTurn();   // 두 적 모두 방어 5. 1번 적의 약화는 행동 2번 뒤 만료
        Check("D. 공격력 감소는 방어도에 영향 없음 - 1번 적 방어도 5", enemyA.combatant.block, 5);
        CheckBool("D. 1번 적 행동 2번 뒤 만료", enemyA.combatant.FindStatus(StatusEffectType.AttackDown) == null, true);
        CheckText("D. 만료 후 예고 재계산", enemyA.nextActionPreview, "공격 10");

        // 강화된 약화(3) 다음에 일반 약화(2)를 다시 걸면 큰 수치 3 유지
        Prepare();
        AddUpgraded(weaken);
        c.cardManager.hand.Add(new CardInstance(weaken));
        c.SelectEnemy(1);   // 이번에는 2번 적에게
        c.PlayCard(0);
        c.PlayCard(0);
        Check("D. 약화+ 후 약화 - 2번 적 수치는 큰 쪽 3 유지", Amount(enemyB.combatant, StatusEffectType.AttackDown), 3);
        Check("D. 약화+ 후 약화 - 지속시간 2로 갱신", Turns(enemyB.combatant, StatusEffectType.AttackDown), 2);
        CheckText("D. 약화+ - 2번 적 예고", enemyB.nextActionPreview, "공격 4 (기본 7, 공격력 감소 -3)");
        Check("D. 약화+ - 1번 적에게는 효과 없음", enemyA.combatant.statusEffects.Count, 0);

        // ===== E. 균열 (적 방어도 -2) =====
        Prepare();
        c.EndPlayerTurn();   // 적 둘 다 공격 7 → 다음 행동은 방어 5
        SetHand(crack);
        c.SelectEnemy(0);
        c.PlayCard(0);
        CheckText("E. 균열 - 1번 적 예고 재계산", enemyA.nextActionPreview, "방어 3 (기본 5, 방어도 감소 -2)");
        c.EndPlayerTurn();
        Check("E. 균열 - 1번 적 방어 5-2=3", enemyA.combatant.block, 3);
        Check("E. 균열 - 2번 적 방어 5 그대로", enemyB.combatant.block, 5);
        CheckText("E. 공격 행동 예고에는 방어도 감소가 붙지 않음", enemyA.nextActionPreview, "공격 10");

        // ===== F. 사용 실패 / 0 이하 값 =====
        Prepare(noneBuff);
        CheckBool("F. 효과 None인 Buff - 사용 실패", c.PlayCard(0), false);
        Check("F. 실패 - 에너지 그대로 3", p.energy, 3);
        Check("F. 실패 - 손패 그대로 1장", c.cardManager.hand.Count, 1);
        Check("F. 실패 - 버림 더미 그대로 0장", c.cardManager.discardPile.Count, 0);
        Prepare(zeroBuff);
        CheckBool("F. 지속시간 0인 Buff - 카드 사용은 성공", c.PlayCard(0), true);
        Check("F. 지속시간 0 - 효과는 걸리지 않음", p.combatant.statusEffects.Count, 0);

        // ===== G. 재정렬 (드로우 2, 강화 3) =====
        Prepare(reorder);   // 전투 시작 드로우 후 뽑기 더미 5장
        CheckBool("G. 재정렬 사용 성공", c.PlayCard(0), true);
        Check("G. 재정렬 - 에너지 3→2", p.energy, 2);
        Check("G. 재정렬 - 손패 2장 (1장 사용, 2장 드로우)", c.cardManager.hand.Count, 2);
        Check("G. 재정렬 - 뽑기 더미 5→3", c.cardManager.drawPile.Count, 3);
        Check("G. 재정렬 - 버림 더미로 이동", c.cardManager.discardPile.Count, 1);

        Prepare();
        AddUpgraded(reorder);
        c.PlayCard(0);
        Check("G. 재정렬+ - 손패 3장 (3장 드로우)", c.cardManager.hand.Count, 3);
        Check("G. 재정렬+ - 뽑기 더미 5→2", c.cardManager.drawPile.Count, 2);

        // 손패 10장 (재정렬 포함)에서 사용: 확정 순서(효과 → 버림)대로 재정렬이 손패에 있는 채로 드로우하므로
        // 뽑은 2장은 손패에 못 들어가고 버림 더미로 간다.
        Prepare(reorder, single, single, single, single, single, single, single, single, single);
        c.PlayCard(0);
        Check("G. 손패 가득 - 손패 10→9 (재정렬만 빠짐)", c.cardManager.hand.Count, 9);
        Check("G. 손패 가득 - 뽑기 더미 5→3", c.cardManager.drawPile.Count, 3);
        Check("G. 손패 가득 - 버림 더미 3장 (넘친 2장 + 재정렬)", c.cardManager.discardPile.Count, 3);

        // ===== H. 전투 종료 / 새 전투 시작 시 효과 제거 =====
        Prepare(focus, weaken, sweep);
        c.SelectEnemy(0);
        c.PlayCard(0);   // 집중 → 플레이어
        c.PlayCard(0);   // 약화 → 1번 적
        enemyA.combatant.currentHp = 5;
        enemyB.combatant.currentHp = 5;
        c.PlayCard(0);   // 휩쓸기 4+2 → 둘 다 처치 → 승리
        CheckBool("H. 승리", c.state == BattleState.Victory, true);
        Check("H. 승리 - 플레이어 효과 제거", p.combatant.statusEffects.Count, 0);
        Check("H. 승리 - 쓰러진 1번 적의 효과도 제거", enemyA.combatant.statusEffects.Count, 0);
        p.combatant.ApplyStatus(StatusEffectType.AttackUp, 2, 2);   // 효과가 남아 있다고 가정
        c.StartBattle();
        Check("H. 새 전투 시작 - 플레이어 효과 없음", p.combatant.statusEffects.Count, 0);

        DestroyTempCards();
        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    // 새 런 + 새 전투를 시작한 뒤, 손패를 지정한 카드들로 바꾼다.
    private void Prepare(params CardData[] handCards)
    {
        combat.StartRun();
        combat.StartBattle();
        SetHand(handCards);
    }

    // 전투는 그대로 두고 손패만 지정한 카드들로 바꾼다.
    private void SetHand(params CardData[] handCards)
    {
        combat.cardManager.hand.Clear();
        foreach (CardData card in handCards)
        {
            combat.cardManager.hand.Add(new CardInstance(card));
        }
    }

    // 강화된 카드 한 장을 손패에 넣는다.
    private void AddUpgraded(CardData data)
    {
        CardInstance card = new CardInstance(data);
        card.Upgrade();
        combat.cardManager.hand.Add(card);
    }

    // type 효과의 수치 (효과가 없으면 0)
    private int Amount(Combatant target, StatusEffectType type)
    {
        StatusEffect effect = target.FindStatus(type);
        return effect != null ? effect.amount : 0;
    }

    // type 효과의 남은 지속시간 (효과가 없으면 0)
    private int Turns(Combatant target, StatusEffectType type)
    {
        StatusEffect effect = target.FindStatus(type);
        return effect != null ? effect.turnsLeft : 0;
    }

    // 테스트용 임시 카드를 만든다. (에셋 파일로 저장되지 않는다)
    private void CreateTempCards()
    {
        focus = MakeBuff("집중", TargetType.Self, StatusEffectType.AttackUp, 2, 3, 2);
        weaken = MakeBuff("약화", TargetType.SingleEnemy, StatusEffectType.AttackDown, 2, 3, 2);
        sturdy = MakeBuff("견고", TargetType.Self, StatusEffectType.DefenseUp, 2, 3, 2);
        crack = MakeBuff("균열", TargetType.SingleEnemy, StatusEffectType.DefenseDown, 2, 3, 2);
        noneBuff = MakeBuff("효과없음(잘못된 데이터)", TargetType.Self, StatusEffectType.None, 2, 2, 2);
        zeroBuff = MakeBuff("지속0", TargetType.Self, StatusEffectType.AttackUp, 2, 2, 0);
        reorder = MakeCard("재정렬", CardType.Special, TargetType.Self, 1, 2, 3);
        single = MakeCard("테스트단일", CardType.Attack, TargetType.SingleEnemy, 1, 6, 9);
        sweep = MakeCard("휩쓸기", CardType.Attack, TargetType.AllEnemies, 1, 4, 6);
        randomShot = MakeCard("난사", CardType.Attack, TargetType.RandomEnemy, 1, 7, 10);
        guardSelf = MakeCard("테스트수비", CardType.Defend, TargetType.Self, 1, 5, 8);
    }

    private CardData MakeCard(string cardName, CardType type, TargetType targetType, int cost, int value, int upgradedValue)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = cardName;
        card.type = type;
        card.targetType = targetType;
        card.cost = cost;
        card.value = value;
        card.upgradedValue = upgradedValue;
        return card;
    }

    // Buff 카드 (비용 1)
    private CardData MakeBuff(string cardName, TargetType targetType, StatusEffectType effect, int value, int upgradedValue, int duration)
    {
        CardData card = MakeCard(cardName, CardType.Buff, targetType, 1, value, upgradedValue);
        card.statusEffect = effect;
        card.duration = duration;
        return card;
    }

    private void DestroyTempCards()
    {
        DestroyTemp(focus);
        DestroyTemp(weaken);
        DestroyTemp(sturdy);
        DestroyTemp(crack);
        DestroyTemp(reorder);
        DestroyTemp(noneBuff);
        DestroyTemp(zeroBuff);
        DestroyTemp(single);
        DestroyTemp(sweep);
        DestroyTemp(randomShot);
        DestroyTemp(guardSelf);
    }

    // 임시로 만든 ScriptableObject를 지운다.
    private void DestroyTemp(Object obj)
    {
        if (obj == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }

    private bool FindReferences()
    {
        if (combat == null) combat = GetComponent<CombatManager>();
        if (combat == null)
        {
            Debug.LogError("CombatManager를 찾을 수 없다. 같은 오브젝트에 붙이거나 인스펙터에 연결하자.");
            return false;
        }
        if (combat.player == null)
        {
            Debug.LogError("CombatManager의 Player 칸을 채우자.");
            return false;
        }
        if (combat.cardManager == null)
        {
            combat.cardManager = combat.player.cardManager;
        }
        if (combat.cardManager == null)
        {
            Debug.LogError("CardManager가 연결되지 않았다.");
            return false;
        }
        if (combat.enemies.Count < 2 || combat.enemies[0] == null || combat.enemies[1] == null)
        {
            Debug.LogError("이 테스트는 CombatManager의 Enemies 리스트에 적 2마리가 필요하다.");
            return false;
        }
        return true;
    }

    // 정수 값 비교
    private void Check(string name, int actual, int expected)
    {
        if (actual == expected)
        {
            passCount++;
            Debug.Log("PASS  " + name + " (값: " + actual + ")");
        }
        else
        {
            failCount++;
            Debug.LogError("FAIL  " + name + " (기대: " + expected + ", 실제: " + actual + ")");
        }
    }

    // true/false 값 비교
    private void CheckBool(string name, bool actual, bool expected)
    {
        if (actual == expected)
        {
            passCount++;
            Debug.Log("PASS  " + name);
        }
        else
        {
            failCount++;
            Debug.LogError("FAIL  " + name + " (기대: " + expected + ", 실제: " + actual + ")");
        }
    }

    // 문자열 비교
    private void CheckText(string name, string actual, string expected)
    {
        if (actual == expected)
        {
            passCount++;
            Debug.Log("PASS  " + name + " (\"" + actual + "\")");
        }
        else
        {
            failCount++;
            Debug.LogError("FAIL  " + name + " (기대: \"" + expected + "\", 실제: \"" + actual + "\")");
        }
    }
}
