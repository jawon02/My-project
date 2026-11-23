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
///
/// 공격 대상 (Phase 3):
///  카드의 Target Type을 보고 대상을 정한다. 단일 = 선택된 적 / 전체 = 살아 있는 모든 적 / 무작위 = 살아 있는 적 중 1명.
///  적 번호는 Enemies 리스트의 칸 번호로 고정이다. (0번 칸 = 1번 적, 적이 죽어도 번호가 바뀌지 않는다)
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
    public int selectedEnemyIndex = -1;   // 단일 공격 대상: Enemies 리스트의 칸 번호 (0부터). -1 = 선택 없음

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
            StartRun();         // 런 시작: 플레이어 체력 최대 + 런 덱 생성
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
        //                  ← / → = 대상 변경, Tab = 다음 살아 있는 적
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
        if (kb.leftArrowKey.wasPressedThisFrame)
        {
            SelectPreviousEnemy();   // ← : 왼쪽 적으로 대상 변경
        }
        if (kb.rightArrowKey.wasPressedThisFrame)
        {
            SelectNextEnemy();       // → : 오른쪽 적으로 대상 변경
        }
        if (kb.tabKey.wasPressedThisFrame)
        {
            SelectNextEnemyLoop();   // Tab : 다음 살아 있는 적 (끝에서 처음으로 순환)
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
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SelectPreviousEnemy();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SelectNextEnemy();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SelectNextEnemyLoop();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            EndPlayerTurn();
        }
#endif
    }

    // ---------------------------------------------------------------
    // 런 시작 (새 게임 한 판의 시작점)
    // ---------------------------------------------------------------

    /// <summary>
    /// 새 런(한 판 전체)을 시작한다. 플레이어 체력을 최대로 채우고, 런 덱을 시작 덱 기준으로 새로 만든다.
    /// 이전 런의 카드 강화 상태는 모두 사라진다. 전투는 시작하지 않는다. (StartBattle을 따로 호출)
    /// </summary>
    public void StartRun()
    {
        if (!CheckReferences())
        {
            return;
        }

        player.InitRun();         // 플레이어 체력 최대
        cardManager.StartRun();   // 런 덱 생성 (모든 카드 미강화)
        state = BattleState.NotStarted;
        Debug.Log("===== 새 런 시작 =====");
    }

    // ---------------------------------------------------------------
    // 전투 시작
    // ---------------------------------------------------------------

    /// <summary>
    /// 전투를 시작한다. (플레이어 체력과 카드 강화 상태는 건드리지 않는다. 런 시작은 StartRun()으로 따로 한다.)
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

        // 대상 선택 초기화: 가장 왼쪽의 살아 있는 적을 기본 대상으로 한다.
        selectedEnemyIndex = -1;
        RefreshSelection();

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
    /// 카드의 Target Type을 보고 대상을 정한 뒤 PlayerController에 넘긴다.
    ///  - 단일(SingleEnemy): 지금 선택된 적 (selectedEnemyIndex)
    ///  - 전체(AllEnemies): 살아 있는 모든 적 (왼쪽부터)
    ///  - 무작위(RandomEnemy): 살아 있는 적 중 1명을 지금 이 순간 무작위로
    /// 처리 순서: 효과 적용 → 쓰러진 적 제거 → 대상 자동 변경 → 승리 판정(한 번)
    /// </summary>
    public bool PlayCard(int handIndex)
    {
        // 플레이어 턴이 아니거나 전투가 끝났으면 카드를 낼 수 없다. (전투 종료 후 카드 효과 차단)
        if (state != BattleState.PlayerTurn)
        {
            Debug.Log("[Combat] 지금은 카드를 낼 수 없다. (상태: " + state + ")");
            return false;
        }

        RefreshSelection();   // 안전장치: 선택된 적이 이미 쓰러졌다면 먼저 다시 고른다

        // 손패 번호가 잘못됐으면 card는 null이다. (실패 처리와 로그는 PlayerController가 한다)
        CardInstance card = null;
        if (handIndex >= 0 && handIndex < cardManager.hand.Count)
        {
            card = cardManager.hand[handIndex];
        }

        // 카드의 대상 유형에 따라 때릴 적을 정한다.
        List<EnemyController> targetEnemies = ResolveTargets(card);
        List<Combatant> targets = new List<Combatant>();
        foreach (EnemyController enemy in targetEnemies)
        {
            targets.Add(enemy.combatant);
        }

        // 에너지 확인 → 지불 → 효과 → 버림 더미 이동은 PlayerController가 처리
        bool played = player.TryPlayCardOnTargets(handIndex, targets);
        if (!played)
        {
            return false;
        }

        // 어떤 적이 맞았는지 로그로 남긴다. (무작위 공격의 실제 대상 확인용)
        if (card != null && card.Type == CardType.Attack)
        {
            Debug.Log("[Combat] '" + card.Name + "' 대상(" + TargetTypeToText(card.TargetType) + "): " + EnemyListToText(targetEnemies));
        }

        // 카드 효과로 쓰러진 적은 제거한다. (전체 공격이어도 모든 적에게 피해를 준 다음 한 번에 처리)
        RemoveDeadEnemies();

        // 선택된 적이 쓰러졌다면 가장 왼쪽의 살아 있는 적으로 자동 변경 (아무도 없으면 선택 해제)
        RefreshSelection();

        // 모든 적이 쓰러졌다면 즉시 승리 (카드 처리가 다 끝난 뒤 한 번만 판정)
        if (livingEnemies.Count == 0)
        {
            EndBattle(BattleState.Victory);
        }
        return true;
    }

    /// <summary>
    /// 카드의 대상 유형에 따라 공격할 적 목록을 만든다. 공격 카드가 아니면 빈 목록.
    /// 대상이 없으면(살아 있는 적이 없거나 선택된 적이 없으면) 빈 목록 → PlayerController가 사용 실패 처리한다.
    /// </summary>
    private List<EnemyController> ResolveTargets(CardInstance card)
    {
        List<EnemyController> result = new List<EnemyController>();
        if (card == null || card.Type != CardType.Attack)
        {
            return result;   // 방어 카드 등은 적 대상이 필요 없다
        }

        switch (card.TargetType)
        {
            case TargetType.SingleEnemy:
                EnemyController selected = SelectedEnemy;
                if (selected != null)
                {
                    result.Add(selected);
                }
                break;

            case TargetType.AllEnemies:
                // livingEnemies는 Enemies 리스트 순서(= 왼쪽부터)를 유지하고 있다.
                foreach (EnemyController enemy in livingEnemies)
                {
                    if (enemy != null && !enemy.IsDead)
                    {
                        result.Add(enemy);
                    }
                }
                break;

            case TargetType.RandomEnemy:
                // 살아 있는 적 후보를 모은 뒤 그중 하나를 무작위로 고른다. (카드 사용 순간에 결정)
                List<EnemyController> candidates = new List<EnemyController>();
                foreach (EnemyController enemy in livingEnemies)
                {
                    if (enemy != null && !enemy.IsDead)
                    {
                        candidates.Add(enemy);
                    }
                }
                if (candidates.Count > 0)
                {
                    result.Add(candidates[Random.Range(0, candidates.Count)]);   // 0 ~ (후보 수 - 1)
                }
                break;

            case TargetType.Self:
                // 공격 카드의 자기 대상은 허용하지 않는다. 빈 목록을 넘기면 PlayerController가 막는다.
                break;
        }
        return result;
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
    // 단일 공격 대상 선택 (키보드 입력과 나중의 UI 클릭이 모두 이 함수들을 호출한다)
    // ---------------------------------------------------------------

    /// <summary>지금 선택된 적. 선택이 없거나 선택된 적이 쓰러졌으면 null</summary>
    public EnemyController SelectedEnemy
    {
        get { return IsSelectable(selectedEnemyIndex) ? enemies[selectedEnemyIndex] : null; }
    }

    /// <summary>
    /// index번 칸(0부터)의 적을 대상으로 선택한다. 성공하면 true.
    /// 쓰러진 적이나 없는 칸은 선택할 수 없다. (이때는 선택이 바뀌지 않는다)
    /// </summary>
    public bool SelectEnemy(int index)
    {
        if (!IsSelectable(index))
        {
            Debug.Log("[Combat] " + (index + 1) + "번 적은 선택할 수 없다. (없거나 쓰러짐)");
            return false;
        }
        selectedEnemyIndex = index;
        Debug.Log("[Combat] 대상 선택: " + EnemyLabel(enemies[index]));
        return true;
    }

    /// <summary>→ : 오른쪽에 있는 살아 있는 적으로 대상 변경. 오른쪽 끝이면 그대로.</summary>
    public bool SelectNextEnemy()
    {
        for (int i = selectedEnemyIndex + 1; i < enemies.Count; i++)
        {
            if (IsSelectable(i))
            {
                return SelectEnemy(i);
            }
        }
        return false;
    }

    /// <summary>← : 왼쪽에 있는 살아 있는 적으로 대상 변경. 왼쪽 끝이면 그대로.</summary>
    public bool SelectPreviousEnemy()
    {
        int start = (selectedEnemyIndex < 0) ? enemies.Count - 1 : selectedEnemyIndex - 1;
        for (int i = start; i >= 0; i--)
        {
            if (IsSelectable(i))
            {
                return SelectEnemy(i);
            }
        }
        return false;
    }

    /// <summary>Tab : 다음 살아 있는 적으로 대상 변경. 오른쪽 끝 다음은 다시 왼쪽 처음으로 돌아간다.</summary>
    public bool SelectNextEnemyLoop()
    {
        for (int step = 1; step <= enemies.Count; step++)
        {
            int i = (selectedEnemyIndex + step) % enemies.Count;   // 끝을 넘어가면 0번 칸으로
            if (i < 0)
            {
                i += enemies.Count;
            }
            if (IsSelectable(i))
            {
                return SelectEnemy(i);
            }
        }
        return false;
    }

    /// <summary>
    /// 선택 상태를 정리한다. (전투 시작, 적 제거 후, 카드 사용 직전에 호출)
    /// - 지금 선택된 적이 살아 있으면 그대로 둔다.
    /// - 아니면 가장 왼쪽의 살아 있는 적을 고른다. (살아 있는 적이 1마리면 자연히 그 적이 선택된다)
    /// - 살아 있는 적이 없으면 선택을 해제한다(-1).
    /// </summary>
    private void RefreshSelection()
    {
        if (IsSelectable(selectedEnemyIndex))
        {
            return;
        }

        int before = selectedEnemyIndex;
        selectedEnemyIndex = -1;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (IsSelectable(i))
            {
                selectedEnemyIndex = i;
                break;
            }
        }

        if (selectedEnemyIndex != before)
        {
            if (selectedEnemyIndex >= 0)
            {
                Debug.Log("[Combat] 대상 자동 변경: " + EnemyLabel(enemies[selectedEnemyIndex]));
            }
            else
            {
                Debug.Log("[Combat] 살아 있는 적이 없어 대상 선택을 해제했다.");
            }
        }
    }

    /// <summary>index번 칸의 적을 선택할 수 있는지 (칸이 있고, 적이 살아 있고, 이번 전투에 참가 중)</summary>
    private bool IsSelectable(int index)
    {
        if (index < 0 || index >= enemies.Count)
        {
            return false;
        }
        EnemyController enemy = enemies[index];
        return enemy != null && !enemy.IsDead && livingEnemies.Contains(enemy);
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    /// <summary>적 이름 앞에 고정 번호를 붙인다. 예: "2번 슬라임"</summary>
    private string EnemyLabel(EnemyController enemy)
    {
        int number = enemies.IndexOf(enemy) + 1;   // 칸 번호(0부터) + 1 = 화면에 보여 줄 번호
        string name = (enemy.data != null) ? enemy.data.enemyName : enemy.gameObject.name;
        return number + "번 " + name;
    }

    private string EnemyListToText(List<EnemyController> list)
    {
        if (list.Count == 0)
        {
            return "(없음)";
        }
        string text = "";
        for (int i = 0; i < list.Count; i++)
        {
            text += (i > 0 ? ", " : "") + EnemyLabel(list[i]);
        }
        return text;
    }

    private string TargetTypeToText(TargetType type)
    {
        switch (type)
        {
            case TargetType.SingleEnemy: return "단일";
            case TargetType.AllEnemies: return "전체";
            case TargetType.RandomEnemy: return "무작위";
            default: return "자신";
        }
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
        foreach (CardInstance card in cardManager.hand)
        {
            handText += "[" + card.Name + "(" + card.Cost + ")] ";
        }
        Debug.Log("[손패 " + cardManager.hand.Count + "장] " + handText
                  + "  (뽑기 " + cardManager.drawPile.Count + " / 버림 " + cardManager.discardPile.Count + ")");

        foreach (EnemyController enemy in livingEnemies)
        {
            enemy.LogState();
        }

        EnemyController selected = SelectedEnemy;
        Debug.Log("[대상] " + (selected != null ? EnemyLabel(selected) : "(없음)") + "  (←/→/Tab으로 변경)");
    }
}
