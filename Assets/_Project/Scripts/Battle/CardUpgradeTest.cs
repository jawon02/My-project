using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 2 테스트용 스크립트: CardInstance와 카드 개별 강화를 검증한다. (검증이 끝나면 삭제해도 된다)
/// 아무 오브젝트에나 붙이면 된다. (예: CardManager 오브젝트)
///
/// 이 테스트가 올바르게 돌려면 아래 조건이 필요하다.
///  - CardManager의 Starting Deck = 타격 3, 강타 1, 속공 1, 수비 4, 재정렬(특수) 1 (총 10장)
///  - 카드 에셋 5개의 Upgraded Value가 Value보다 크게 설정되어 있을 것
///    (타격 9, 수비 8, 강타 14, 속공 4, 재정렬 3)
/// </summary>
public class CardUpgradeTest : MonoBehaviour
{
    [Header("연결")]
    public CardManager cardManager;
    public PlayerController player;

    [Header("테스트에 쓸 카드 에셋")]
    public CardData strike;       // 타격
    public CardData guard;        // 수비
    public CardData heavyStrike;  // 강타
    public CardData quickJab;     // 속공
    public CardData special;      // 재정렬 (특수)

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
        Debug.Log("===== 카드 강화(CardInstance) 자동 테스트 시작 =====");

        CardManager cm = cardManager;
        int originalMaxEnergy = player.maxEnergy;

        // 원본 에셋 값을 미리 기록해 둔다. (강화 후에도 변하지 않아야 한다)
        int originalValue = strike.value;
        int originalUpgradedValue = strike.upgradedValue;
        int originalCost = strike.cost;

        // ===== A) 런 시작 =====
        cm.StartRun();
        Check("A. 런 덱 장수", cm.runDeck.Count, 10);
        Check("A. 시작 시 강화된 카드 수", CountUpgraded(cm.runDeck), 0);
        Check("A. 강화 가능한 카드 수(전부 가능)", cm.GetUpgradableCards().Count, 10);

        // ===== B) 같은 카드 3장 중 한 장만 강화 =====
        List<CardInstance> strikes = FindInstances(cm.runDeck, strike);
        Check("B. 런 덱의 타격 카드 수", strikes.Count, 3);
        if (strikes.Count < 3)
        {
            Debug.LogError("타격이 3장 필요하다. CardManager의 Starting Deck 구성을 확인하자.");
            return;
        }
        CardInstance first = strikes[0];
        CardInstance second = strikes[1];
        CardInstance third = strikes[2];

        CheckBool("B. 첫 번째 타격 강화 성공", cm.UpgradeCard(first), true);
        CheckBool("B. 첫 번째 타격은 강화됨", first.isUpgraded, true);
        CheckBool("B. 두 번째 타격은 그대로", second.isUpgraded, false);
        CheckBool("B. 세 번째 타격은 그대로", third.isUpgraded, false);
        Check("B. 런 덱의 강화된 카드 수(한 장만)", CountUpgraded(cm.runDeck), 1);

        // ===== C) 강화된 카드와 일반 카드의 값 차이 =====
        Check("C. 강화된 타격의 수치", first.Value, strike.upgradedValue);
        Check("C. 일반 타격의 수치", second.Value, strike.value);
        Check("C. 강화해도 비용 불변", first.Cost, strike.cost);
        CheckText("C. 강화된 카드 이름에 + 표시", first.Name, strike.cardName + "+");
        CheckText("C. 일반 카드 이름", second.Name, strike.cardName);

        // ===== D) 한 장당 강화는 1회만 =====
        CheckBool("D. 이미 강화한 카드는 다시 강화 불가", cm.UpgradeCard(first), false);
        Check("D. 다시 시도해도 수치 그대로", first.Value, strike.upgradedValue);

        // ===== E) 원본 CardData 에셋은 수정되지 않음 =====
        Check("E. 원본 타격 value 불변", strike.value, originalValue);
        Check("E. 원본 타격 upgradedValue 불변", strike.upgradedValue, originalUpgradedValue);
        Check("E. 원본 타격 cost 불변", strike.cost, originalCost);

        // ===== F) 강화 수치가 없는 카드(upgradedValue == value)는 강화 불가 =====
        CardData noUpgradeData = ScriptableObject.CreateInstance<CardData>();   // 테스트용 임시 카드 원본
        noUpgradeData.value = 5;
        noUpgradeData.upgradedValue = 5;
        CardInstance noUpgrade = new CardInstance(noUpgradeData);
        CheckBool("F. 강화 수치 없는 카드 - 강화 가능 여부", noUpgrade.CanUpgrade, false);
        CheckBool("F. 강화 수치 없는 카드 - 강화 시도 실패", cm.UpgradeCard(noUpgrade), false);
        CheckBool("F. 강화 수치 없는 카드 - 강화 상태 그대로", noUpgrade.isUpgraded, false);
        DestroyTemp(noUpgradeData);

        // ===== G) 전투가 시작되어도 강화 상태 유지 =====
        cm.StartBattle();
        Check("G. 전투 시작 후에도 런 덱 장수 유지", cm.runDeck.Count, 10);
        CheckBool("G. 전투 시작 후에도 강화 상태 유지", first.isUpgraded, true);
        CheckBool("G. 같은 카드 객체가 뽑기 더미에 들어 있음", cm.drawPile.Contains(first), true);
        Check("G. 뽑기 더미의 강화된 카드 수", CountUpgraded(cm.drawPile), 1);

        // ===== H) 강화된 카드가 실제 전투 효과에 반영됨 =====
        PlayerController p = player;
        p.InitRun();
        p.maxEnergy = 3;
        p.StartTurn();   // 에너지 3, 방어도 0
        Combatant dummy = new Combatant();
        dummy.Init(100);

        CardInstance guardCard = FindInstances(cm.runDeck, guard)[0];
        cm.UpgradeCard(guardCard);   // 수비 한 장 강화

        cm.hand.Clear();
        cm.hand.Add(first);       // 강화된 타격
        cm.hand.Add(second);      // 일반 타격
        cm.hand.Add(guardCard);   // 강화된 수비

        p.TryPlayCard(0, dummy);
        Check("H. 강화된 타격 피해", dummy.currentHp, 100 - strike.upgradedValue);
        p.TryPlayCard(0, dummy);
        Check("H. 일반 타격 피해", dummy.currentHp, 100 - strike.upgradedValue - strike.value);
        p.TryPlayCard(0, null);
        Check("H. 강화된 수비 방어도", p.combatant.block, guard.upgradedValue);
        Check("H. 카드 3장 사용 후 에너지", p.energy, 0);
        p.maxEnergy = originalMaxEnergy;

        // ===== I) 재정렬(특수 카드)의 강화 수치 (드로우 장수) =====
        CardInstance specialCard = FindInstances(cm.runDeck, special)[0];
        Check("I. 재정렬 기본 수치(드로우 장수)", specialCard.Value, special.value);
        CheckBool("I. 재정렬 강화 성공", cm.UpgradeCard(specialCard), true);
        Check("I. 재정렬 강화 수치", specialCard.Value, special.upgradedValue);
        Check("I. 재정렬 강화해도 비용 불변", specialCard.Cost, special.cost);

        // ===== J) 새 런을 시작하면 모두 초기화 =====
        cm.StartRun();
        Check("J. 새 런 - 강화된 카드 수 0", CountUpgraded(cm.runDeck), 0);
        Check("J. 새 런 - 런 덱 장수", cm.runDeck.Count, 10);
        CheckBool("J. 새 런 - 이전 런의 카드 객체는 사라짐", cm.runDeck.Contains(first), false);
        Check("J. 시작 덱 구성은 그대로", cm.startingDeck.Count, 10);

        // ===== K) 강화 가능한 카드 목록 =====
        Check("K. 새 런 - 강화 가능 카드 수", cm.GetUpgradableCards().Count, 10);
        cm.UpgradeCard(cm.runDeck[0]);
        Check("K. 한 장 강화 후 - 강화 가능 카드 수", cm.GetUpgradableCards().Count, 9);

        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // ---------------------------------------------------------------
    // 수동 테스트 (눈으로 확인하고 싶을 때, 위에서부터 순서대로 실행)
    // ---------------------------------------------------------------
    [ContextMenu("Manual 1: Start Run (런 덱 생성)")]
    public void ManualStartRun()
    {
        if (!FindReferences()) return;
        cardManager.StartRun();
        cardManager.LogRunDeck();
    }

    [ContextMenu("Manual 2: Upgrade One Random Card (무작위 한 장 강화)")]
    public void ManualUpgradeRandom()
    {
        if (!FindReferences()) return;
        List<CardInstance> candidates = cardManager.GetUpgradableCards();
        if (candidates.Count == 0)
        {
            Debug.Log("강화할 수 있는 카드가 없다.");
            return;
        }
        cardManager.UpgradeCard(candidates[Random.Range(0, candidates.Count)]);
        cardManager.LogRunDeck();   // 강화된 카드 이름 뒤에 +가 붙는다
    }

    [ContextMenu("Manual 3: Start Battle (강화 유지 확인)")]
    public void ManualStartBattle()
    {
        if (!FindReferences()) return;
        cardManager.StartBattle();
        cardManager.DrawCards(5);
        cardManager.LogState();   // 손패/더미에서도 강화된 카드는 +가 붙어 있어야 한다
    }

    [ContextMenu("Manual 4: Log Run Deck")]
    public void ManualLogRunDeck()
    {
        if (!FindReferences()) return;
        cardManager.LogRunDeck();
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    // 리스트에서 특정 원본 카드(CardData)로 만든 카드 한 장들을 모두 찾는다.
    private List<CardInstance> FindInstances(List<CardInstance> list, CardData data)
    {
        List<CardInstance> result = new List<CardInstance>();
        foreach (CardInstance card in list)
        {
            if (card.data == data)
            {
                result.Add(card);
            }
        }
        return result;
    }

    // 강화된 카드가 몇 장인지 센다.
    private int CountUpgraded(List<CardInstance> list)
    {
        int count = 0;
        foreach (CardInstance card in list)
        {
            if (card.isUpgraded)
            {
                count++;
            }
        }
        return count;
    }

    // 테스트용으로 임시 생성한 ScriptableObject를 지운다.
    private void DestroyTemp(Object obj)
    {
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
        if (cardManager == null) cardManager = GetComponent<CardManager>();
        if (cardManager == null)
        {
            Debug.LogError("CardManager를 연결하자.");
            return false;
        }
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("PlayerController를 연결하자.");
            return false;
        }
        if (player.cardManager != cardManager)
        {
            Debug.LogError("PlayerController의 Card Manager 칸이 이 테스트의 Card Manager와 다르다.");
            return false;
        }
        if (strike == null || guard == null || heavyStrike == null || quickJab == null || special == null)
        {
            Debug.LogError("카드 에셋 5개(strike, guard, heavyStrike, quickJab, special)를 모두 연결하자.");
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
