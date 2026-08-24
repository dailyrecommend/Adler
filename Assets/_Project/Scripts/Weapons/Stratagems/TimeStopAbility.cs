using System.Collections.Generic;
using Adler.Abilities;
using Adler.Core;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 쓴 사람을 뺀 나머지의 시간을 세운다.
    /// <para>
    /// 세상 시계는 건드리지 않는다. 세상을 늦추고 자기를 되세우는 방법도 있지만, 그러면
    /// 되세우는 배율만큼 물리에 실리는 속도가 커지고 유니티가 그것을 각속도 한도에서
    /// 잘라낸다 — 멈추라고 시켰는데 정작 내 선회가 굼떠진다. 보정하는 자리가 하나 생길
    /// 때마다 엔진과 싸울 곳이 하나씩 는다.
    /// </para>
    /// <para>
    /// 늦출 것만 늦추면 그런 자리가 없다. 플레이어도 카메라도 물리도 손대지 않으므로
    /// 잘릴 것이 없고, 나누는 쪽이 세상 배율로 남아 있어서 <b>배율을 0으로 둘 수 있다</b> —
    /// 흉내가 아니라 실제로 선다.
    /// </para>
    /// <para>
    /// 무엇을 멈출지는 목록으로 들고 있지 않다. 시계를 가진 것들이 스스로 등록해 두므로
    /// 그것을 훑기만 하면 되고, 적을 새로 만들어도 여기는 그대로다. 대신 늦추고 싶은
    /// 것에는 <see cref="TimeScale"/>이 붙어 있어야 한다.
    /// </para>
    /// </summary>
    public sealed class TimeStopAbility : Ability
    {
        private readonly TimeStopDefinition _stop;
        private readonly List<Held> _held = new();

        private Transform _user;

        public TimeStopAbility(TimeStopDefinition stop) : base(stop) => _stop = stop;

        protected override void OnBegin(in AbilityContext context)
        {
            _held.Clear();
            _user = context.Owner != null ? context.Owner.transform : null;

            Seize();
        }

        /// <summary>
        /// 멈춘 뒤에 태어난 것도 붙잡는다.
        /// <para>
        /// 시작할 때 한 번만 훑으면 그사이 나타난 것이 멈춘 세상을 홀로 가로지른다.
        /// 몇 개 되지 않는 목록이라 매 프레임 훑어도 값이 싸다.
        /// </para>
        /// </summary>
        protected override void OnActive(in AbilityContext context) => Seize();

        /// <summary>
        /// 붙잡은 것을 모두 놓아준다.
        /// <para>
        /// 1로 밀지 않고 붙잡기 전의 값으로 되돌린다. 디버프가 이미 늦춰둔 적이 있으면
        /// 1로 밀어버리는 순간 그 디버프가 조용히 풀린다.
        /// </para>
        /// <para>
        /// 시간이 다 되든 격추당해 끊기든 반드시 여기를 지난다. 멈춘 채로 남으면
        /// 그 판이 끝나므로, 되돌리는 길은 하나뿐이어야 한다.
        /// </para>
        /// </summary>
        protected override void OnEnd(in AbilityContext context)
        {
            foreach (Held held in _held)
            {
                // 멈춰 있는 동안 격추되어 사라졌을 수 있다.
                if (held.Clock != null)
                {
                    held.Clock.Scale = held.Was;
                }
            }

            _held.Clear();
            _user = null;
        }

        private void Seize()
        {
            IReadOnlyList<TimeScale> all = TimeScale.All;

            for (int i = 0; i < all.Count; i++)
            {
                TimeScale clock = all[i];

                if (clock == null || IsMine(clock) || AlreadyHeld(clock))
                {
                    continue;
                }

                _held.Add(new Held(clock, clock.Scale));
                clock.Scale = _stop.StopScale;
            }
        }

        /// <summary>
        /// 쓴 사람의 것인지. 기체 아래에 시계가 여럿 걸려 있어도 전부 걸러진다.
        /// </summary>
        private bool IsMine(TimeScale clock)
            => _user != null && clock.transform.IsChildOf(_user);

        private bool AlreadyHeld(TimeScale clock)
        {
            for (int i = 0; i < _held.Count; i++)
            {
                if (_held[i].Clock == clock)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>붙잡은 시계 하나와 붙잡기 전의 배율.</summary>
        private readonly struct Held
        {
            public readonly TimeScale Clock;
            public readonly float Was;

            public Held(TimeScale clock, float was)
            {
                Clock = clock;
                Was = was;
            }
        }
    }
}
