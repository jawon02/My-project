using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 4-B (적 SelfBuff / AllyBuff / AllyHeal) 테스트용 스크립트. CombatManager와 같은 오브젝트에 붙인다.
/// 컴포넌트 우클릭 → Run Auto Test 로 실행한다.
///
/// 전제: CombatManager의 Enemies 리스트에 적 2마리가 들어 있다. (씬 구성: 슬라임 2마리)
///       플레이어 최대 체력 70, 최대 에너지 3. (기존 테스트와 같은 구성)
/// 테스트 적(주술사, 아군 슬라임)은 에셋 없이 이 스크립트가 임시로 만든다.
/// 테스트 중에만 씬의 두 적이 쓰는 데이터를 바꾸고, 끝나면 반드시 원래 데이터로 되돌린다.
/// 적이 3마리 이상 필요한 대상 선택 확인은 임시 오브젝트로 따로 하고, 끝나면 지운다. (씬의 Enemies 리스트는 건드리지 않는다)
/// 수치는 모두 임시 테스트 수치이며 최종 밸런스가 아니다.
/// </summary>
public class EnemyBuffTest : MonoBehaviour
{
    [Header("연결")]
    public CombatManager combat;

    private int passCount = 0;
    private int failCount = 0;

    // 테스트 중에만 쓰는 임시 데이터
    private EnemyData shamanData;   // 주술사: 체력 30 / 자기 강화 공격력 +2 (2턴) → 공격 4 → 아군 강화 공격력 +2 (2턴) → 아군 회복 6
    private EnemyData allyData;     // 아군 테스트 적 "슬라임": 체력 20 / 공격 5
    private CardData weakHit;       // 플레이어 단일 공격 2 (적 체력을 조금만 깎는 용도)
    private List<GameObject> tempObjects = new List<GameObject>();   // 대상 선택 단위 테스트용 임시 오브젝트

    // 패턴 번호 (turnIndex 직접 지정용)
    private const int ShamanSelfBuff = 0;
    private const int ShamanAllyBuff = 2;
    private const int ShamanAllyHeal = 3;

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
        Debug.Log("===== Phase 4-B 적 강화·회복 자동 테스트 시작 =====");

        EnemyController enemyA = combat.enemies[0];   // 1번 칸 (왼쪽, 먼저 행동)
        EnemyController enemyB = combat.enemies[1];   // 2번 칸
        EnemyData originalA = enemyA.data;           // 테스트가 끝나면 되돌릴 원래 데이터
        EnemyData originalB = enemyB.data;

        CreateTempData();
        try
        {
            TestSelectionUnits();
            TestSelfBuffRefresh();
            TestShamanLeft(enemyA, enemyB);
            TestShamanRight(enemyA, enemyB);
            TestNoTarget(enemyA, enemyB);
        }
        finally
        {
            // 중간에 에러가 나도 씬의 적 데이터는 반드시 원래대로 되돌린다.
            enemyA.data = originalA;
            enemyB.data = originalB;
            combat.StartRun();
            combat.StartBattle();   // 원래 슬라임 2마리로 전투 상태를 다시 맞춘다
            DestroyTempData();
            Debug.Log("[테스트] 적 데이터 복원: 1번 " + NameOf(enemyA.data) + ", 2번 " + NameOf(enemyB.data));
        }

        Debug.Log("===== 결과: PASS " + passCount + " / FAIL " + failCount + " =====");
    }

    // ===== A. 아군 대상 선택 (임시 적 오브젝트, 3~4마리) =====
    private void TestSelectionUnits()
    {
        // [주술사, A, B] → 강화 대상은 A (자신 제외, 가장 왼쪽)
        EnemyController s = MakeTempEnemy(shamanData, "주술사");
        EnemyController a = MakeTempEnemy(allyData, "A");
        EnemyController b = MakeTempEnemy(allyData, "B");
        SetAllies(s, a, b);
        CheckBool("A. 아군 강화 대상 - 자신 제외 가장 왼쪽 A", s.FindAllyBuffTarget() == a, true);
        CheckBool("A. 모두 체력 가득 - 회복 대상 없음", s.FindAllyHealTarget() == null, true);
        s.turnIndex = ShamanAllyHeal;
        s.RefreshPreview();
        CheckText("A. 회복 대상 없음 - 예고", s.nextActionPreview, EnemyController.NoTargetPreview);

        // [죽은 A, 주술사, B] → 강화 대상은 B (쓰러진 적 제외)
        a.combatant.currentHp = 0;
        SetAllies(a, s, b);
        CheckBool("A. 쓰러진 아군 제외 - 강화 대상 B", s.FindAllyBuffTarget() == b, true);
        CheckBool("A. 쓰러진 아군은 체력이 줄었어도 회복 대상 아님", s.FindAllyHealTarget() == null, true);

        // [가득 찬 A, 주술사(체력 줄어듦), 줄어든 B, 줄어든 C] → 회복 대상은 B
        EnemyController c = MakeTempEnemy(allyData, "C");
        a.combatant.currentHp = a.combatant.maxHp;
        s.combatant.currentHp = 10;
        b.combatant.currentHp = 15;
        c.combatant.currentHp = 5;
        SetAllies(a, s, b, c);
        CheckBool("A. 회복 대상 - 체력 줄어든 아군 중 가장 왼쪽 B", s.FindAllyHealTarget() == b, true);
        CheckBool("A. 강화 대상은 체력과 상관없이 가장 왼쪽 A", s.FindAllyBuffTarget() == a, true);
        s.RefreshPreview();
        CheckText("A. 회복 예고에 대상 이름", s.nextActionPreview, "아군 회복: B +6");

        // 아군 목록이 없는 적 (단독 테스트) → 대상 없음
        s.allies = null;
        CheckBool("A. 아군 목록 없음 - 강화 대상 없음", s.FindAllyBuffTarget() == null, true);

        DestroyTempObjects();
    }

    // ===== B. SelfBuff 갱신 시에도 그 행동 끝에 줄이지 않음 (단독 적) =====
    private void TestSelfBuffRefresh()
    {
        EnemyController s = MakeTempEnemy(shamanData, "주술사");
        Combatant dummyPlayer = new Combatant();
        dummyPlayer.Init(70);
        dummyPlayer.displayName = "가짜 플레이어";

        // 이미 공격력 증가 1 (1턴)과 방어도 증가 2 (2턴)가 걸려 있다고 가정
        s.combatant.ApplyStatus(StatusEffectType.AttackUp, 1, 1);
        s.combatant.ApplyStatus(StatusEffectType.DefenseUp, 2, 2);
        s.turnIndex = ShamanSelfBuff;
        s.ExecuteAction(dummyPlayer);   // 자기 강화 공격력 +2 (2턴) → 이미 있으므로 갱신
        Check("B. SelfBuff 갱신 - 기존 수치 1 유지", Amount(s.combatant, StatusEffectType.AttackUp), 1);
        Check("B. SelfBuff 갱신 - 행동 끝에 줄이지 않아 2턴", Turns(s.combatant, StatusEffectType.AttackUp), 2);
        Check("B. 다른 효과(방어도 증가)는 평소처럼 2→1", Turns(s.combatant, StatusEffectType.DefenseUp), 1);

        DestroyTempObjects();
    }

    // ===== C. 주술사 1번 / 슬라임 2번: 패턴 전체 =====
    private void TestShamanLeft(EnemyController shaman, EnemyController slime)
    {
        shaman.data = shamanData;
        slime.data = allyData;
        Prepare();
        PlayerController p = combat.player;

        CheckText("C. 시작 - 1번 주술사 예고", shaman.nextActionPreview, "자기 강화: 공격력 +2 (2턴)");
        CheckText("C. 시작 - 2번 슬라임 예고", slime.nextActionPreview, "공격 5");

        // 적 턴 1: 주술사 자기 강화, 슬라임 공격 5
        combat.EndPlayerTurn();
        Check("C. SelfBuff - 주술사 공격력 증가 +2", Amount(shaman.combatant, StatusEffectType.AttackUp), 2);
        Check("C. SelfBuff 직후 지속시간 2 (줄지 않음)", Turns(shaman.combatant, StatusEffectType.AttackUp), 2);
        Check("C. SelfBuff - 2번 슬라임에게는 효과 없음", slime.combatant.statusEffects.Count, 0);
        Check("C. 적 턴 1 - 플레이어 70-5=65", p.combatant.currentHp, 65);
        CheckText("C. SelfBuff 반영 - 주술사 공격 예고", shaman.nextActionPreview, "공격 6 (기본 4, 공격력 증가 +2)");

        // 적 턴 2: 주술사 공격 4+2=6, 슬라임 공격 5
        combat.EndPlayerTurn();
        Check("C. SelfBuff 반영 - 플레이어 65-6-5=54", p.combatant.currentHp, 54);
        Check("C. SelfBuff 이후 첫 행동 끝 - 지속시간 1", Turns(shaman.combatant, StatusEffectType.AttackUp), 1);
        CheckText("C. 주술사 아군 강화 예고", shaman.nextActionPreview, "아군 강화: 2번 슬라임 공격력 +2 (2턴)");

        // 적 턴 3: 주술사 아군 강화 → 같은 턴에 슬라임이 강화된 공격 7
        combat.EndPlayerTurn();
        CheckBool("C. SelfBuff 이후 두 번째 행동 끝 - 만료", shaman.combatant.FindStatus(StatusEffectType.AttackUp) == null, true);
        Check("C. AllyBuff - 주술사 자신에게는 효과 없음", shaman.combatant.statusEffects.Count, 0);
        Check("C. AllyBuff - 2번 슬라임 공격력 증가 +2", Amount(slime.combatant, StatusEffectType.AttackUp), 2);
        Check("C. AllyBuff 반영 - 같은 턴 슬라임 공격 7 (54-7=47)", p.combatant.currentHp, 47);
        Check("C. AllyBuff - 슬라임 자신의 행동 끝에 2→1", Turns(slime.combatant, StatusEffectType.AttackUp), 1);
        CheckText("C. AllyBuff 반영 - 슬라임 예고", slime.nextActionPreview, "공격 7 (기본 5, 공격력 증가 +2)");
        CheckText("C. 슬라임 체력 가득 - 주술사 회복 예고는 대상 없음", shaman.nextActionPreview, EnemyController.NoTargetPreview);

        // 플레이어 턴: 슬라임 체력 20 → 18 (회복 대상이 생김)
        SetHand(weakHit);
        combat.SelectEnemy(1);
        combat.PlayCard(0);
        Check("C. 약한 공격 - 슬라임 20→18", slime.combatant.currentHp, 18);
        CheckText("C. 체력 변화 후 회복 예고 재계산", shaman.nextActionPreview, "아군 회복: 2번 슬라임 +6");

        // 적 턴 4: 주술사 아군 회복 18+6 → 최대 20, 슬라임 공격 7
        combat.EndPlayerTurn();
        Check("C. AllyHeal - 최대 체력을 넘지 않음 (18+6 → 20)", slime.combatant.currentHp, 20);
        Check("C. 적 턴 4 - 플레이어 47-7=40", p.combatant.currentHp, 40);
        CheckBool("C. AllyBuff - 슬라임 두 번째 행동 끝 - 만료", slime.combatant.FindStatus(StatusEffectType.AttackUp) == null, true);
        CheckText("C. 패턴 반복 - 다시 자기 강화 예고", shaman.nextActionPreview, "자기 강화: 공격력 +2 (2턴)");
    }

    // ===== D. 주술사 2번 / 슬라임 1번: 이미 행동한 아군에게 강화 =====
    private void TestShamanRight(EnemyController enemyA, EnemyController enemyB)
    {
        EnemyController slime = enemyA;
        EnemyController shaman = enemyB;
        slime.data = allyData;
        shaman.data = shamanData;
        Prepare();
        PlayerController p = combat.player;

        shaman.turnIndex = ShamanAllyBuff;   // 바로 아군 강화부터 시작
        shaman.RefreshPreview();
        CheckText("D. 2번 주술사 아군 강화 예고", shaman.nextActionPreview, "아군 강화: 1번 슬라임 공격력 +2 (2턴)");

        // 적 턴 1: 슬라임 공격 5 (먼저 행동) → 주술사 아군 강화
        combat.EndPlayerTurn();
        Check("D. 적 턴 1 - 플레이어 70-5=65", p.combatant.currentHp, 65);
        Check("D. 이미 행동한 슬라임 - 지속시간 2 그대로", Turns(slime.combatant, StatusEffectType.AttackUp), 2);
        CheckText("D. 주술사 행동 직후 슬라임 예고 재계산", slime.nextActionPreview, "공격 7 (기본 5, 공격력 증가 +2)");

        // 적 턴 2: 슬라임 공격 7 → 2→1, 주술사 회복은 대상 없음(생략)
        combat.EndPlayerTurn();
        Check("D. 강화 첫 적용 - 플레이어 65-7=58", p.combatant.currentHp, 58);
        Check("D. 슬라임 행동 끝 - 지속시간 1", Turns(slime.combatant, StatusEffectType.AttackUp), 1);

        // 적 턴 3: 슬라임 공격 7 → 만료, 주술사 자기 강화
        combat.EndPlayerTurn();
        Check("D. 강화 두 번째 적용 - 플레이어 58-7=51", p.combatant.currentHp, 51);
        CheckBool("D. 슬라임 두 번째 행동 끝 - 만료", slime.combatant.FindStatus(StatusEffectType.AttackUp) == null, true);
        CheckText("D. 만료 후 슬라임 예고", slime.nextActionPreview, "공격 5");
    }

    // ===== E. 대상이 죽으면 예고 재계산 / 대상 없음 생략 =====
    private void TestNoTarget(EnemyController enemyA, EnemyController enemyB)
    {
        EnemyController shaman = enemyA;
        EnemyController slime = enemyB;
        shaman.data = shamanData;
        slime.data = allyData;
        Prepare();
        PlayerController p = combat.player;

        shaman.turnIndex = ShamanAllyBuff;
        shaman.RefreshPreview();
        CheckText("E. 처치 전 - 아군 강화 예고", shaman.nextActionPreview, "아군 강화: 2번 슬라임 공격력 +2 (2턴)");

        // 플레이어가 슬라임을 처치
        slime.combatant.currentHp = 1;
        SetHand(weakHit);
        combat.SelectEnemy(1);
        combat.PlayCard(0);
        CheckBool("E. 슬라임 처치", slime.IsDead, true);
        CheckBool("E. 전투는 계속 (주술사 생존)", combat.state == BattleState.PlayerTurn, true);
        CheckText("E. 대상 사망 - 예고 재계산 (대상 없음)", shaman.nextActionPreview, EnemyController.NoTargetPreview);

        // 대상 없는 아군 강화 → 안전하게 생략
        combat.EndPlayerTurn();
        Check("E. 아군 강화 생략 - 플레이어 체력 그대로 70", p.combatant.currentHp, 70);
        Check("E. 아군 강화 생략 - 주술사 효과 없음", shaman.combatant.statusEffects.Count, 0);
        Check("E. 생략해도 다음 행동으로 넘어감 (turnIndex 3)", shaman.turnIndex, 3);
        CheckText("E. 다음 아군 회복도 대상 없음", shaman.nextActionPreview, EnemyController.NoTargetPreview);

        // 대상 없는 아군 회복 → 안전하게 생략
        combat.EndPlayerTurn();
        Check("E. 아군 회복 생략 - 플레이어 체력 그대로 70", p.combatant.currentHp, 70);
        Check("E. 생략 후 turnIndex 4", shaman.turnIndex, 4);
        CheckBool("E. 전투 계속 - 플레이어 턴", combat.state == BattleState.PlayerTurn, true);
    }

    // ---------------------------------------------------------------
    // 도우미 함수
    // ---------------------------------------------------------------

    // 새 런 + 새 전투를 시작하고 손패를 비운다.
    private void Prepare()
    {
        combat.StartRun();
        combat.StartBattle();
        combat.cardManager.hand.Clear();
    }

    // 손패를 지정한 카드들로 바꾼다.
    private void SetHand(params CardData[] handCards)
    {
        combat.cardManager.hand.Clear();
        foreach (CardData card in handCards)
        {
            combat.cardManager.hand.Add(new CardInstance(card));
        }
    }

    // 대상 선택 단위 테스트용 임시 적. 씬에 저장되지 않고(HideAndDontSave) 테스트 끝에 지운다.
    private EnemyController MakeTempEnemy(EnemyData data, string label)
    {
        GameObject obj = new GameObject("TempEnemy_" + label);
        obj.hideFlags = HideFlags.HideAndDontSave;
        tempObjects.Add(obj);
        EnemyController enemy = obj.AddComponent<EnemyController>();
        enemy.data = data;
        enemy.InitBattle();
        enemy.combatant.displayName = label;
        return enemy;
    }

    // 주어진 순서(= 왼쪽부터)의 리스트를 모두에게 아군 목록으로 넣어 준다.
    private void SetAllies(params EnemyController[] order)
    {
        List<EnemyController> list = new List<EnemyController>(order);
        foreach (EnemyController enemy in order)
        {
            enemy.allies = list;
        }
    }

    // type 효과의 수치 (효과가 없으면 0)
    private int Amount(Combatant target, StatusEffectType type)
    {
        StatusEffect effect = target.FindStatus(type);
        return effect != null ? effect.amount : 0;
    }

    // type 효과의 남은 지속시간 (효과가 없으면 0)
    private int Turns(Combatant target, StatusEffectType type)
    {
        StatusEffect effect = target.FindStatus(type);
        return effect != null ? effect.turnsLeft : 0;
    }

    private string NameOf(EnemyData data)
    {
        return data != null ? data.enemyName : "(없음)";
    }

    // 테스트용 임시 데이터를 만든다. (에셋 파일로 저장되지 않는다)
    private void CreateTempData()
    {
        shamanData = ScriptableObject.CreateInstance<EnemyData>();
        shamanData.enemyName = "주술사";
        shamanData.maxHp = 30;
        shamanData.actionPattern = new List<EnemyActionData>
        {
            MakeAction(EnemyActionType.SelfBuff, 2, StatusEffectType.AttackUp, 2),
            MakeAction(EnemyActionType.Attack, 4, StatusEffectType.None, 0),
            MakeAction(EnemyActionType.AllyBuff, 2, StatusEffectType.AttackUp, 2),
            MakeAction(EnemyActionType.AllyHeal, 6, StatusEffectType.None, 0)
        };

        allyData = ScriptableObject.CreateInstance<EnemyData>();
        allyData.enemyName = "슬라임";
        allyData.maxHp = 20;
        allyData.actionPattern = new List<EnemyActionData>
        {
            MakeAction(EnemyActionType.Attack, 5, StatusEffectType.None, 0)
        };

        weakHit = ScriptableObject.CreateInstance<CardData>();
        weakHit.cardName = "테스트약공";
        weakHit.type = CardType.Attack;
        weakHit.targetType = TargetType.SingleEnemy;
        weakHit.cost = 1;
        weakHit.value = 2;
        weakHit.upgradedValue = 3;
    }

    private EnemyActionData MakeAction(EnemyActionType type, int value, StatusEffectType effect, int duration)
    {
        EnemyActionData action = new EnemyActionData();
        action.type = type;
        action.value = value;
        action.statusEffect = effect;
        action.duration = duration;
        return action;
    }

    private void DestroyTempData()
    {
        DestroyTempObjects();
        DestroyTemp(shamanData);
        DestroyTemp(allyData);
        DestroyTemp(weakHit);
    }

    private void DestroyTempObjects()
    {
        foreach (GameObject obj in tempObjects)
        {
            DestroyTemp(obj);
        }
        tempObjects.Clear();
    }

    // 임시로 만든 오브젝트를 지운다.
    private void DestroyTemp(Object obj)
    {
        if (obj == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }

    private bool FindReferences()
    {
        if (combat == null) combat = GetComponent<CombatManager>();
        if (combat == null)
        {
            Debug.LogError("CombatManager를 찾을 수 없다. 같은 오브젝트에 붙이거나 인스펙터에 연결하자.");
            return false;
        }
        if (combat.player == null)
        {
            Debug.LogError("CombatManager의 Player 칸을 채우자.");
            return false;
        }
        if (combat.cardManager == null)
        {
            combat.cardManager = combat.player.cardManager;
        }
        if (combat.cardManager == null)
        {
            Debug.LogError("CardManager가 연결되지 않았다.");
            return false;
        }
        if (combat.enemies.Count < 2 || combat.enemies[0] == null || combat.enemies[1] == null)
        {
            Debug.LogError("이 테스트는 CombatManager의 Enemies 리스트에 적 2마리가 필요하다.");
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

    // 문자열 비교
    private void CheckText(string name, string actual, string expected)
    {
        if (actual == expected)
        {
            passCount++;
            Debug.Log("PASS  " + name + " (\"" + actual + "\")");
        }
        else
        {
            failCount++;
            Debug.LogError("FAIL  " + name + " (기대: \"" + expected + "\", 실제: \"" + actual + "\")");
        }
    }
}
