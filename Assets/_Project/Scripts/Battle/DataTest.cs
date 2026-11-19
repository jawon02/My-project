using UnityEngine;

/// <summary>
/// 1-B 단계 테스트용 스크립트. (검증이 끝나면 삭제해도 된다)
/// 인스펙터에 카드 에셋들과 적 에셋을 끌어다 놓고 [ContextMenu]로 실행한다.
/// </summary>
public class DataTest : MonoBehaviour
{
    public CardData[] cards;   // 테스트할 카드 에셋들
    public EnemyData enemy;    // 테스트할 적 에셋

    private int passCount = 0;
    private int failCount = 0;

    [ContextMenu("Run Data Test")]
    public void RunDataTest()
    {
        passCount = 0;
        failCount = 0;
        Debug.Log("===== 데이터 테스트 시작 =====");

        TestCards();
        TestEnemy();

        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // 카드 에셋 검사: 정보를 출력하고, 비용 범위와 수치가 올바른지 확인
    private void TestCards()
    {
        if (cards == null || cards.Length == 0)
        {
            Check("카드가 1장 이상 등록되어 있다", false);
            return;
        }

        foreach (CardData card in cards)
        {
            if (card == null)
            {
                Check("카드 슬롯이 비어 있지 않다", false);
                continue;
            }

            Debug.Log("[카드] " + card.cardName + " / 비용 " + card.cost + " / 종류 " + card.type + " / 수치 " + card.value);
            Check(card.cardName + ": 비용이 0~5 범위", card.cost >= 0 && card.cost <= 5);
            Check(card.cardName + ": 수치가 0 이상", card.value >= 0);
        }
    }

    // 적 에셋 검사: 체력, 행동 패턴, 순환 동작 확인
    private void TestEnemy()
    {
        if (enemy == null)
        {
            Check("적이 등록되어 있다", false);
            return;
        }

        Debug.Log("[적] " + enemy.enemyName + " / 최대 체력 " + enemy.maxHp + " / 패턴 " + enemy.actionPattern.Count + "개");
        Check(enemy.enemyName + ": 최대 체력이 1 이상", enemy.maxHp >= 1);
        Check(enemy.enemyName + ": 행동 패턴이 1개 이상", enemy.actionPattern.Count >= 1);

        if (enemy.actionPattern.Count == 0)
        {
            return;
        }

        // 처음 6턴의 행동 예고를 출력해서 순환되는지 눈으로 확인
        string log = "[적 행동 예고] ";
        for (int i = 0; i < 6; i++)
        {
            log += (i + 1) + "턴: " + enemy.GetAction(i).GetPreviewText() + "  ";
        }
        Debug.Log(log);

        // 첫 행동 확인, 패턴 한 바퀴 뒤에 처음으로 돌아오는지 확인
        Check("0번째 턴 행동 = 패턴의 첫 번째", enemy.GetAction(0) == enemy.actionPattern[0]);
        Check("패턴 한 바퀴 뒤에 처음 행동으로 순환", enemy.GetAction(enemy.actionPattern.Count) == enemy.actionPattern[0]);
    }

    // 조건이 참이면 PASS, 거짓이면 FAIL을 출력하는 도우미 함수
    private void Check(string name, bool condition)
    {
        if (condition)
        {
            passCount++;
            Debug.Log("PASS  " + name);
        }
        else
        {
            failCount++;
            Debug.LogError("FAIL  " + name);
        }
    }
}
