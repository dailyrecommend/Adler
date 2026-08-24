namespace Adler.Core
{
    /// <summary>
    /// 커맨드를 이루는 방향 입력 하나.
    /// <para>
    /// 무기가 아니라 여기에 둔다. 이것은 "무엇을 요청하는가"가 아니라 "어느 쪽을
    /// 눌렀는가"라서, 요청을 받는 쪽만이 아니라 입력을 읽는 쪽도 알아야 하는 말이다.
    /// 무기 쪽에 두면 입력을 다루는 층이 무기를 알아야 한다.
    /// </para>
    /// </summary>
    public enum CommandDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }
}
