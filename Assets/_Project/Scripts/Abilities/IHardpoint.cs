using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 무언가를 내놓는 자리.
    /// <para>
    /// 폭탄이든 조명탄이든 기체의 어느 지점에서 나가야 하는데, 그 지점이 어디인지는
    /// 기체마다 다르다. 행동이 이름으로 찾으면 기체 구조를 알아야 하므로, 어느 것이
    /// 어느 자리인지는 조립하는 쪽이 정한다.
    /// </para>
    /// </summary>
    public interface IHardpoint
    {
        /// <summary>내놓을 자리와 방향.</summary>
        Transform Mount { get; }

        /// <summary>이 기체가 지금 내는 속도. 내놓는 것이 물려받을 몫이다.</summary>
        Vector3 Velocity { get; }
    }
}
