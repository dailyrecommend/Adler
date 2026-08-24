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
        Grapple = 2,
        DropBomb = 3,
        ToggleCommands = 4,
        SwitchWeapon = 5,
        SwitchTarget = 6,
        Respawn = 7,
    }
}
