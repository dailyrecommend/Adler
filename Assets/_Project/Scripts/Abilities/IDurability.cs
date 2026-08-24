namespace Adler.Abilities
{
    /// <summary>
    /// 행동이 내구도에 대해 할 수 있는 것의 전부.
    /// <para>
    /// 체력을 통째로 넘기지 않는다. 넘기면 수리하는 행동이 피해를 입히거나 되살릴
    /// 수도 있게 되는데, 채우러 온 것이 할 일이 아니다. 채우는 문만 열어두면
    /// 무엇을 할 수 있는 행동인지가 서명에 드러난다.
    /// </para>
    /// </summary>
    public interface IDurability
    {
        /// <summary>지금 남은 정도. 0이면 부서졌고 1이면 온전하다.</summary>
        float Normalized { get; }

        /// <summary>가득 찼는지. 더 채울 것이 없으면 수리를 시작할 이유가 없다.</summary>
        bool IsFull { get; }

        /// <summary>이만큼 채운다. 실제로 채워진 양을 돌려준다.</summary>
        float Restore(float amount);
    }
}
