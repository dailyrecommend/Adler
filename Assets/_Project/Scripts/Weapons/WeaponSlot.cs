namespace Adler.Weapons
{
    /// <summary>
    /// 무기가 놓이는 자리.
    /// <para>
    /// 목록의 몇 번째가 아니라 역할이다. 둘 다 늘 손에 들려 있고 각자의 버튼으로
    /// 나가므로, "지금 무엇을 들고 있는가"라는 질문 자체가 없어졌다.
    /// </para>
    /// <para>
    /// 어느 자리에 걸리는지는 무기 자신이 안다. 실은 쪽이 정하게 두면 같은 무기가
    /// 기체마다 다른 자리에 붙을 수 있는데, 주무기냐 보조무기냐는 그 무기가 어떻게
    /// 생겨먹었는지의 문제지 어느 기체에 달렸는지의 문제가 아니다.
    /// </para>
    /// </summary>
    public enum WeaponSlot
    {
        /// <summary>주무기. 늘 쥐고 있는 쪽.</summary>
        Primary = 0,

        /// <summary>보조무기. 때를 골라 쓰는 쪽.</summary>
        Secondary = 1,
    }
}
