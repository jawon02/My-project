using UnityEngine;

/// <summary>
/// 1-C 단계 테스트용 스크립트. (검증이 끝나면 삭제해도 된다)
/// CardManager와 같은 오브젝트에 붙이면 자동으로 CardManager를 찾는다.
/// 인스펙터에서 컴포넌트 이름을 우클릭하면 테스트 메뉴가 보인다.
/// </summary>
public class CardManagerTest : MonoBehaviour
{
    public CardManager cardManager;

    private int passCount = 0;
    private int failCount = 0;

    // ---------------------------------------------------------------
    // 자동 테스트
    // ---------------------------------------------------------------
    [ContextMenu("Run Auto Test")]
    public void RunAutoTest()
    {
        if (cardManager == null)
        {
            cardManager = GetComponent<CardManager>();
        }
        if (cardManager == null)
        {
            Debug.LogError("CardManager를 찾을 수 없다. 같은 오브젝트에 붙이거나 인스펙터에 연결하자.");
            return;
        }

        passCount = 0;
        failCount = 0;
        Debug.Log("===== CardManager 자동 테스트 시작 =====");

        CardManager cm = cardManager;
        cm.StartRun();                      // 런 시작: 시작 덱으로 런 덱(카드 한 장 한 장)을 만든다
        int deckSize = cm.runDeck.Count;
        int originalMaxHand = cm.maxHandSize;
        cm.maxHandSize = 10;   // 테스트 시작 전에 기본값 10으로 맞춘다

        // A) 덱 구성 확인: 총 10장 = 공격 5 + 방어 4 + 특수 1
        Check("A. 덱 총 장수", deckSize, 10);
        Check("A. 공격 카드 수", CountType(cm, CardType.Attack), 5);
        Check("A. 방어 카드 수", CountType(cm, CardType.Defend), 4);
        Check("A. 특수 카드 수", CountType(cm, CardType.Special), 1);

        // B) 전투 시작: 덱 전체가 뽑기 더미로, 손패/버림은 비어 있어야 함
        cm.StartBattle();
        Check("B. 전투 시작 후 뽑기 더미", cm.drawPile.Count, 10);
        Check("B. 전투 시작 후 손패", cm.hand.Count, 0);
        Check("B. 전투 시작 후 버림 더미", cm.discardPile.Count, 0);

        // C) 5장 드로우
        int drawn = cm.DrawCards(5);
        Check("C. 5장 드로우 - 뽑은 장수", drawn, 5);
        Check("C. 5장 드로우 - 손패", cm.hand.Count, 5);
        Check("C. 5장 드로우 - 뽑기 더미", cm.drawPile.Count, 5);
        Check("C. 5장 드로우 - 버림 더미", cm.discardPile.Count, 0);
        Check("C. 5장 드로우 - 카드 총합 유지", cm.TotalCardCount, deckSize);

        // D) 손패 0번 카드 1장 버리기
        cm.DiscardFromHand(0);
        Check("D. 1장 버리기 - 손패", cm.hand.Count, 4);
        Check("D. 1장 버리기 - 버림 더미", cm.discardPile.Count, 1);
        Check("D. 1장 버리기 - 카드 총합 유지", cm.TotalCardCount, deckSize);

        // E) 손패 전부 버리기 (턴 종료 상황)
        cm.DiscardAllHand();
        Check("E. 손패 전부 버리기 - 손패", cm.hand.Count, 0);
        Check("E. 손패 전부 버리기 - 버림 더미", cm.discardPile.Count, 5);
        Check("E. 손패 전부 버리기 - 뽑기 더미", cm.drawPile.Count, 5);
        Check("E. 손패 전부 버리기 - 카드 총합 유지", cm.TotalCardCount, deckSize);

        // F) 뽑기 더미 소진: 5장을 더 뽑으면 뽑기 더미가 0장이 된다.
        cm.DrawCards(5);
        Check("F1. 뽑기 더미를 다 뽑음 - 손패", cm.hand.Count, 5);
        Check("F1. 뽑기 더미를 다 뽑음 - 뽑기 더미", cm.drawPile.Count, 0);
        Check("F1. 뽑기 더미를 다 뽑음 - 버림 더미", cm.discardPile.Count, 5);

        //    이제 3장을 더 뽑으면 버림 더미(5장)가 섞여 뽑기 더미가 되고, 3장을 뽑는다.
        drawn = cm.DrawCards(3);
        Check("F2. 재셔플 후 드로우 - 뽑은 장수", drawn, 3);
        Check("F2. 재셔플 후 드로우 - 손패", cm.hand.Count, 8);
        Check("F2. 재셔플 후 드로우 - 뽑기 더미", cm.drawPile.Count, 2);
        Check("F2. 재셔플 후 드로우 - 버림 더미(비어야 함)", cm.discardPile.Count, 0);
        Check("F2. 재셔플 후 드로우 - 카드 총합 유지", cm.TotalCardCount, deckSize);

        // G) 손패 초과: 손패 최대치를 3으로 줄이고 5장을 뽑는다.
        //    손패에는 3장만 들어가고, 초과 2장은 버림 더미로 가며, 뽑기 더미는 5장 줄어야 한다.
        cm.StartBattle();
        cm.maxHandSize = 3;
        drawn = cm.DrawCards(5);
        Check("G. 손패 초과 - 뽑은 장수", drawn, 5);
        Check("G. 손패 초과 - 손패(최대 3)", cm.hand.Count, 3);
        Check("G. 손패 초과 - 버림 더미(초과 2장)", cm.discardPile.Count, 2);
        Check("G. 손패 초과 - 뽑기 더미(5장 소모)", cm.drawPile.Count, 5);
        Check("G. 손패 초과 - 카드 총합 유지", cm.TotalCardCount, deckSize);
        cm.maxHandSize = 10;   // 원래대로 되돌림

        // H) 뽑기/버림 더미가 모두 비었을 때 드로우 중단
        cm.StartBattle();
        cm.DrawCards(10);      // 10장을 모두 손패로
        Check("H. 전부 뽑기 - 손패", cm.hand.Count, 10);
        Check("H. 전부 뽑기 - 뽑기 더미", cm.drawPile.Count, 0);
        drawn = cm.DrawCards(3);   // 뽑을 카드가 없음 -> 중단되어야 함
        Check("H. 더미가 모두 비었을 때 - 뽑은 장수", drawn, 0);
        Check("H. 더미가 모두 비었을 때 - 손패 그대로", cm.hand.Count, 10);
        Check("H. 더미가 모두 비었을 때 - 카드 총합 유지", cm.TotalCardCount, deckSize);

        // I) 전투를 다시 시작하면 전부 초기화되어야 함
        cm.StartBattle();
        Check("I. 재시작 - 손패", cm.hand.Count, 0);
        Check("I. 재시작 - 버림 더미", cm.discardPile.Count, 0);
        Check("I. 재시작 - 뽑기 더미", cm.drawPile.Count, 10);

        cm.maxHandSize = originalMaxHand;
        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // ---------------------------------------------------------------
    // 수동 테스트 (눈으로 확인하고 싶을 때)
    // ---------------------------------------------------------------
    [ContextMenu("Manual: Start Battle (덱 섞기)")]
    public void ManualStartBattle()
    {
        GetCM().StartBattle();
        GetCM().LogState();
    }

    [ContextMenu("Manual: Draw 5")]
    public void ManualDraw5()
    {
        GetCM().DrawCards(5);
        GetCM().LogState();
    }

    [ContextMenu("Manual: Draw 1")]
    public void ManualDraw1()
    {
        GetCM().DrawCards(1);
        GetCM().LogState();
    }

    [ContextMenu("Manual: Discard All Hand (턴 종료)")]
    public void ManualDiscardAll()
    {
        GetCM().DiscardAllHand();
        GetCM().LogState();
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------
    private CardManager GetCM()
    {
        if (cardManager == null)
        {
            cardManager = GetComponent<CardManager>();
        }
        return cardManager;
    }

    // 덱에서 특정 종류의 카드가 몇 장인지 센다.
    private int CountType(CardManager cm, CardType type)
    {
        int count = 0;
        foreach (CardData card in cm.startingDeck)
        {
            if (card != null && card.type == type)
            {
                count++;
            }
        }
        return count;
    }

    // 기대값과 실제값을 비교해서 PASS / FAIL 로그를 출력한다.
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
}
