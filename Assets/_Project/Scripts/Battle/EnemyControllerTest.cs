using UnityEngine;

/// <summary>
/// 1-E 단계 테스트용 스크립트. (검증이 끝나면 삭제해도 된다)
/// EnemyController와 같은 오브젝트에 붙이면 자동으로 찾는다.
/// 주의: 이 테스트는 1-B에서 만든 슬라임 패턴(공격 7 → 방어 5 → 공격 10, 체력 40)을 기준으로 한다.
/// </summary>
public class EnemyControllerTest : MonoBehaviour
{
    [Header("연결")]
    public EnemyController enemy;

    [Header("가짜 플레이어 (공격 대상, 인스펙터에서 체력 변화 확인용)")]
    public Combatant dummyPlayer = new Combatant();

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
        Debug.Log("===== EnemyController 자동 테스트 시작 =====");

        EnemyController e = enemy;
        dummyPlayer.Init(70);   // 가짜 플레이어: 체력 70, 방어도 0

        // A) 전투 시작: 체력 40, 방어도 0, 첫 행동 예고 = 공격 7
        e.InitBattle();
        Check("A. 전투 시작 - 적 체력", e.combatant.currentHp, 40);
        Check("A. 전투 시작 - 적 방어도", e.combatant.block, 0);
        CheckText("A. 전투 시작 - 행동 예고", e.nextActionPreview, "공격 7");

        // B) 1번째 행동: 예고한 대로 공격 7 → 플레이어 체력 70→63
        EnemyActionData previewed = e.NextAction;
        EnemyActionData executed = e.ExecuteAction(dummyPlayer);
        CheckBool("B. 예고한 행동과 실행한 행동이 같다", object.ReferenceEquals(previewed, executed), true);
        Check("B. 공격 7 - 플레이어 체력", dummyPlayer.currentHp, 63);
        CheckText("B. 다음 행동 예고", e.nextActionPreview, "방어 5");

        // C) 2번째 행동: 방어 5 → 적 방어도 5, 플레이어 체력 변화 없음
        previewed = e.NextAction;
        executed = e.ExecuteAction(dummyPlayer);
        CheckBool("C. 예고한 행동과 실행한 행동이 같다", object.ReferenceEquals(previewed, executed), true);
        Check("C. 방어 5 - 적 방어도", e.combatant.block, 5);
        Check("C. 방어 - 플레이어 체력 그대로", dummyPlayer.currentHp, 63);
        CheckText("C. 다음 행동 예고", e.nextActionPreview, "공격 10");

        // D) 플레이어 턴 동안 적의 방어도가 유지되어 공격을 막는다.
        //    플레이어가 8 피해를 주면 방어도 5가 막고, 체력은 3만 감소: 40→37
        e.combatant.TakeDamage(8);
        Check("D. 플레이어 공격 8 - 적 체력(방어도 5가 흡수)", e.combatant.currentHp, 37);
        Check("D. 플레이어 공격 8 - 적 방어도 소진", e.combatant.block, 0);

        // E) 3번째 행동: 공격 10 → 플레이어 체력 63→53, 패턴이 처음으로 순환
        previewed = e.NextAction;
        executed = e.ExecuteAction(dummyPlayer);
        CheckBool("E. 예고한 행동과 실행한 행동이 같다", object.ReferenceEquals(previewed, executed), true);
        Check("E. 공격 10 - 플레이어 체력", dummyPlayer.currentHp, 53);
        CheckText("E. 패턴 순환 - 다음 행동 예고", e.nextActionPreview, "공격 7");

        // F) 행동 직전에 방어도가 초기화되는지 확인
        //    방어도 9가 남아 있다고 가정하고 행동(공격 7)을 실행하면, 행동 시작 전에 방어도가 0이 된다.
        e.combatant.GainBlock(9);
        e.ExecuteAction(dummyPlayer);
        Check("F. 행동 직전 방어도 초기화", e.combatant.block, 0);
        Check("F. 공격 7 - 플레이어 체력", dummyPlayer.currentHp, 46);
        CheckText("F. 다음 행동 예고", e.nextActionPreview, "방어 5");

        // G) 죽은 적은 행동하지 않는다.
        e.combatant.TakeDamage(999);
        executed = e.ExecuteAction(dummyPlayer);
        CheckBool("G. 죽은 적은 행동하지 않는다 (null 반환)", executed == null, true);
        Check("G. 죽은 적 - 플레이어 체력 그대로", dummyPlayer.currentHp, 46);

        // H) 다시 전투를 시작하면 완전히 초기화
        e.InitBattle();
        Check("H. 재시작 - 적 체력", e.combatant.currentHp, 40);
        Check("H. 재시작 - 적 방어도", e.combatant.block, 0);
        CheckText("H. 재시작 - 행동 예고(처음부터)", e.nextActionPreview, "공격 7");

        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // ---------------------------------------------------------------
    // 수동 테스트 (눈으로 확인하고 싶을 때, 위에서부터 순서대로 실행)
    // ---------------------------------------------------------------
    [ContextMenu("Manual 1: Setup (적 등장 + 가짜 플레이어 체력 70)")]
    public void ManualSetup()
    {
        if (!FindReferences()) return;
        dummyPlayer.Init(70);
        enemy.InitBattle();
    }

    [ContextMenu("Manual 2: Enemy Acts (적 행동 실행)")]
    public void ManualEnemyActs()
    {
        if (!FindReferences()) return;
        enemy.ExecuteAction(dummyPlayer);
        enemy.LogState();
        Debug.Log("가짜 플레이어 체력 " + dummyPlayer.currentHp + " / 방어도 " + dummyPlayer.block);
    }

    [ContextMenu("Manual 3: Player Hits Enemy 8 (플레이어가 적을 8 공격)")]
    public void ManualPlayerHits()
    {
        if (!FindReferences()) return;
        int hpLost = enemy.combatant.TakeDamage(8);
        Debug.Log("플레이어 공격 8 → 적 체력 -" + hpLost);
        enemy.LogState();
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    private bool FindReferences()
    {
        if (enemy == null) enemy = GetComponent<EnemyController>();
        if (enemy == null)
        {
            Debug.LogError("EnemyController를 찾을 수 없다. 같은 오브젝트에 붙이거나 인스펙터에 연결하자.");
            return false;
        }
        if (enemy.data == null)
        {
            Debug.LogError("EnemyController의 Data 칸에 EnemyData(Slime) 에셋을 연결하자.");
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
