using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적이 할 수 있는 행동 종류.
/// 주의: 새 종류는 반드시 맨 뒤에 추가한다. (에셋에는 순서 번호로 저장되므로 중간에 넣으면 기존 적 데이터가 바뀐다)
/// </summary>
public enum EnemyActionType
{
    Attack,     // 플레이어에게 피해
    Defend,     // 자신의 방어도 획득
    SelfBuff,   // 자기 자신에게 상태 효과 (Phase 4-B)
    AllyBuff,   // 자신을 제외한 살아 있는 아군 중 가장 왼쪽에게 상태 효과 (Phase 4-B)
    AllyHeal    // 자신을 제외한 살아 있는 아군 중 체력이 줄어든 가장 왼쪽 아군 회복 (Phase 4-B)
}

/// <summary>
/// 적의 행동 하나 = 종류 + 수치 (+ 강화 행동이면 효과 종류와 지속시간).
/// 예) 공격 7, 방어 5, 자기 강화 공격력 +2 (2턴), 아군 회복 6
/// [System.Serializable] 덕분에 EnemyData 인스펙터에서 리스트로 편집할 수 있다.
/// </summary>
[System.Serializable]
public class EnemyActionData
{
    public EnemyActionType type = EnemyActionType.Attack;
    [Min(0)]
    public int value = 5;   // 공격: 피해 / 방어: 방어도 / SelfBuff·AllyBuff: 효과 수치 / AllyHeal: 회복량

    [Header("SelfBuff·AllyBuff 전용")]
    public StatusEffectType statusEffect = StatusEffectType.None;   // 걸어 줄 상태 효과 (예: 공격력 증가)
    [Min(0)]
    public int duration = 0;                                        // 지속시간(턴)

    /// <summary>행동 예고용 문장. 예: "공격 7" (예고와 실행이 같은 데이터를 쓰므로 항상 일치한다)</summary>
    public string GetPreviewText()
    {
        switch (type)
        {
            case EnemyActionType.Attack: return "공격 " + value;
            case EnemyActionType.Defend: return "방어 " + value;
            default: return type + " " + value;
        }
    }
}

/// <summary>
/// 적 한 종류의 "데이터"만 담는 ScriptableObject.
/// Project 창 우클릭 → Create → Game → Enemy 로 만든다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Enemy", fileName = "NewEnemy")]
public class EnemyData : ScriptableObject
{
    [Header("기본 정보")]
    public string enemyName = "새 적";
    [Min(1)]
    public int maxHp = 40;

    [Header("행동 패턴 (위에서부터 순서대로 반복)")]
    public List<EnemyActionData> actionPattern = new List<EnemyActionData>();

    /// <summary>
    /// 몇 번째 적 턴인지(0부터 시작)를 넣으면 그 턴의 행동을 돌려준다.
    /// 패턴 끝까지 가면 처음으로 돌아간다. 예) 패턴 3개: 0,1,2,0,1,2...
    /// 패턴이 비어 있으면 null을 반환한다.
    /// </summary>
    public EnemyActionData GetAction(int turnIndex)
    {
        if (actionPattern == null || actionPattern.Count == 0)
        {
            return null;
        }
        return actionPattern[turnIndex % actionPattern.Count]; // % = 나머지 연산 (순환용)
    }
}
