using UnityEngine;

/// <summary>
/// 플레이어와 적이 함께 쓰는 "체력 + 방어도" 계산 클래스.
/// MonoBehaviour가 아닌 일반 클래스라서 오브젝트에 직접 붙이지 않고,
/// PlayerController / EnemyController 안에 변수로 넣어서 사용한다.
/// [System.Serializable] 덕분에 인스펙터 창에서 값을 바로 볼 수 있다.
/// </summary>
[System.Serializable]
public class Combatant
{
    [Header("체력")]
    public int maxHp = 70;       // 최대 체력
    public int currentHp = 70;   // 현재 체력

    [Header("방어도")]
    public int block = 0;        // 현재 방어도 (전투 시작 시 0)

    /// <summary>체력이 0 이하이면 true (죽음)</summary>
    public bool IsDead
    {
        get { return currentHp <= 0; }
    }

    /// <summary>
    /// 전투 시작 전에 호출. 최대 체력을 정하고 체력을 가득 채우며 방어도를 0으로 만든다.
    /// </summary>
    public void Init(int newMaxHp)
    {
        maxHp = newMaxHp;
        currentHp = newMaxHp;
        block = 0;
    }

    /// <summary>
    /// 피해를 받는다. 규칙: 방어도가 먼저 깎이고, 막지 못한 피해만 체력에 적용된다.
    /// 반환값 = 실제로 체력이 깎인 양 (방어도로 전부 막으면 0)
    /// </summary>
    public int TakeDamage(int amount)
    {
        // 0 이하의 피해는 무시 (회복 효과로 쓰이는 실수 방지)
        if (amount <= 0)
        {
            return 0;
        }

        // 1) 방어도가 흡수할 수 있는 양 = 방어도와 피해 중 작은 쪽
        int absorbed = Mathf.Min(block, amount);
        block -= absorbed;

        // 2) 남은 피해만 체력에 적용
        int hpDamage = amount - absorbed;
        currentHp = Mathf.Max(0, currentHp - hpDamage); // 체력은 0 아래로 내려가지 않음

        return hpDamage;
    }

    /// <summary>방어도를 얻는다. (0 이하의 값은 무시)</summary>
    public void GainBlock(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        block += amount;
    }

    /// <summary>
    /// 방어도를 0으로 만든다.
    /// 플레이어는 "다음 플레이어 턴이 시작될 때" 호출한다. (확정된 규칙)
    /// </summary>
    public void ResetBlock()
    {
        block = 0;
    }

    /// <summary>체력을 회복한다. 최대 체력을 넘을 수 없다.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }
}
