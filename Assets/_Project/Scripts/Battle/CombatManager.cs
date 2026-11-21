using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;   // 새 Input System을 쓰는 프로젝트용 (Unity 6 기본값)
#endif

/// <summary>
/// 전투의 현재 상태.
/// </summary>
public enum BattleState
{
    NotStarted,   // 전투 시작 전
    PlayerTurn,   // 플레이어 턴 (카드를 낼 수 있음)
    EnemyTurn,    // 적 턴 (적이 순서대로 행동 중)
    Victory,      // 승리 (모든 적 처치)
    Defeat        // 패배 (플레이어 체력 0 이하)
}

/// <summary>
/// 전투 관리자: 지금까지 만든 PlayerController / EnemyController / CardManager를 연결해서
/// 턴 루프, 사망 처리, 승리/패배 판정을 담당한다.
/// 빈 오브젝트 "CombatManager"에 붙여서 사용한다. (적이나 플레이어 오브젝트에 붙이지 말 것!)
///
/// 턴 진행 순서 (확정):
///  플레이어 턴 시작 → 방어도 초기화 → 에너지 회복 → 5장 드로우 → 카드 사용 → 플레이어 턴 종료(수동)
///  → 남은 손패 버리기 → 적 턴 시작 → 적이 리스트 순서대로 행동 → 다음 플레이어 턴 시작
/// </summary>
public class CombatManager : MonoBehaviour
{
    [Header("연결")]
    public PlayerController player;
    public CardManager cardManager;                                   // 비워두면 player의 CardManager를 사용
    public List<EnemyController> enemies = new List<EnemyController>(); // 리스트 순서 = 화면 왼쪽부터 (행동 순서)

    [Header("설정")]
    public int drawPerTurn = 5;        // 턴 시작 드로우 장수 (명세: 5장)
    public bool startOnPlay = true;    // 플레이 버튼을 누르면 자동으로 런 + 전투를 시작할지

    [Header("현재 상태 (확인용)")]
    public BattleState state = BattleState.NotStarted;
    public int turnCount = 0;                                          // 플레이어 턴 횟수
    public List<EnemyController> livingEnemies = new List<EnemyController>(); // 살아 있는 적 (순서 유지)

    /// <summary>전투가 끝났는지 (승리 또는 패배)</summary>
    public bool IsBattleOver
    {
        get { return state == BattleState.Victory || state == BattleState.Defeat; }
    }

    // ---------------------------------------------------------------
    // Unity 기본 함수
    // ---------------------------------------------------------------

    private void Start()
    {
        // 플레이 모드 진입 시 자동 시작 (인스펙터에서 끌 수 있다)
        if (startOnPlay && CheckReferences())
        {
            player.InitRun();   // 런 시작: 플레이어 체력을 최대로
            StartBattle();
        }
    }

    private void Update()
    {
        // 키보드 입력은 플레이어 턴에만 받는다. (Game 창을 한 번 클릭해서 포커스를 줘야 입력됨)
        if (state != BattleState.PlayerTurn)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        // 새 Input System: 숫자키 1~9 = 손패 1~9번째 카드, 0 = 10번째 카드, 스페이스 = 턴 종료
        Keyboard kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }
        for (int i = 0; i < 10; i++)
        {
            Key key = (i < 9) ? (Key)((int)Key.Digit1 + i) : Key.Digit0;
            if (kb[key].wasPressedThisFrame)
            {
                PlayCard(i);
                return;   // 한 프레임에 한 번만 처리
            }
        }
        if (kb.spaceKey.wasPressedThisFrame)
        {
            EndPlayerTurn();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        // 예전 Input Manager 방식
        for (int i = 0; i < 10; i++)
        {
            KeyCode key = (i < 9) ? (KeyCode)((int)KeyCode.Alpha1 + i) : KeyCode.Alpha0;
            if (Input.GetKeyDown(key))
            {
                PlayCard(i);
                return;
            }
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            EndPlayerTurn();
        }
#endif
    }

    // ---------------------------------------------------------------
    // 전투 시작
    // ---------------------------------------------------------------

    /// <summary>
    /// 전투를 시작한다. (플레이어 체력은 건드리지 않는다. 런 시작은 player.InitRun()으로 따로 한다.)
    /// 플레이어가 선공이므로 바로 플레이어 턴이 시작된다.
    /// </summary>
    public void StartBattle()
    {
        if (!CheckReferences())
        {
            return;
        }

        turnCount = 0;
        player.StartBattle();        // 방어도 0
        cardManager.StartBattle();   // 덱 섞기, 손패/버림 더미 초기화

        // 적 등록: 이전 전투에서 숨겨진 적도 다시 켜고 초기화한다.
        livingEnemies.Clear();
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null)
            {
                continue;
            }
            enemy.gameObject.SetActive(true);
            enemy.InitBattle();
            livingEnemies.Add(enemy);   // 리스트 순서 그대로 (= 왼쪽부터)
        }

        if (livingEnemies.Count == 0)
        {
            Debug.LogError("[Combat] 등록된 적이 없다. Enemies 리스트에 적을 넣자.");
            return;
        }

        Debug.Log("===== 전투 시작 (적 " + livingEnemies.Count + "마리) =====");
        StartPlayerTurn();
    }

    // ---------------------------------------------------------------
    // 플레이어 턴
    // ---------------------------------------------------------------

    /// <summary>
    /// 플레이어 턴을 시작한다. 순서: 방어도 초기화 → 에너지 회복(player.StartTurn) → 카드 드로우.
    /// </summary>
    private void StartPlayerTurn()
    {
        turnCount++;
        state = BattleState.PlayerTurn;
        Debug.Log("----- 플레이어 턴 " + turnCount + " 시작 -----");

        player.StartTurn();                      // 방어도 초기화 + 에너지 회복
        cardManager.DrawCards(drawPerTurn);      // 카드 드로우

        LogBattleState();
    }

    /// <summary>
    /// 손패의 handIndex번째 카드를 사용한다. (0부터 시작) 성공하면 true.
    /// 공격 카드의 대상은 "살아 있는 적 중 리스트 맨 앞의 적"이다. (임시 규칙, 대상 선택 기획이 생기면 교체)
    /// 카드 사용으로 마지막 적이 쓰러지면 플레이어 턴이 남아 있어도 즉시 승리한다.
    /// </summary>
    public bool PlayCard(int handIndex)
    {
        // 플레이어 턴이 아니거나 전투가 끝났으면 카드를 낼 수 없다. (전투 종료 후 카드 효과 차단)
        if (state != BattleState.PlayerTurn)
        {
            Debug.Log("[Combat] 지금은 카드를 낼 수 없다. (상태: " + state + ")");
            return false;
        }

        EnemyController target = GetFirstLivingEnemy();
        if (target == null)
        {
            return false;
        }

        // 에너지 확인 → 지불 → 효과 → 버림 더미 이동은 PlayerController가 처리
        bool played = player.TryPlayCard(handIndex, target.combatant);
        if (!played)
        {
            return false;
        }

        // 카드 효과로 쓰러진 적은 즉시 제거한다.
        RemoveDeadEnemies();

        // 모든 적이 쓰러졌다면 즉시 승리
        if (livingEnemies.Count == 0)
        {
            EndBattle(BattleState.Victory);
        }
        return true;
    }

    /// <summary>
    /// 플레이어 턴을 수동으로 종료한다. 순서: 에너지 소멸 → 손패 전부 버리기 → 적 턴.
    /// </summary>
    public void EndPlayerTurn()
    {
        if (state != BattleState.PlayerTurn)
        {
            return;
        }

        Debug.Log("----- 플레이어 턴 종료 -----");
        player.EndTurn();               // 남은 에너지 소멸
        cardManager.DiscardAllHand();   // 남은 손패 버리기

        state = BattleState.EnemyTurn;
        RunEnemyTurn();
    }

    // ---------------------------------------------------------------
    // 적 턴
    // ---------------------------------------------------------------

    /// <summary>
    /// 살아 있는 적이 리스트 순서대로 한 번씩 행동한다.
    /// (적마다: 방어도 초기화 → 행동 실행. EnemyController.ExecuteAction이 처리)
    /// 플레이어가 쓰러지면 남은 적은 행동하지 않고 즉시 패배 처리한다.
    /// </summary>
    private void RunEnemyTurn()
    {
        Debug.Log("----- 적 턴 시작 -----");

        // 행동 도중 리스트가 바뀌어도 안전하도록 복사본으로 순회한다.
        List<EnemyController> actingOrder = new List<EnemyController>(livingEnemies);

        foreach (EnemyController enemy in actingOrder)
        {
            if (IsBattleOver)
            {
                break;
            }
            if (enemy == null || enemy.IsDead)
            {
                continue;   // 쓰러진 적은 행동하지 않는다
            }

            enemy.ExecuteAction(player.combatant);

            // 플레이어 체력이 0 이하가 되면 즉시 패배
            if (player.IsDead)
            {
                EndBattle(BattleState.Defeat);
                break;
            }
        }

        // 전투가 계속되면 다음 플레이어 턴
        if (!IsBattleOver)
        {
            StartPlayerTurn();
        }
    }

    // ---------------------------------------------------------------
    // 사망 처리 / 전투 종료
    // ---------------------------------------------------------------

    /// <summary>체력이 0 이하인 적을 살아 있는 적 리스트에서 빼고, 오브젝트를 숨긴다.</summary>
    private void RemoveDeadEnemies()
    {
        for (int i = livingEnemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = livingEnemies[i];
            if (enemy != null && !enemy.IsDead)
            {
                continue;
            }

            livingEnemies.RemoveAt(i);
            if (enemy != null)
            {
                Debug.Log("[Combat] " + enemy.data.enemyName + " 처치! (제거됨)");
                enemy.gameObject.SetActive(false);   // UI가 없으므로 오브젝트를 숨겨서 표시
            }
        }
    }

    /// <summary>전투를 끝낸다. 이후에는 카드 사용과 턴 종료가 모두 무시된다.</summary>
    private void EndBattle(BattleState result)
    {
        state = result;
        if (result == BattleState.Victory)
        {
            Debug.Log("===== 승리! 모든 적을 처치했다. =====");
        }
        else
        {
            Debug.Log("===== 패배... 플레이어 체력이 0이 되었다. =====");
        }
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    /// <summary>살아 있는 적 중 리스트 맨 앞의 적. 없으면 null</summary>
    private EnemyController GetFirstLivingEnemy()
    {
        foreach (EnemyController enemy in livingEnemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                return enemy;
            }
        }
        return null;
    }

    /// <summary>필요한 연결이 다 되어 있는지 확인한다.</summary>
    private bool CheckReferences()
    {
        if (player == null)
        {
            Debug.LogError("[Combat] Player 칸이 비어 있다.");
            return false;
        }
        if (cardManager == null)
        {
            cardManager = player.cardManager;   // 비어 있으면 플레이어가 가진 것을 사용
        }
        if (cardManager == null)
        {
            Debug.LogError("[Combat] CardManager를 찾을 수 없다.");
            return false;
        }
        return true;
    }

    /// <summary>현재 전투 상태를 콘솔에 출력한다.</summary>
    public void LogBattleState()
    {
        player.LogState();

        string handText = "";
        foreach (CardData card in cardManager.hand)
        {
            handText += "[" + card.cardName + "(" + card.cost + ")] ";
        }
        Debug.Log("[손패 " + cardManager.hand.Count + "장] " + handText
                  + "  (뽑기 " + cardManager.drawPile.Count + " / 버림 " + cardManager.discardPile.Count + ")");

        foreach (EnemyController enemy in livingEnemies)
        {
            enemy.LogState();
        }
    }
}
