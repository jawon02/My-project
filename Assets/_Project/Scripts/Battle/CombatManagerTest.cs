using UnityEngine;

/// <summary>
/// 1-F 단계 테스트용 스크립트. (검증이 끝나면 삭제해도 된다)
/// CombatManager와 같은 오브젝트에 붙이면 자동으로 찾는다.
/// 주의: 이 테스트는 "적 2마리(슬라임: 체력 40, 패턴 공격 7 → 방어 5 → 공격 10)"가
///       CombatManager의 Enemies 리스트에 들어 있다는 전제로 만들었다.
/// 손패는 테스트 안에서 직접 정하므로 셔플 결과와 상관없이 항상 같은 결과가 나온다.
/// </summary>
public class CombatManagerTest : MonoBehaviour
{
    [Header("연결")]
    public CombatManager combat;

    [Header("테스트에 쓸 카드 에셋")]
    public CardData strike;      // 타격: 비용 1, Attack 6
    public CardData guard;       // 수비: 비용 1, Defend 5
    public CardData quickJab;    // 속공: 비용 0, Attack 2

    private int passCount = 0;
    private int failCount = 0;

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
        Debug.Log("===== CombatManager 자동 테스트 시작 =====");

        CombatManager c = combat;
        EnemyController enemyA = c.enemies[0];   // 리스트 첫 번째 적 (먼저 행동, 먼저 맞음)
        EnemyController enemyB = c.enemies[1];   // 리스트 두 번째 적

        // ===== 테스트 1: 전투 시작 (플레이어 선공) =====
        c.StartRun();       // 런 시작 (플레이어 체력 + 런 덱)
        c.StartBattle();
        CheckBool("1. 전투 시작 - 플레이어 턴으로 시작(선공)", c.state == BattleState.PlayerTurn, true);
        Check("1. 전투 시작 - 손패 5장 드로우", c.cardManager.hand.Count, 5);
        Check("1. 전투 시작 - 뽑기 더미 5장", c.cardManager.drawPile.Count, 5);
        Check("1. 전투 시작 - 에너지 3", c.player.energy, 3);
        Check("1. 전투 시작 - 살아 있는 적 2마리", c.livingEnemies.Count, 2);

        // ===== 테스트 2: 카드 사용 → 턴 종료 → 적 행동 → 다음 턴 =====
        Prepare(strike, guard, quickJab);
        c.PlayCard(0);   // 타격: 맨 앞의 적(A)을 공격
        Check("2. 타격 - 첫 번째 적(A) 체력 40→34", enemyA.combatant.currentHp, 34);
        Check("2. 타격 - 두 번째 적(B)은 그대로", enemyB.combatant.currentHp, 40);
        Check("2. 타격 - 에너지 3→2", c.player.energy, 2);
        c.PlayCard(0);   // 수비: 방어도 5 (손패가 앞으로 당겨져 수비가 0번)
        Check("2. 수비 - 방어도 5", c.player.combatant.block, 5);
        Check("2. 수비 - 에너지 2→1", c.player.energy, 1);

        c.EndPlayerTurn();   // 적 A: 공격 7 (방어도 5가 막아 체력 -2), 적 B: 공격 7 (체력 -7)
        Check("2. 적 턴 후 플레이어 체력 70→61", c.player.combatant.currentHp, 61);
        Check("2. 다음 턴 시작 - 방어도 초기화", c.player.combatant.block, 0);
        Check("2. 다음 턴 시작 - 에너지 3 회복", c.player.energy, 3);
        Check("2. 다음 턴 시작 - 손패 5장 드로우", c.cardManager.hand.Count, 5);
        CheckBool("2. 다음 턴 시작 - 플레이어 턴 상태", c.state == BattleState.PlayerTurn, true);
        CheckText("2. 적 A 다음 행동 예고", enemyA.nextActionPreview, "방어 5");
        CheckText("2. 적 B 다음 행동 예고", enemyB.nextActionPreview, "방어 5");

        // ===== 테스트 3: 패배 (플레이어 체력 0 → 남은 적은 행동하지 않음) =====
        Prepare(strike);
        c.player.combatant.currentHp = 5;   // 체력을 5로 만들어 첫 번째 적의 공격 7에 쓰러지게 한다
        c.EndPlayerTurn();
        CheckBool("3. 플레이어 체력 0 - 즉시 패배", c.state == BattleState.Defeat, true);
        Check("3. 패배 - 플레이어 체력 0", c.player.combatant.currentHp, 0);
        Check("3. 패배 - 적 A는 행동함(행동 횟수 1)", enemyA.turnIndex, 1);
        Check("3. 패배 - 적 B는 행동하지 않음(행동 횟수 0)", enemyB.turnIndex, 0);

        // ===== 테스트 4: 승리 (적 사망 즉시 제거, 마지막 적 처치 시 즉시 승리) =====
        Prepare(strike, strike, quickJab, guard);
        enemyA.combatant.currentHp = 5;   // 적 A: 타격 한 번에 쓰러지는 체력
        c.PlayCard(0);                    // 타격 → 적 A 처치
        Check("4. 적 A 처치 - 살아 있는 적 1마리", c.livingEnemies.Count, 1);
        CheckBool("4. 적 A 처치 - 오브젝트 숨김", enemyA.gameObject.activeSelf, false);
        CheckBool("4. 적 A 처치 - 아직 전투 중", c.state == BattleState.PlayerTurn, true);
        c.PlayCard(0);                    // 타격 → 이제 맨 앞의 적은 B
        Check("4. 다음 공격은 적 B로 (체력 40→34)", enemyB.combatant.currentHp, 34);
        enemyB.combatant.currentHp = 2;   // 적 B: 속공(피해 2)에 쓰러지는 체력
        c.PlayCard(0);                    // 속공(0코스트) → 적 B 처치
        CheckBool("4. 마지막 적 처치 - 즉시 승리", c.state == BattleState.Victory, true);
        Check("4. 승리 - 살아 있는 적 0마리", c.livingEnemies.Count, 0);
        CheckBool("4. 승리 - 적 B 오브젝트 숨김", enemyB.gameObject.activeSelf, false);
        Check("4. 승리 - 플레이어 턴이 남아 있어도 에너지 1 그대로", c.player.energy, 1);

        bool played = c.PlayCard(0);      // 손패에 수비가 남아 있지만, 전투가 끝났으므로 사용 불가
        CheckBool("4. 승리 후 카드 사용 차단", played, false);
        Check("4. 승리 후 에너지 그대로", c.player.energy, 1);
        Check("4. 승리 후 플레이어 방어도 그대로(0)", c.player.combatant.block, 0);
        c.EndPlayerTurn();                // 승리 후 턴 종료도 무시되어야 한다
        CheckBool("4. 승리 후 턴 종료 무시 - 상태 유지", c.state == BattleState.Victory, true);
        Check("4. 승리 후 적 행동 없음 - 플레이어 체력 그대로", c.player.combatant.currentHp, 70);

        // ===== 테스트 5: 승리 후 새 전투 시작 → 적이 다시 등장 =====
        c.StartBattle();
        CheckBool("5. 새 전투 - 플레이어 턴으로 시작", c.state == BattleState.PlayerTurn, true);
        CheckBool("5. 새 전투 - 적 A 다시 등장", enemyA.gameObject.activeSelf, true);
        CheckBool("5. 새 전투 - 적 B 다시 등장", enemyB.gameObject.activeSelf, true);
        Check("5. 새 전투 - 살아 있는 적 2마리", c.livingEnemies.Count, 2);
        Check("5. 새 전투 - 적 A 체력 회복(40)", enemyA.combatant.currentHp, 40);

        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // ---------------------------------------------------------------
    // 수동 테스트 (Play 버튼을 누르지 않고 우클릭 메뉴로 한 판 진행해 볼 수 있다)
    // 플레이 모드에서는 Game 창을 클릭한 뒤 키보드로도 가능: 숫자 1~9,0 = 카드 사용, 스페이스 = 턴 종료
    // ---------------------------------------------------------------
    [ContextMenu("Manual: Start Run + Battle")]
    public void ManualStart()
    {
        if (!FindReferences()) return;
        combat.StartRun();
        combat.StartBattle();
    }

    [ContextMenu("Manual: Play Card 1번째 (손패 0번)")]
    public void ManualPlay0() { PlayAndLog(0); }

    [ContextMenu("Manual: Play Card 2번째 (손패 1번)")]
    public void ManualPlay1() { PlayAndLog(1); }

    [ContextMenu("Manual: Play Card 3번째 (손패 2번)")]
    public void ManualPlay2() { PlayAndLog(2); }

    [ContextMenu("Manual: End Player Turn")]
    public void ManualEndTurn()
    {
        if (!FindReferences()) return;
        combat.EndPlayerTurn();
    }

    [ContextMenu("Manual: Log Battle State")]
    public void ManualLog()
    {
        if (!FindReferences()) return;
        combat.LogBattleState();
    }

    private void PlayAndLog(int index)
    {
        if (!FindReferences()) return;
        combat.PlayCard(index);
        if (!combat.IsBattleOver)
        {
            combat.LogBattleState();
        }
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    // 새 런 + 새 전투를 시작한 뒤, 손패를 지정한 카드들로 바꾼다. (뽑기 더미 5장은 그대로 남음)
    private void Prepare(params CardData[] handCards)
    {
        combat.StartRun();
        combat.StartBattle();
        combat.cardManager.hand.Clear();
        foreach (CardData card in handCards)
        {
            combat.cardManager.hand.Add(new CardInstance(card));   // 카드 한 장(CardInstance)으로 만들어 손패에 넣는다
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
        if (strike == null || guard == null || quickJab == null)
        {
            Debug.LogError("테스트용 카드 에셋 3개(strike, guard, quickJab)를 연결하자.");
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

    // 문자열 비교
    private void CheckText(string name, string actual, string expected)
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
