using UnityEngine;

/// <summary>
/// 카드 종류 4가지 (확정된 기획).
/// 새 종류가 필요해지면 여기에 한 줄 추가하면 된다.
/// </summary>
public enum CardType
{
    Attack,   // 공격: 적에게 피해
    Defend,   // 방어: 방어도 획득
    Buff,     // 강화: 버프 부여 (효과 구현은 4단계에서)
    Special   // 특수: 특수 효과 (효과 구현은 필요할 때 추가)
}

/// <summary>
/// 카드 한 장의 "데이터"만 담는 ScriptableObject.
/// Project 창 우클릭 → Create → Game → Card 로 카드 에셋을 만들 수 있다.
/// 수치는 코드가 아니라 인스펙터에서 바꾸므로 밸런스 조정이 쉽다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Card", fileName = "NewCard")]
public class CardData : ScriptableObject
{
    [Header("기본 정보")]
    public string cardName = "새 카드";   // 카드 이름

    [Range(0, 5)]                          // 인스펙터에서 슬라이더로 0~5만 선택 가능
    public int cost = 1;                   // 에너지 비용 (0~5)

    public CardType type = CardType.Attack; // 카드 종류

    [Header("효과 수치")]
    [Min(0)]
    public int value = 6;                  // 공격 카드면 피해량, 방어 카드면 방어도

    [TextArea]
    public string description = "";        // 설명 (지금은 메모 용도)
}
