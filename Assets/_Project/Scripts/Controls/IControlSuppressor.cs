namespace Adler.Controls
{
    /// <summary>
    /// 지금 키보드를 조종이 아닌 다른 일에 쓰고 있는 쪽.
    /// <para>
    /// 조종면과 같은 키를 쓰는 기능이 있으면, 그 기능이 켜져 있는 동안 기체가 함께
    /// 움직이면 안 된다. 그렇다고 조종이 그 기능들을 하나하나 알아야 한다면 기능이
    /// 늘 때마다 조종 코드가 길어지고, 조종이 무기나 화면을 아는 이상한 모양이 된다.
    /// </para>
    /// <para>
    /// 그래서 "무엇이 키보드를 가져갔는가"는 묻지 않고 "가져간 것이 있는가"만 묻는다.
    /// 가져가는 쪽이 이것을 구현해 같은 오브젝트에 붙어 있기만 하면 된다.
    /// </para>
    /// </summary>
    public interface IControlSuppressor
    {
        /// <summary>참이면 키보드 조종 입력을 버린다.</summary>
        bool SuppressesKeyboard { get; }
    }
}
