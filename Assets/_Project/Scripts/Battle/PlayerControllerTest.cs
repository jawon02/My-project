using UnityEngine;

/// <summary>
/// 1-D 단계 테스트용 스크립트. (검증이 끝나면 삭제해도 된다)
/// PlayerController와 같은 오브젝트에 붙이면 자동으로 찾는다.
/// 손패를 직접 정해서 테스트하므로 셔플 결과와 상관없이 항상 같은 결과가 나온다.
/// </summary>
public class PlayerControllerTest : MonoBehaviour
{
    [Header("연결")]
    public PlayerController player;
    public CardManager cardManager;

    [Header("테스트에 쓸 카드 에셋 (1-B, 1-C에서 만든 것)")]
    public CardData strike;        // 타격: 비용 1, Attack 6
    public CardData guard;         // 수비: 비용 1, Defend 5
    public CardData heavyStrike;   // 강타: 비용 2, Attack 10
    public CardData quickJab;      // 속공: 비용 0, Attack 2
    public CardData special;       // 재정렬(특수): 비용 1, Special, 드로우 2

    [Header("가짜 적 (공격 대상, 인스펙터에서 체력 변화 확인용)")]
    public Combatant dummyEnemy = new Combatant();

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
        Debug.Log("===== PlayerController 자동 테스트 시작 =====");

        PlayerController p = player;
        CardManager cm = cardManager;
        p.maxEnergy = 3;   // 테스트 시작 전에 기본값 3으로 맞춘다

        // A) 런 시작: 체력 70, 방어도 0
        p.InitRun();
        Check("A. 런 시작 - 체력", p.combatant.currentHp, 70);
        Check("A. 런 시작 - 방어도", p.combatant.block, 0);

        // B) 턴 시작: 방어도 초기화 + 에너지 회복
        p.combatant.GainBlock(4);   // 지난 턴에 남은 방어도가 있다고 가정
        p.StartTurn();
        Check("B. 턴 시작 - 에너지 회복", p.energy, 3);
        Check("B. 턴 시작 - 방어도 초기화", p.combatant.block, 0);

        // 테스트 준비: 가짜 적(체력 40)과 손패 5장을 직접 만든다.
        dummyEnemy.Init(40);
        cm.StartRun();      // 런 덱 새로 생성
        cm.StartBattle();
        cm.hand.Clear();
        cm.drawPile.Clear();
        cm.discardPile.Clear();
        cm.hand.Add(new CardInstance(strike));        // 0번
        cm.hand.Add(new CardInstance(guard));         // 1번
        cm.hand.Add(new CardInstance(heavyStrike));   // 2번
        cm.hand.Add(new CardInstance(quickJab));      // 3번
        cm.hand.Add(new CardInstance(special));       // 4번

        // C1) 타격(비용 1, 공격 6): 에너지 3→2, 적 체력 40→34
        bool ok = p.TryPlayCard(0, dummyEnemy);
        CheckBool("C1. 타격 사용 성공", ok, true);
        Check("C1. 타격 - 에너지", p.energy, 2);
        Check("C1. 타격 - 적 체력", dummyEnemy.currentHp, 34);
        Check("C1. 타격 - 손패(5→4)", cm.hand.Count, 4);
        Check("C1. 타격 - 버림 더미", cm.discardPile.Count, 1);
        // 이제 손패: [수비, 강타, 속공, 특수]

        // C2) 수비(비용 1, 방어 5): 에너지 2→1, 플레이어 방어도 0→5
        ok = p.TryPlayCard(0, null);   // 방어 카드는 대상 필요 없음
        CheckBool("C2. 수비 사용 성공", ok, true);
        Check("C2. 수비 - 에너지", p.energy, 1);
        Check("C2. 수비 - 플레이어 방어도", p.combatant.block, 5);
        Check("C2. 수비 - 손패", cm.hand.Count, 3);
        Check("C2. 수비 - 버림 더미", cm.discardPile.Count, 2);
        // 이제 손패: [강타, 속공, 특수]

        // C3) 강타(비용 2)를 에너지 1로 사용 → 실패해야 하고, 아무것도 변하면 안 됨
        ok = p.TryPlayCard(0, dummyEnemy);
        CheckBool("C3. 에너지 부족 - 사용 실패", ok, false);
        Check("C3. 에너지 부족 - 에너지 그대로", p.energy, 1);
        Check("C3. 에너지 부족 - 적 체력 그대로", dummyEnemy.currentHp, 34);
        Check("C3. 에너지 부족 - 손패 그대로", cm.hand.Count, 3);

        // C4) 속공(비용 0, 공격 2): 에너지 변화 없이 사용 가능, 적 체력 34→32
        ok = p.TryPlayCard(1, dummyEnemy);
        CheckBool("C4. 속공(0코스트) 사용 성공", ok, true);
        Check("C4. 속공 - 에너지 그대로", p.energy, 1);
        Check("C4. 속공 - 적 체력", dummyEnemy.currentHp, 32);
        Check("C4. 속공 - 손패", cm.hand.Count, 2);
        // 이제 손패: [강타, 특수]

        // C5) 재정렬(특수, 비용 1, 드로우 2) - Phase 4에서 효과 구현됨
        //     뽑기 더미가 비어 있으므로 버림 더미(타격, 수비, 속공 3장)를 섞어 2장을 뽑는다.
        //     손패: [강타, 재정렬] → 재정렬 사용 → 2장 드로우 → 재정렬은 버림 더미로 → [강타, ?, ?]
        //     버림 더미: 3장 → 재셔플로 0장 → 재정렬 1장
        ok = p.TryPlayCard(1, null);
        CheckBool("C5. 재정렬 사용 성공", ok, true);
        Check("C5. 재정렬 - 에너지 1→0", p.energy, 0);
        Check("C5. 재정렬 - 손패 (강타 + 2장 드로우)", cm.hand.Count, 3);
        Check("C5. 재정렬 - 버림 더미 (재셔플 후 재정렬 1장)", cm.discardPile.Count, 1);
        // 이제 손패: [강타, 드로우한 카드 2장]

        // C6) 공격 카드에 대상이 없으면 실패하고 에너지를 쓰지 않아야 함
        p.StartTurn();   // 에너지 3으로 회복
        ok = p.TryPlayCard(0, null);   // 0번 = 강타
        CheckBool("C6. 대상 없는 공격 - 사용 실패", ok, false);
        Check("C6. 대상 없는 공격 - 에너지 그대로", p.energy, 3);
        Check("C6. 대상 없는 공격 - 손패 그대로", cm.hand.Count, 3);

        // E) 존재하지 않는 손패 번호
        ok = p.TryPlayCard(99, dummyEnemy);
        CheckBool("E. 잘못된 손패 번호 - 사용 실패", ok, false);

        // D) 턴 종료: 남은 에너지 소멸
        p.EndTurn();
        Check("D. 턴 종료 - 남은 에너지 소멸", p.energy, 0);

        // F) 최대 에너지 증가: 다음 턴 시작 때 늘어난 만큼 회복
        p.IncreaseMaxEnergy(1);
        Check("F. 최대 에너지 증가", p.maxEnergy, 4);
        p.StartTurn();
        Check("F. 늘어난 최대 에너지로 회복", p.energy, 4);

        // H) 에너지 직접 사용 (나중에 스킬이 쓰는 함수)
        ok = p.TrySpendEnergy(3);
        CheckBool("H. 에너지 3 사용 성공", ok, true);
        Check("H. 에너지 3 사용 후 남은 에너지", p.energy, 1);
        ok = p.TrySpendEnergy(2);
        CheckBool("H. 에너지 부족 시 사용 실패", ok, false);
        Check("H. 실패 후 에너지 그대로", p.energy, 1);

        p.maxEnergy = 3;   // 원래대로 되돌림
        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // ---------------------------------------------------------------
    // 수동 테스트 (눈으로 확인하고 싶을 때, 위에서부터 순서대로 실행)
    // ---------------------------------------------------------------
    [ContextMenu("Manual 1: Setup (손패 5장 + 가짜 적 체력 40)")]
    public void ManualSetup()
    {
        if (!FindReferences()) return;
        player.InitRun();
        dummyEnemy.Init(40);
        cardManager.StartRun();
        cardManager.StartBattle();
        cardManager.hand.Clear();
        cardManager.drawPile.Clear();
        cardManager.discardPile.Clear();
        cardManager.hand.Add(new CardInstance(strike));
        cardManager.hand.Add(new CardInstance(guard));
        cardManager.hand.Add(new CardInstance(heavyStrike));
        cardManager.hand.Add(new CardInstance(quickJab));
        cardManager.hand.Add(new CardInstance(special));
        cardManager.LogState();
    }

    [ContextMenu("Manual 2: Start Turn")]
    public void ManualStartTurn()
    {
        if (!FindReferences()) return;
        player.StartTurn();
        player.LogState();
    }

    [ContextMenu("Manual 3: Play Hand Card 0 (맨 앞 카드 사용)")]
    public void ManualPlayCard0()
    {
        if (!FindReferences()) return;
        player.TryPlayCard(0, dummyEnemy);
        player.LogState();
        Debug.Log("가짜 적 체력 " + dummyEnemy.currentHp + " / 손패 " + cardManager.hand.Count + "장");
    }

    [ContextMenu("Manual 4: End Turn")]
    public void ManualEndTurn()
    {
        if (!FindReferences()) return;
        player.EndTurn();
        player.LogState();
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    // PlayerController, CardManager, 카드 에셋이 모두 연결됐는지 확인한다.
    private bool FindReferences()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogError("PlayerController를 찾을 수 없다. 같은 오브젝트에 붙이거나 인스펙터에 연결하자.");
            return false;
        }

        if (cardManager == null) cardManager = player.cardManager;
        if (cardManager == null)
        {
            Debug.LogError("CardManager가 연결되지 않았다. PlayerController의 Card Manager 칸을 채우자.");
            return false;
        }

        if (strike == null || guard == null || heavyStrike == null || quickJab == null || special == null)
        {
            Debug.LogError("테스트용 카드 에셋 5개(strike, guard, heavyStrike, quickJab, special)를 모두 연결하자.");
            return false;
        }
        return true;
    }

    // 정수 값 비교: 기대값과 실제값이 같으면 PASS
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
