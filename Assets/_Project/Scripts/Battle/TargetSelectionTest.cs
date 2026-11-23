using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 3 (카드 대상 선택) 테스트용 스크립트. CombatManager와 같은 오브젝트에 붙인다.
/// 컴포넌트 우클릭 → Run Auto Test 로 실행한다.
///
/// 전제: CombatManager의 Enemies 리스트에 적 2마리(슬라임: 체력 40)가 들어 있다. (기존 테스트와 같은 구성)
/// 테스트에 쓰는 카드(타격형, 휩쓸기, 난사 등)는 이 스크립트가 임시로 만들고 끝나면 지운다.
/// 그래서 카드 에셋을 따로 만들거나 연결하지 않아도 된다. (시작 덱도 건드리지 않는다)
/// 손패는 테스트 안에서 직접 정하므로 셔플 결과와 상관없이 항상 같은 결과가 나온다.
/// (무작위 공격 테스트는 "누가 맞았는지"가 아니라 "정확히 1명만 맞았는지"를 확인한다)
/// </summary>
public class TargetSelectionTest : MonoBehaviour
{
    [Header("연결")]
    public CombatManager combat;

    private int passCount = 0;
    private int failCount = 0;

    // 테스트 중에만 쓰는 임시 카드 원본들
    private CardData single;       // 단일 공격: 비용 1, 피해 6 (강화 9) - 타격과 같은 수치
    private CardData sweep;        // 휩쓸기: 전체 공격, 비용 1, 피해 4 (강화 6)
    private CardData randomShot;   // 난사: 무작위 공격, 비용 1, 피해 7 (강화 10)
    private CardData selfAttack;   // 잘못된 데이터: 공격 카드인데 대상이 Self
    private CardData guardSelf;    // 방어 카드: Self, 방어도 5 (강화 8)

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
        Debug.Log("===== Phase 3 대상 선택 자동 테스트 시작 =====");
        CreateTempCards();

        CombatManager c = combat;
        EnemyController enemyA = c.enemies[0];   // 1번 적 (왼쪽)
        EnemyController enemyB = c.enemies[1];   // 2번 적 (오른쪽)

        // ===== 1. 단일 공격: 기본 대상 =====
        Prepare(single, single, single);
        Check("1. 전투 시작 - 기본 대상은 가장 왼쪽 적(칸 0)", c.selectedEnemyIndex, 0);
        c.PlayCard(0);
        Check("1. 단일 공격 - 선택된 1번 적 체력 40→34", enemyA.combatant.currentHp, 34);
        Check("1. 단일 공격 - 2번 적은 그대로 40", enemyB.combatant.currentHp, 40);

        // ===== 2. 대상 변경 (←/→/Tab에 연결된 함수) =====
        c.SelectNextEnemy();   // →
        Check("2. → - 대상이 2번 적(칸 1)으로", c.selectedEnemyIndex, 1);
        c.PlayCard(0);
        Check("2. 변경 후 공격 - 2번 적 체력 40→34", enemyB.combatant.currentHp, 34);
        Check("2. 변경 후 공격 - 1번 적은 그대로 34", enemyA.combatant.currentHp, 34);
        c.SelectNextEnemy();   // → (오른쪽 끝)
        Check("2. 오른쪽 끝에서 → - 그대로 칸 1", c.selectedEnemyIndex, 1);
        c.SelectPreviousEnemy();   // ←
        Check("2. ← - 대상이 1번 적(칸 0)으로", c.selectedEnemyIndex, 0);
        c.SelectPreviousEnemy();   // ← (왼쪽 끝)
        Check("2. 왼쪽 끝에서 ← - 그대로 칸 0", c.selectedEnemyIndex, 0);
        c.SelectNextEnemyLoop();   // Tab
        Check("2. Tab - 다음 적(칸 1)", c.selectedEnemyIndex, 1);
        c.SelectNextEnemyLoop();   // Tab (끝에서 처음으로)
        Check("2. 끝에서 Tab - 처음(칸 0)으로 순환", c.selectedEnemyIndex, 0);
        CheckBool("2. SelectEnemy(1) - 직접 선택 성공", c.SelectEnemy(1), true);
        CheckBool("2. SelectEnemy(5) - 없는 칸은 선택 실패", c.SelectEnemy(5), false);
        Check("2. 선택 실패 후에도 대상 그대로 칸 1", c.selectedEnemyIndex, 1);

        // ===== 3. 선택된 적 사망 → 자동 변경, 고정 번호 유지 =====
        Prepare(single, single, single);
        Check("3. 새 전투 - 대상이 가장 왼쪽(칸 0)으로 초기화", c.selectedEnemyIndex, 0);
        enemyA.combatant.currentHp = 5;   // 1번 적: 한 번에 쓰러지는 체력
        c.PlayCard(0);
        Check("3. 1번 적 처치 - 살아 있는 적 1마리", c.livingEnemies.Count, 1);
        Check("3. 대상 자동 변경 - 남은 2번 적(칸 1) 자동 선택", c.selectedEnemyIndex, 1);
        CheckBool("3. 고정 번호 - 2번 적은 여전히 칸 1", c.enemies[1] == enemyB, true);
        CheckBool("3. 쓰러진 1번 적은 선택 불가", c.SelectEnemy(0), false);
        c.SelectPreviousEnemy();   // 왼쪽에는 쓰러진 적뿐
        Check("3. ← - 쓰러진 적을 건너뛰어 그대로 칸 1", c.selectedEnemyIndex, 1);
        c.PlayCard(0);
        Check("3. 다음 공격은 2번 적 (체력 40→34)", enemyB.combatant.currentHp, 34);

        Prepare(single, single, single);
        c.SelectEnemy(1);
        enemyB.combatant.currentHp = 5;   // 선택된 2번 적을 쓰러뜨린다
        c.PlayCard(0);
        Check("3. 2번 적 처치 - 대상이 가장 왼쪽의 살아 있는 적(칸 0)으로", c.selectedEnemyIndex, 0);
        Check("3. 1번 적은 맞지 않음 (체력 40)", enemyA.combatant.currentHp, 40);

        // ===== 4. 대상이 없으면 선택 해제 =====
        enemyA.combatant.currentHp = 5;
        c.PlayCard(0);   // 마지막 적 처치
        CheckBool("4. 마지막 적 처치 - 승리", c.state == BattleState.Victory, true);
        Check("4. 살아 있는 적 없음 - 선택 해제(-1)", c.selectedEnemyIndex, -1);

        // ===== 5. 적이 없을 때 공격 실패 (대상 목록이 비어 있음) =====
        Prepare(single, sweep, randomShot);
        List<Combatant> noTargets = new List<Combatant>();
        CheckBool("5. 대상 없음 - 단일 공격 실패", c.player.TryPlayCardOnTargets(0, noTargets), false);
        CheckBool("5. 대상 없음 - 전체 공격 실패", c.player.TryPlayCardOnTargets(1, noTargets), false);
        CheckBool("5. 대상 없음 - 무작위 공격 실패", c.player.TryPlayCardOnTargets(2, noTargets), false);
        Check("5. 실패 - 에너지 그대로 3", c.player.energy, 3);
        Check("5. 실패 - 손패 그대로 3장", c.cardManager.hand.Count, 3);

        // ===== 6. 전체 공격 =====
        Prepare(sweep);
        c.PlayCard(0);
        Check("6. 휩쓸기 - 1번 적 40→36", enemyA.combatant.currentHp, 36);
        Check("6. 휩쓸기 - 2번 적 40→36", enemyB.combatant.currentHp, 36);
        Check("6. 휩쓸기 - 에너지는 한 번만(3→2)", c.player.energy, 2);

        Prepare(sweep);
        enemyA.combatant.GainBlock(3);   // 방어도가 먼저 깎이는지 확인
        c.PlayCard(0);
        Check("6. 방어도 3인 1번 적 - 체력 40→39", enemyA.combatant.currentHp, 39);
        Check("6. 방어도 없는 2번 적 - 체력 40→36", enemyB.combatant.currentHp, 36);

        Prepare(sweep);
        enemyA.combatant.currentHp = 3;    // 1번 적만 쓰러지는 체력
        enemyB.combatant.currentHp = 10;
        c.PlayCard(0);
        Check("6. 1번 적만 처치 - 살아 있는 적 1마리", c.livingEnemies.Count, 1);
        Check("6. 1번 적이 쓰러져도 2번 적은 계속 피해 (10→6)", enemyB.combatant.currentHp, 6);
        CheckBool("6. 적이 남아 있으므로 아직 전투 중", c.state == BattleState.PlayerTurn, true);
        Check("6. 대상 자동 변경 - 2번 적(칸 1)", c.selectedEnemyIndex, 1);

        Prepare(sweep);
        enemyA.combatant.currentHp = 3;
        enemyB.combatant.currentHp = 4;
        c.PlayCard(0);
        CheckBool("6. 두 적 모두 처치 - 처리 후 승리", c.state == BattleState.Victory, true);
        Check("6. 승리 - 살아 있는 적 0마리", c.livingEnemies.Count, 0);

        // ===== 7. 무작위 공격: 20번 반복해서 매번 정확히 1명만 7 피해를 받는지 =====
        Prepare();
        int wrongCount = 0;   // 규칙에 맞지 않는 결과 수 (0이어야 정상)
        int hitA = 0;
        int hitB = 0;
        for (int i = 0; i < 20; i++)
        {
            enemyA.combatant.currentHp = 40;
            enemyB.combatant.currentHp = 40;
            c.player.energy = 3;
            c.cardManager.hand.Clear();
            c.cardManager.hand.Add(new CardInstance(randomShot));
            c.PlayCard(0);

            int lostA = 40 - enemyA.combatant.currentHp;
            int lostB = 40 - enemyB.combatant.currentHp;
            if (lostA == 7 && lostB == 0)
            {
                hitA++;
            }
            else if (lostA == 0 && lostB == 7)
            {
                hitB++;
            }
            else
            {
                wrongCount++;
            }
        }
        Debug.Log("[참고] 무작위 공격 20회 결과: 1번 적 " + hitA + "회 / 2번 적 " + hitB + "회");
        Check("7. 난사 20회 - 매번 정확히 1명에게만 7 피해 (잘못된 결과 수)", wrongCount, 0);
        Check("7. 난사 20회 - 맞은 횟수 합계", hitA + hitB, 20);
        Check("7. 난사 - 에너지 3→2", c.player.energy, 2);

        Prepare(single, randomShot);
        enemyA.combatant.currentHp = 5;
        c.PlayCard(0);   // 단일 공격으로 1번 적 처치 → 살아 있는 적은 2번 적뿐
        bool randomPlayed = c.PlayCard(0);   // 난사 (손패가 당겨져 0번)
        CheckBool("7. 생존 적 1명 - 난사 사용 성공", randomPlayed, true);
        Check("7. 생존 적 1명 - 남은 2번 적이 맞음 (40→33)", enemyB.combatant.currentHp, 33);

        // ===== 8. 강화된 공격 카드 =====
        Prepare();
        AddUpgraded(single);
        AddUpgraded(sweep);
        AddUpgraded(randomShot);
        c.PlayCard(0);   // 강화 단일 9
        Check("8. 강화 단일(9) - 1번 적 40→31", enemyA.combatant.currentHp, 31);
        c.PlayCard(0);   // 강화 휩쓸기 6
        Check("8. 강화 휩쓸기(6) - 1번 적 31→25", enemyA.combatant.currentHp, 25);
        Check("8. 강화 휩쓸기(6) - 2번 적 40→34", enemyB.combatant.currentHp, 34);
        c.PlayCard(0);   // 강화 난사 10 (누가 맞든 체력 합계가 10 줄어야 한다)
        Check("8. 강화 난사(10) - 두 적 체력 합계 59→49", enemyA.combatant.currentHp + enemyB.combatant.currentHp, 49);

        // ===== 9. Self 대상 =====
        Prepare(selfAttack, guardSelf);
        bool selfPlayed = c.PlayCard(0);
        CheckBool("9. 공격 카드 + Self - 사용 실패", selfPlayed, false);
        Check("9. 실패 - 에너지 그대로 3", c.player.energy, 3);
        Check("9. 실패 - 손패 그대로 2장", c.cardManager.hand.Count, 2);
        Check("9. 실패 - 적 체력 그대로 40", enemyA.combatant.currentHp, 40);
        c.PlayCard(1);   // 방어 카드 (Self)
        Check("9. 방어 카드(Self) - 플레이어 방어도 5", c.player.combatant.block, 5);
        Check("9. 방어 카드 - 에너지 3→2", c.player.energy, 2);

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

    // 테스트용 임시 카드를 만든다. (에셋 파일로 저장되지 않는다)
    private void CreateTempCards()
    {
        single = MakeCard("테스트단일", CardType.Attack, TargetType.SingleEnemy, 1, 6, 9);
        sweep = MakeCard("휩쓸기", CardType.Attack, TargetType.AllEnemies, 1, 4, 6);
        randomShot = MakeCard("난사", CardType.Attack, TargetType.RandomEnemy, 1, 7, 10);
        selfAttack = MakeCard("자기공격(잘못된 데이터)", CardType.Attack, TargetType.Self, 1, 6, 6);
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

    private void DestroyTempCards()
    {
        DestroyTemp(single);
        DestroyTemp(sweep);
        DestroyTemp(randomShot);
        DestroyTemp(selfAttack);
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
}
