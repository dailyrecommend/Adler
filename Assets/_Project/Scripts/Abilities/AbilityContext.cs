using Adler.Core;
using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 행동이 세상에 닿는 통로를 한데 묶은 것.
    /// <para>
    /// 행동이 기체 컴포넌트를 직접 들고 있으면 그 기체 없이는 시험할 수도, 적기에
    /// 다시 쓸 수도 없다. 필요한 것만 인터페이스로 묶어 넘기면 행동은 자기가 누구
    /// 위에서 도는지 모른 채로 남고, 같은 행동을 플레이어와 적이 함께 쓸 수 있다.
    /// </para>
    /// <para>
    /// 시계가 여기 들어 있는 이유도 같다. 행동은 <see cref="Time"/>을 읽지 않고
    /// 받은 시계를 쓰므로, 늦춰진 기체에 얹으면 그 행동도 함께 늦게 흐른다.
    /// </para>
    /// </summary>
    public readonly struct AbilityContext
    {
        /// <summary>이 행동을 쓰는 쪽.</summary>
        public readonly GameObject Owner;

        /// <summary>이 행동이 사는 시계.</summary>
        public readonly Clock Clock;

        /// <summary>움직임에 손대는 통로. 없을 수 있다.</summary>
        public readonly IMovementDriver Movement;

        /// <summary>무기를 다루는 통로. 없을 수 있다.</summary>
        public readonly IWeaponHost Weapons;

        /// <summary>표적을 물어보는 통로. 없을 수 있다.</summary>
        public readonly ITargetSource Targets;

        /// <summary>내구도를 채우는 통로. 없을 수 있다.</summary>
        public readonly IDurability Durability;

        /// <summary>무언가를 내놓는 자리. 없을 수 있다.</summary>
        public readonly IHardpoint Hardpoint;

        public AbilityContext(
            GameObject owner,
            Clock clock,
            IMovementDriver movement = null,
            IWeaponHost weapons = null,
            ITargetSource targets = null,
            IDurability durability = null,
            IHardpoint hardpoint = null)
        {
            Owner = owner;
            Clock = clock;
            Movement = movement;
            Weapons = weapons;
            Targets = targets;
            Durability = durability;
            Hardpoint = hardpoint;
        }

        /// <summary>이번 프레임에 이 행동의 시계가 흐른 양.</summary>
        public float Delta => Clock?.Delta ?? 0f;
    }
}
