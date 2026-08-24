using System;

namespace Adler.Abilities
{
    /// <summary>
    /// 행동에 붙는 꼬리표. 전이 규칙이 행동의 이름 대신 이것을 보고 판단한다.
    /// <para>
    /// 규칙을 이름으로 쓰면 행동을 하나 더할 때마다 규칙도 함께 늘어난다.
    /// "기총은 그래플로 끊을 수 있다"를 스무 개 행동에 대해 쓰면 규칙이 사백 줄이 된다.
    /// 꼬리표로 쓰면 "이어지는 것은 순간적인 것으로 끊을 수 있다" 한 줄이면 되고,
    /// 새 행동은 자기에게 맞는 꼬리표만 달면 규칙에 손대지 않아도 자리를 찾는다.
    /// </para>
    /// <para>
    /// 여러 개를 함께 달 수 있다. 그래플은 <see cref="Sustained"/>이면서 동시에
    /// <see cref="Movement"/>다.
    /// </para>
    /// </summary>
    [Flags]
    public enum AbilityTag
    {
        None = 0,

        /// <summary>한순간에 끝난다. 폭탄 투하처럼.</summary>
        Instant = 1 << 0,

        /// <summary>누르고 있는 동안 이어진다. 기총처럼.</summary>
        Sustained = 1 << 1,

        /// <summary>기체가 나아가는 방식을 바꾼다. 그래플이나 부스터처럼.</summary>
        Movement = 1 << 2,

        /// <summary>무기를 쓴다.</summary>
        Weapon = 1 << 3,

        /// <summary>손이 조종에서 떠난다. 커맨드 입력처럼.</summary>
        HandsOff = 1 << 4,

        /// <summary>표적이 잡혀 있어야 한다.</summary>
        NeedsTarget = 1 << 5,
    }
}
