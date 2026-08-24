using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 행동이 표적에 대해 물어볼 수 있는 것의 전부.
    /// <para>
    /// 조준 컴포넌트를 통째로 넘기면 행동이 표적을 <b>바꿀</b> 수도 있게 된다. 무엇을
    /// 잡을지는 조준의 몫이고 행동은 잡힌 것을 쓸 뿐이라, 읽는 쪽만 열어둔다.
    /// </para>
    /// </summary>
    public interface ITargetSource
    {
        /// <summary>잡은 것이 있는지.</summary>
        bool HasTarget { get; }

        /// <summary>잡은 표적. 없으면 null.</summary>
        Transform Target { get; }

        /// <summary>겨누는 지점. 원점이 아니라 몸통 한가운데다.</summary>
        Vector3 TargetPoint { get; }
    }
}
