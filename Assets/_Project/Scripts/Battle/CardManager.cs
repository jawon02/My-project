using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 카드 더미 관리자: 시작 덱 / 런 덱 / 뽑기 더미 / 손패 / 버림 더미를 관리한다.
/// 빈 오브젝트에 붙여서 사용한다.
/// 이 클래스는 "카드를 어디로 옮길지"와 "카드 강화"만 담당하고, 카드 효과(공격/방어)는 다루지 않는다.
///
/// 덱이 두 종류다:
///  - startingDeck : 인스펙터에서 정하는 "기본 덱 구성" (카드 원본 에셋 목록)
///  - runDeck      : 이번 런에서 실제로 쓰는 카드 한 장 한 장(CardInstance) 목록. 강화 상태가 여기에 저장된다.
/// 전투가 끝나도 runDeck은 그대로라서 강화가 이어지고, 새 런을 시작하면 startingDeck으로 다시 만든다.
/// </summary>
public class CardManager : MonoBehaviour
{
    [Header("설정")]
    public int maxHandSize = 10;   // 손패 최대 장수 (명세: 10장)

    [Header("기본 덱 구성 (인스펙터에서 카드 에셋을 넣는다)")]
    [FormerlySerializedAs("deck")]   // 예전 이름(deck)으로 넣어 둔 카드 목록이 사라지지 않게 해 준다
    public List<CardData> startingDeck = new List<CardData>();

    [Header("현재 런의 덱 (확인용 - 직접 수정하지 말 것)")]
    public List<CardInstance> runDeck = new List<CardInstance>();

    [Header("전투 중 카드 더미 (확인용 - 직접 수정하지 말 것)")]
    public List<CardInstance> drawPile = new List<CardInstance>();     // 뽑기 더미 (맨 끝 카드가 맨 위)
    public List<CardInstance> hand = new List<CardInstance>();         // 손패
    public List<CardInstance> discardPile = new List<CardInstance>();  // 버림 더미

    /// <summary>뽑기 + 손패 + 버림 더미의 카드 합계. 전투 중에는 항상 런 덱 장수와 같아야 정상이다.</summary>
    public int TotalCardCount
    {
        get { return drawPile.Count + hand.Count + discardPile.Count; }
    }

    // ---------------------------------------------------------------
    // 런 시작
    // ---------------------------------------------------------------

    /// <summary>
    /// 새 런을 시작할 때 호출. startingDeck의 카드 한 장 한 장을 CardInstance(미강화)로 만들어 runDeck을 새로 채운다.
    /// 이전 런의 강화 상태는 모두 사라진다. 전투 중 더미도 함께 비운다.
    /// </summary>
    public void StartRun()
    {
        runDeck.Clear();
        foreach (CardData data in startingDeck)
        {
            if (data == null)
            {
                continue;   // 인스펙터에서 빈 칸이 있으면 건너뜀
            }
            runDeck.Add(new CardInstance(data));
        }

        drawPile.Clear();
        hand.Clear();
        discardPile.Clear();
        Debug.Log("[CardManager] 런 시작 - 런 덱 " + runDeck.Count + "장을 만들었다.");
    }

    // ---------------------------------------------------------------
    // 카드 강화
    // ---------------------------------------------------------------

    /// <summary>
    /// 카드 한 장(CardInstance)을 강화한다. 성공하면 true.
    /// 이미 강화했거나 강화가 불가능한 카드는 false. (카드 한 장당 강화 1회)
    /// 나중에 휴식 노드에서 이 함수를 호출하게 된다.
    /// </summary>
    public bool UpgradeCard(CardInstance card)
    {
        if (card == null)
        {
            return false;
        }

        bool success = card.Upgrade();
        if (success)
        {
            Debug.Log("[CardManager] 카드 강화: " + card.Name + " (수치 " + card.data.value + " → " + card.Value + ")");
        }
        else
        {
            Debug.Log("[CardManager] '" + card.Name + "'은(는) 강화할 수 없다. (이미 강화했거나 강화 수치가 없음)");
        }
        return success;
    }

    /// <summary>런 덱에서 아직 강화할 수 있는 카드들을 돌려준다. (휴식 노드의 선택지용)</summary>
    public List<CardInstance> GetUpgradableCards()
    {
        List<CardInstance> result = new List<CardInstance>();
        foreach (CardInstance card in runDeck)
        {
            if (card.CanUpgrade)
            {
                result.Add(card);
            }
        }
        return result;
    }

    // ---------------------------------------------------------------
    // 전투 시작
    // ---------------------------------------------------------------

    /// <summary>
    /// 전투 시작 시 호출. 순서(확정):
    /// 1) 런 덱 전체를 뽑기 더미로 이동  2) 뽑기 더미 셔플  3) 손패 초기화  4) 버림 더미 초기화
    /// 런 덱의 카드(CardInstance)는 그대로 옮기므로 강화 상태가 유지된다.
    /// </summary>
    public void StartBattle()
    {
        // 안전장치: 런 시작을 깜빡했다면 여기서 런 덱을 만든다.
        if (runDeck.Count == 0)
        {
            StartRun();
        }

        drawPile.Clear();
        drawPile.AddRange(runDeck);   // 카드 한 장 한 장(같은 CardInstance)을 그대로 옮긴다
        Shuffle(drawPile);
        hand.Clear();
        discardPile.Clear();
        Debug.Log("[CardManager] 전투 시작 - 런 덱 " + runDeck.Count + "장을 섞었다.");
    }

    // ---------------------------------------------------------------
    // 드로우
    // ---------------------------------------------------------------

    /// <summary>
    /// 카드를 count장 뽑는다. 반환값 = 뽑기 더미에서 실제로 꺼낸 장수.
    /// 규칙:
    /// - 뽑기 더미가 비면 버림 더미를 즉시 셔플해 새 뽑기 더미를 만들고 이어서 뽑는다.
    /// - 뽑기 더미와 버림 더미가 모두 비면 드로우를 중단한다.
    /// - 손패가 가득 찬 상태에서 뽑은 카드는 손패에 못 들어가고 버림 더미로 간다. (뽑기 더미에서는 소모됨)
    /// </summary>
    public int DrawCards(int count)
    {
        int drawnCount = 0;

        // 손패에 못 들어간 카드는 드로우가 끝난 뒤에 버림 더미로 보낸다.
        // (드로우 도중 재셔플될 때 이 카드가 다시 뽑기 더미로 섞여 들어오지 않게 하려는 장치)
        List<CardInstance> overflowCards = new List<CardInstance>();

        for (int i = 0; i < count; i++)
        {
            // 뽑기 더미가 비었으면 버림 더미를 섞어서 채운다.
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0)
                {
                    Debug.Log("[CardManager] 뽑기 더미와 버림 더미가 모두 비어 드로우를 중단한다.");
                    break;
                }
                ReshuffleDiscardIntoDrawPile();
            }

            // 뽑기 더미의 맨 위(리스트의 마지막) 카드를 꺼낸다.
            int lastIndex = drawPile.Count - 1;
            CardInstance card = drawPile[lastIndex];
            drawPile.RemoveAt(lastIndex);
            drawnCount++;

            if (hand.Count < maxHandSize)
            {
                hand.Add(card);   // 손패에 자리가 있으면 손패로
            }
            else
            {
                overflowCards.Add(card);   // 손패가 가득 차면 초과 카드로 따로 모아둠
                Debug.Log("[CardManager] 손패가 가득 차서 '" + card.Name + "' 카드가 버림 더미로 갔다.");
            }
        }

        // 초과 카드를 버림 더미로 이동
        discardPile.AddRange(overflowCards);

        return drawnCount;
    }

    /// <summary>버림 더미를 전부 뽑기 더미로 옮기고 섞는다.</summary>
    private void ReshuffleDiscardIntoDrawPile()
    {
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Shuffle(drawPile);
        Debug.Log("[CardManager] 버림 더미를 섞어 새 뽑기 더미를 만들었다. (" + drawPile.Count + "장)");
    }

    // ---------------------------------------------------------------
    // 버리기
    // ---------------------------------------------------------------

    /// <summary>
    /// 손패의 handIndex번째 카드를 버림 더미로 옮기고 그 카드를 돌려준다.
    /// (카드 사용 후 처리용. 소멸 시스템은 없으므로 모든 카드는 버림 더미로 간다.)
    /// 잘못된 번호면 null을 돌려준다.
    /// </summary>
    public CardInstance DiscardFromHand(int handIndex)
    {
        if (handIndex < 0 || handIndex >= hand.Count)
        {
            Debug.LogWarning("[CardManager] 손패에 " + handIndex + "번 카드가 없다.");
            return null;
        }

        CardInstance card = hand[handIndex];
        hand.RemoveAt(handIndex);
        discardPile.Add(card);
        return card;
    }

    /// <summary>손패를 전부 버림 더미로 옮긴다. (플레이어 턴 종료 시 사용)</summary>
    public void DiscardAllHand()
    {
        discardPile.AddRange(hand);
        hand.Clear();
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    /// <summary>리스트를 무작위로 섞는다 (피셔-예이츠 셔플).</summary>
    private void Shuffle(List<CardInstance> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);   // 0 ~ i 사이의 무작위 위치
            CardInstance temp = list[i];      // i번과 j번 카드를 맞바꾼다
            list[i] = list[j];
            list[j] = temp;
        }
    }

    /// <summary>현재 상태를 콘솔에 출력한다. (테스트/디버그용)</summary>
    public void LogState()
    {
        Debug.Log("[상태] 뽑기 " + drawPile.Count + " / 손패 " + hand.Count + " / 버림 " + discardPile.Count
                  + "\n손패: " + CardListToText(hand)
                  + "\n뽑기 더미(맨 위가 오른쪽 끝): " + CardListToText(drawPile)
                  + "\n버림 더미: " + CardListToText(discardPile));
    }

    /// <summary>런 덱의 카드를 강화 여부(+)와 함께 콘솔에 출력한다. (테스트/디버그용)</summary>
    public void LogRunDeck()
    {
        Debug.Log("[런 덱 " + runDeck.Count + "장] " + CardListToText(runDeck));
    }

    private string CardListToText(List<CardInstance> list)
    {
        if (list.Count == 0)
        {
            return "(없음)";
        }
        string text = "";
        foreach (CardInstance card in list)
        {
            text += card.Name + ", ";
        }
        return text;
    }
}
