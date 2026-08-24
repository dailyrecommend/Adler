using System.Collections.Generic;
using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 정해둔 상대를 잠시 통과시킨다.
    /// <para>
    /// 줄로 끌어당겨 코앞까지 붙이는 것과 몸으로 들이받고 지나가는 것은 서로 다른 일인데,
    /// 둘 다 "이 상대와 잠깐 부딪히지 않는다"를 필요로 한다. 각자 구현하면 같은 함정을
    /// 두 번 밟게 된다 — 실제로 그랬다.
    /// </para>
    /// <para>
    /// 되돌리는 시점이 이 물건의 전부다. 겹쳐 있는 동안 되살리면 물리가 둘을 떼어내며
    /// 세게 튕겨내므로, 충분히 벌어질 때까지 기다려야 한다. 그런데 상대가 나란히 날거나
    /// 제자리에 멈춰 있으면 영영 벌어지지 않아서 기한도 함께 둬야 한다.
    /// </para>
    /// <para>
    /// 짝을 기억해뒀다가 그대로 되돌린다. 되돌릴 때 다시 찾으면 그 사이에 부서져 사라진
    /// 콜라이더를 놓치고, 그 짝이 남아 다음에 같은 상대를 만났을 때 이유 없이 통과한다.
    /// </para>
    /// <para>
    /// <see cref="Physics.IgnoreCollision(Collider, Collider, bool)"/>은 누가 몇 번 껐는지
    /// 세지 않는다. 그러므로 한 짝의 주인은 하나여야 하고, 이 물건을 둘이 나눠 쓰면서
    /// 같은 상대를 각자 열면 먼저 닫는 쪽이 다른 쪽의 기록을 거짓말로 만든다.
    /// </para>
    /// </summary>
    public sealed class CollisionPassage
    {
        private readonly Transform _owner;
        private readonly List<Pass> _passes = new();
        private readonly List<Collider> _mine = new();
        private readonly List<Collider> _theirs = new();

        public CollisionPassage(Transform owner) => _owner = owner;

        /// <summary>지금 통과시키고 있는 상대의 수.</summary>
        public int Count => _passes.Count;

        /// <summary>이 상대를 통과시키는 중인지.</summary>
        public bool Holds(Transform target) => IndexOf(target) >= 0;

        /// <summary>
        /// 이 상대를 통과시키기 시작한다. 놓아줄 때까지 계속 열려 있다.
        /// <para>
        /// 이미 열려 있으면 아무 일도 하지 않는다. 다시 열면 짝이 두 벌 쌓이는데,
        /// 되돌릴 때 한 벌만 지워도 물리는 이미 켜져 있어 남은 한 벌이 거짓이 된다.
        /// </para>
        /// </summary>
        public void Open(Transform target)
        {
            if (target == null || Holds(target))
            {
                return;
            }

            _owner.GetComponentsInChildren(includeInactive: false, _mine);
            target.root.GetComponentsInChildren(includeInactive: false, _theirs);

            if (_mine.Count == 0 || _theirs.Count == 0)
            {
                return;
            }

            Pass pass = new(target, _mine.Count * _theirs.Count);

            foreach (Collider mine in _mine)
            {
                foreach (Collider theirs in _theirs)
                {
                    Physics.IgnoreCollision(mine, theirs, true);
                    pass.Pairs.Add((mine, theirs));
                }
            }

            _passes.Add(pass);
        }

        /// <summary>
        /// 이제 되돌려도 된다고 알린다. 다만 곧바로 되돌리지는 않는다.
        /// </summary>
        /// <param name="clearRange">이만큼 벌어지면 되돌린다(m).</param>
        /// <param name="timeout">벌어지지 않아도 이 시간이 지나면 되돌린다(초).</param>
        public void Release(Transform target, float clearRange, float timeout)
        {
            int index = IndexOf(target);

            if (index >= 0)
            {
                _passes[index] = _passes[index].Releasing(clearRange, timeout);
            }
        }

        /// <summary>열자마자 놓아준다. 스쳐 지나가는 것에 쓴다.</summary>
        public void Cross(Transform target, float clearRange, float timeout)
        {
            Open(target);
            Release(target, clearRange, timeout);
        }

        /// <summary>기다리지 않고 지금 되돌린다.</summary>
        public void Close(Transform target)
        {
            int index = IndexOf(target);

            if (index >= 0)
            {
                Restore(index);
            }
        }

        /// <summary>
        /// 전부 되돌린다. 꺼지거나 사라질 때 부른다.
        /// <para>
        /// 짝을 남긴 채 사라지면 다음에 같은 상대를 만났을 때 이유 없이 통과한다.
        /// </para>
        /// </summary>
        public void CloseAll()
        {
            for (int i = _passes.Count - 1; i >= 0; i--)
            {
                Restore(i);
            }
        }

        /// <summary>쓰는 쪽이 자기 시계로 밀어준다.</summary>
        public void Tick(float delta)
        {
            for (int i = _passes.Count - 1; i >= 0; i--)
            {
                Pass pass = _passes[i];

                // 사라졌거나 꺼진 상대는 기다릴 것이 없다. 되돌릴 짝도 이미 없다.
                if (pass.Target == null || !pass.Target.gameObject.activeInHierarchy)
                {
                    Restore(i);
                    continue;
                }

                if (!pass.IsReleasing)
                {
                    continue;
                }

                pass = pass.Aged(delta);
                _passes[i] = pass;

                bool apart = Vector3.Distance(pass.Target.position, _owner.position) > pass.ClearRange;

                if (apart || pass.Remaining <= 0f)
                {
                    Restore(i);
                }
            }
        }

        private void Restore(int index)
        {
            foreach ((Collider mine, Collider theirs) in _passes[index].Pairs)
            {
                if (mine != null && theirs != null)
                {
                    Physics.IgnoreCollision(mine, theirs, false);
                }
            }

            _passes.RemoveAt(index);
        }

        private int IndexOf(Transform target)
        {
            if (target == null)
            {
                return -1;
            }

            for (int i = 0; i < _passes.Count; i++)
            {
                if (_passes[i].Target == target)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>통과시키는 상대 하나와, 되돌려야 할 짝들.</summary>
        private readonly struct Pass
        {
            public readonly Transform Target;
            public readonly List<(Collider Mine, Collider Theirs)> Pairs;
            public readonly bool IsReleasing;
            public readonly float ClearRange;
            public readonly float Remaining;

            public Pass(Transform target, int capacity)
            {
                Target = target;
                Pairs = new List<(Collider, Collider)>(capacity);
                IsReleasing = false;
                ClearRange = 0f;
                Remaining = 0f;
            }

            private Pass(Pass from, bool releasing, float clearRange, float remaining)
            {
                Target = from.Target;
                Pairs = from.Pairs;
                IsReleasing = releasing;
                ClearRange = clearRange;
                Remaining = remaining;
            }

            public Pass Releasing(float clearRange, float timeout)
                => new(this, releasing: true, clearRange, timeout);

            public Pass Aged(float delta)
                => new(this, IsReleasing, ClearRange, Remaining - delta);
        }
    }
}
