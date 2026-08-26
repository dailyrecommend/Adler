namespace Adler.Controls
{
    /// <summary>
    /// 조종석에서 할 수 있는 조작 하나.
    /// <para>
    /// 입력을 이름표로 두는 이유는 무엇에 쓸지를 데이터로 정하기 위해서다. 행동이
    /// <c>_input.Fire</c>를 직접 읽으면 어느 키가 어느 행동을 부르는지가 코드에 박히고,
    /// 행동을 하나 더할 때마다 그 연결을 적을 자리도 함께 늘어난다.
    /// </para>
    /// <para>
    /// 여기 있는 것은 조작의 <b>목록</b>이지 그것이 무엇을 하는지가 아니다. 무엇을
    /// 할지는 잇는 쪽이 정한다.
    /// </para>
    /// </summary>
    public enum PilotAction
    {
        Fire = 0,
        Boost = 1,

        // 2는 갈고리(Grapple), 3은 투하(DropBomb)가 쓰던 자리다. 번호는 인스펙터에
        // 저장되므로 재사용하면 옛 배선이 소리 없이 새 조작으로 이어진다 —
        // 새 조작은 9부터 단다.

        ToggleCommands = 4,
        SwitchWeapon = 5,
        SwitchTarget = 6,
        Respawn = 7,

        /// <summary>보조무기 방아쇠. 주무기와 나란히 당길 수 있다.</summary>
        FireSecondary = 8,
    }
}
