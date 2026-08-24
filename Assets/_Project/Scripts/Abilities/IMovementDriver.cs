using Adler.Core;
using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 행동이 기체의 움직임에 손댈 수 있는 방법.
    /// <para>
    /// 비행 모델을 통째로 넘기지 않는다. 그쪽에는 얼리고 녹이고 처음으로 되돌리는
    /// 일까지 들어 있는데, 그건 기체의 일생을 다루는 쪽의 몫이지 행동 하나가 할
    /// 일이 아니다.
    /// </para>
    /// </summary>
    public interface IMovementDriver
    {
        /// <summary>지금 나아가는 빠르기 (m/s).</summary>
        float Speed { get; }

        /// <summary>이 기체가 놓인 자리와 자세.</summary>
        Transform Body { get; }

        /// <summary>이 기체가 사는 시계.</summary>
        Clock Clock { get; }

        /// <summary>밖에서 끌어당기는 힘을 이번 스텝에 얹는다.</summary>
        void Pull(in Tether tether);
    }
}
