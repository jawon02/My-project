using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 더미 관리자: 전체 덱 / 뽑기 더미 / 손패 / 버림 더미를 관리한다.
/// 빈 오브젝트에 붙여서 사용한다.
/// 이 클래스는 "카드를 어디로 옮길지"만 담당하고, 카드 효과(공격/방어)는 다루지 않는다.
/// </summary>
public class CardManager : MonoBehaviour
{
    [Header("설정")]
    public int maxHandSize = 10;   // 손패 최대 장수 (명세: 10장)

    [Header("전체 덱 (인스펙터에서 카드 에셋을 넣는다)")]
    public List<CardData> deck = new List<CardData>();

    [Header("전투 중 카드 더미 (확인용 - 직접 수정하지 말 것)")]
    public List<CardData> drawPile = new List<CardData>();     // 뽑기 더미 (맨 끝 카드가 맨 위)
    public List<CardData> hand = new List<CardData>();         // 손패
    public List<CardData> discardPile = new List<CardData>();  // 버림 더미

    /// <summary>뽑기 + 손패 + 버림 더미의 카드 합계. 전투 중에는 항상 덱 장수와 같아야 정상이다.</summary>
    public int TotalCardCount
    {
        get { return drawPile.Count + hand.Count + discardPile.Count; }
    }

    // ---------------------------------------------------------------
    // 전투 시작
    // ---------------------------------------------------------------

    /// <summary>
    /// 전투 시작 시 호출. 순서(확정):
    /// 1) 전체 덱을 뽑기 더미로 이동  2) 뽑기 더미 셔플  3) 손패 초기화  4) 버림 더미 초기화
    /// </summary>
    public void StartBattle()
    {
        drawPile.Clear();
        drawPile.AddRange(deck);   // 덱의 복사본을 뽑기 더미로 (원본 덱은 그대로 유지)
        Shuffle(drawPile);
        hand.Clear();
        discardPile.Clear();
        Debug.Log("[CardManager] 전투 시작 - 덱 " + deck.Count + "장을 섞었다.");
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
        List<CardData> overflowCards = new List<CardData>();

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
            CardData card = drawPile[lastIndex];
            drawPile.RemoveAt(lastIndex);
            drawnCount++;

            if (hand.Count < maxHandSize)
            {
                hand.Add(card);   // 손패에 자리가 있으면 손패로
            }
            else
            {
                overflowCards.Add(card);   // 손패가 가득 차면 초과 카드로 따로 모아둠
                Debug.Log("[CardManager] 손패가 가득 차서 '" + card.cardName + "' 카드가 버림 더미로 갔다.");
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
    public CardData DiscardFromHand(int handIndex)
    {
        if (handIndex < 0 || handIndex >= hand.Count)
        {
            Debug.LogWarning("[CardManager] 손패에 " + handIndex + "번 카드가 없다.");
            return null;
        }

        CardData card = hand[handIndex];
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
    private void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);   // 0 ~ i 사이의 무작위 위치
            CardData temp = list[i];          // i번과 j번 카드를 맞바꾼다
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

    private string CardListToText(List<CardData> list)
    {
        if (list.Count == 0)
        {
            return "(없음)";
        }
        string text = "";
        foreach (CardData card in list)
        {
            text += card.cardName + ", ";
        }
        return text;
    }
}
