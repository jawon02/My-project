/// <summary>
/// 덱에 들어 있는 카드 "한 장"의 상태.
///
/// CardData  = 카드의 원본 설계도 (에셋, 모든 같은 카드가 공유, 게임 중 절대 수정하지 않는다)
/// CardInstance = 실제 덱에 들어 있는 한 장 (같은 카드가 3장이면 CardInstance도 3개)
///
/// 그래서 같은 카드가 여러 장이어도 "한 장만" 강화할 수 있다.
/// 일반 C# 클래스이므로 오브젝트에 붙이지 않고 CardManager가 만들어서 관리한다.
/// [System.Serializable] 덕분에 인스펙터에서 현재 덱을 눈으로 확인할 수 있다.
/// </summary>
[System.Serializable]
public class CardInstance
{
    public CardData data;            // 원본 카드 데이터 (읽기만 한다)
    public bool isUpgraded = false;  // 이 카드 한 장의 강화 여부

    /// <summary>인스펙터 직렬화용 기본 생성자 (직접 호출하지 않는다)</summary>
    public CardInstance()
    {
    }

    /// <summary>원본 카드 데이터로 새 카드 한 장을 만든다. (미강화 상태)</summary>
    public CardInstance(CardData cardData)
    {
        data = cardData;
        isUpgraded = false;
    }

    // ---------------------------------------------------------------
    // 현재 상태를 반영한 값들 (게임 로직은 CardData 대신 이 값을 읽는다)
    // ---------------------------------------------------------------

    /// <summary>카드 이름. 강화된 카드는 뒤에 +가 붙는다.</summary>
    public string Name
    {
        get { return isUpgraded ? data.cardName + "+" : data.cardName; }
    }

    /// <summary>비용. 강화해도 바뀌지 않는다. (확정 규칙)</summary>
    public int Cost
    {
        get { return data.cost; }
    }

    /// <summary>카드 종류</summary>
    public CardType Type
    {
        get { return data.type; }
    }

    /// <summary>현재 수치. 강화된 카드는 원본의 upgradedValue를 쓴다.</summary>
    public int Value
    {
        get { return isUpgraded ? data.upgradedValue : data.value; }
    }

    // ---------------------------------------------------------------
    // 강화
    // ---------------------------------------------------------------

    /// <summary>
    /// 강화할 수 있는 카드인지.
    /// 조건: 아직 강화하지 않았고, 원본의 upgradedValue가 value보다 커야 한다.
    /// (강화가 없는 카드는 upgradedValue를 value와 같게 두면 된다.)
    /// </summary>
    public bool CanUpgrade
    {
        get { return !isUpgraded && data.upgradedValue > data.value; }
    }

    /// <summary>
    /// 이 카드 한 장을 강화한다. 성공하면 true.
    /// 이미 강화했거나 강화가 불가능한 카드면 아무것도 하지 않고 false.
    /// 원본 CardData는 수정하지 않고, 이 카드의 isUpgraded 값만 바꾼다.
    /// </summary>
    public bool Upgrade()
    {
        if (!CanUpgrade)
        {
            return false;
        }
        isUpgraded = true;
        return true;
    }
}
