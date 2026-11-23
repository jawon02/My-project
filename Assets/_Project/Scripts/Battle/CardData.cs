using UnityEngine;

/// <summary>
/// 카드 종류 4가지 (확정된 기획).
/// 새 종류가 필요해지면 여기에 한 줄 추가하면 된다.
/// </summary>
public enum CardType
{
    Attack,   // 공격: 적에게 피해
    Defend,   // 방어: 방어도 획득
    Buff,     // 강화: 상태 효과 부여 (Phase 4)
    Special   // 특수: 지금은 재정렬(value만큼 드로우)만 있다 (Phase 4)
}

/// <summary>
/// 카드의 대상 유형 4가지 (Phase 3 확정).
/// 카드마다 인스펙터에서 직접 지정한다.
/// </summary>
public enum TargetType
{
    SingleEnemy,   // 단일: 플레이어가 고른 적 1명 (←/→/Tab으로 선택)
    AllEnemies,    // 전체: 살아 있는 모든 적 (왼쪽부터 차례대로)
    RandomEnemy,   // 무작위: 살아 있는 적 중 1명을 카드 사용 순간에 무작위로
    Self           // 자신: 플레이어 자신 (방어·자기 강화 카드용. 공격 카드에는 쓸 수 없다)
}

/// <summary>
/// 카드 한 장의 "데이터"만 담는 ScriptableObject.
/// 
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

    public TargetType targetType = TargetType.SingleEnemy; // 대상 유형 (방어 카드는 Self로 바꿔야 한다)

    [Header("효과 수치")]
    [Min(0)]
    public int value = 6;                  // 공격 카드면 피해량, 방어 카드면 방어도, Buff 카드면 효과 수치 (재정렬은 드로우 장수)

    [Min(0)]
    public int upgradedValue = 0;          // 강화했을 때의 수치 (카드마다 개별 설정)
                                           // value보다 커야 강화 가능. 강화가 없는 카드는 value와 같게 둔다.
                                           // 기본값 0이라서, 강화가 필요한 카드는 인스펙터에서 직접 채워야 한다.

    [Header("Buff 카드 전용 (다른 종류의 카드는 그대로 둔다)")]
    public StatusEffectType statusEffect = StatusEffectType.None;   // 걸어 줄 상태 효과
                                           // 효과 수치는 위의 value(강화 시 upgradedValue)를 쓴다.
                                           // 감소 효과(약화 등)도 value는 양수로 넣는다. 예) 공격력 감소 2 → value 2

    [Min(0)]
    public int duration = 0;               // 지속시간(턴). 강화해도 늘어나지 않는다.

    [TextArea]
    public string description = "";        // 설명 (지금은 메모 용도)
}
