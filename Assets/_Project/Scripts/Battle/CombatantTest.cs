using UnityEngine;

/// <summary>
/// 1-A 단계 테스트용 스크립트. (Combatant 검증이 끝나면 삭제해도 된다)
/// 빈 오브젝트에 붙인 뒤, 인스펙터에서 이 컴포넌트 이름 옆 점 세 개(⋮) 메뉴 또는
/// 컴포넌트 이름을 우클릭하면 [ContextMenu]로 만든 메뉴가 보인다.
/// </summary>
public class CombatantTest : MonoBehaviour
{
    // 인스펙터에서 체력/방어도 값이 어떻게 변하는지 실시간으로 볼 수 있다.
    public Combatant combatant = new Combatant();

    // 테스트 통과/실패 횟수 집계용
    private int passCount = 0;
    private int failCount = 0;

    /// <summary>자동 테스트: 정해진 시나리오를 실행하고 PASS/FAIL을 콘솔에 출력</summary>
    [ContextMenu("Run Auto Test")]
    public void RunAutoTest()
    {
        passCount = 0;
        failCount = 0;
        Debug.Log("===== Combatant 자동 테스트 시작 =====");

        // 시나리오 1) 초기화: 최대 체력 70이면 체력 70, 방어도 0
        combatant.Init(70);
        Check("초기 체력", combatant.currentHp, 70);
        Check("초기 방어도", combatant.block, 0);

        // 시나리오 2) 방어도 5를 얻고 8 피해 -> 방어도 0, 체력 3 감소(67)
        combatant.GainBlock(5);
        int hpLost = combatant.TakeDamage(8);
        Check("방어도 5 + 피해 8 -> 실제 체력 피해", hpLost, 3);
        Check("방어도 5 + 피해 8 -> 체력", combatant.currentHp, 67);
        Check("방어도 5 + 피해 8 -> 방어도", combatant.block, 0);

        // 시나리오 3) 방어도 없이 10 피해 -> 체력 57
        combatant.TakeDamage(10);
        Check("방어도 없이 피해 10 -> 체력", combatant.currentHp, 57);

        // 시나리오 4) 방어도 10을 얻고 4 피해 -> 방어도 6 남고 체력 그대로
        combatant.GainBlock(10);
        hpLost = combatant.TakeDamage(4);
        Check("방어도 10 + 피해 4 -> 실제 체력 피해", hpLost, 0);
        Check("방어도 10 + 피해 4 -> 체력", combatant.currentHp, 57);
        Check("방어도 10 + 피해 4 -> 남은 방어도", combatant.block, 6);

        // 시나리오 5) 방어도 초기화
        combatant.ResetBlock();
        Check("방어도 초기화", combatant.block, 0);

        // 시나리오 6) 회복은 최대 체력을 넘지 않음
        combatant.Heal(100);
        Check("과회복 -> 체력은 최대치", combatant.currentHp, 70);

        // 시나리오 7) 0 이하 피해/회복/방어도는 무시
        combatant.TakeDamage(-5);
        combatant.GainBlock(-5);
        Check("음수 피해 무시 -> 체력", combatant.currentHp, 70);
        Check("음수 방어도 무시 -> 방어도", combatant.block, 0);

        // 시나리오 8) 치명 피해 -> 체력 0, 죽음 판정
        combatant.TakeDamage(999);
        Check("치명 피해 -> 체력은 0 아래로 내려가지 않음", combatant.currentHp, 0);
        Check("치명 피해 -> 죽음 판정(1=true)", combatant.IsDead ? 1 : 0, 1);

        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    /// <summary>수동 테스트용: 피해 7 받기 (여러 번 눌러서 인스펙터 값 변화를 관찰)</summary>
    [ContextMenu("Manual: Take 7 Damage")]
    public void ManualDamage()
    {
        int hpLost = combatant.TakeDamage(7);
        Debug.Log("피해 7 -> 체력 감소 " + hpLost + " / 현재 체력 " + combatant.currentHp + " / 방어도 " + combatant.block);
    }

    /// <summary>수동 테스트용: 방어도 5 얻기</summary>
    [ContextMenu("Manual: Gain 5 Block")]
    public void ManualBlock()
    {
        combatant.GainBlock(5);
        Debug.Log("방어도 +5 -> 현재 방어도 " + combatant.block);
    }

    // 기대값과 실제값을 비교해서 PASS / FAIL 로그를 출력하는 도우미 함수
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
