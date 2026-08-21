using System;
using System.Collections.Generic;
using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 기체가 지금 받고 있는 나쁜 상태들을 모아 둔다.
    /// <para>
    /// 화면 표시가 상태마다 다른 곳을 들여다보게 두면, 디버프를 하나 늘릴 때마다 UI를
    /// 고쳐야 한다. 여기 한 곳만 보게 하면 그럴 일이 없다.
    /// </para>
    /// <para>
    /// 상태는 두 가지 방식으로 들어온다. 이미 그 상태를 알고 있는 시스템은
    /// <see cref="IDebuffSource"/>를 구현해 스스로 내놓고 — 봉인이 그렇다 — 밖에서
    /// 걸어주는 것은 <see cref="Apply"/>로 시간을 정해 건다. 둘 다 새 컴포넌트를
    /// 만들 필요가 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftDebuffs : MonoBehaviour
    {
        private IDebuffSource[] _sources;

        private readonly List<DebuffDefinition> _active = new();
        private readonly List<DebuffDefinition> _scratch = new();
        private readonly Dictionary<DebuffDefinition, float> _timed = new();
        private readonly List<DebuffDefinition> _expired = new();

        /// <summary>목록이 바뀔 때. 화면 표시가 구독한다.</summary>
        public event Action Changed;

        /// <summary>지금 걸려 있는 것들. 급한 것이 앞에 온다.</summary>
        public IReadOnlyList<DebuffDefinition> Active => _active;

        public bool IsActive(DebuffDefinition debuff)
            => debuff != null && _active.Contains(debuff);

        private void Awake()
        {
            // 기체 위에 있는 것들을 한 번만 찾아둔다. 새 시스템이 디버프를 걸게 되면
            // 인터페이스만 구현하면 되고, 여기도 화면도 고칠 것이 없다.
            _sources = GetComponentsInChildren<IDebuffSource>(includeInactive: true);
        }

        /// <summary>
        /// 시간이 정해진 상태를 건다. 이미 걸려 있으면 남은 시간을 늘린다.
        /// <para>
        /// 겹쳐 쌓지 않고 긴 쪽을 남긴다. 짧은 것이 뒤에 와서 남은 시간을 줄이면,
        /// 맞을수록 빨리 풀리는 이상한 일이 생긴다.
        /// </para>
        /// </summary>
        public void Apply(DebuffDefinition debuff, float seconds)
        {
            if (debuff == null || seconds <= 0f)
            {
                return;
            }

            float until = Time.time + seconds;

            if (!_timed.TryGetValue(debuff, out float existing) || until > existing)
            {
                _timed[debuff] = until;
            }
        }

        /// <summary>시간이 정해진 상태를 일찍 걷어낸다. 스스로 내놓는 것은 건드리지 못한다.</summary>
        public void Remove(DebuffDefinition debuff)
        {
            if (debuff != null)
            {
                _timed.Remove(debuff);
            }
        }

        /// <summary>걸어둔 것을 전부 걷어낸다. 재출격처럼 상태를 처음으로 돌릴 때 쓴다.</summary>
        public void Clear() => _timed.Clear();

        /// <summary>
        /// 매 프레임 지금 걸린 것들을 다시 모은다.
        /// <para>
        /// 신호를 주고받는 대신 물어본다. 상태를 가진 쪽이 켜고 끄는 순간을 빠짐없이
        /// 알려야 하는 구조는, 어느 한 곳이 알리는 것을 잊으면 화면에 유령이 남는다.
        /// 몇 개 안 되는 것을 매 프레임 물어보는 편이 늘 맞다.
        /// </para>
        /// </summary>
        private void Update()
        {
            _scratch.Clear();

            foreach (IDebuffSource source in _sources)
            {
                source.CollectDebuffs(_scratch);
            }

            DebuffZone.CollectFor(this, _scratch);
            CollectTimed();

            // 같은 것을 둘이 내놓을 수 있다. 구름 구역과 야간이 함께 시야를 막는 식으로.
            // 그때 줄이 두 개 뜨면 무엇이 다른지 읽으려다 시간을 버린다.
            Dedup();

            // 순서를 정해두지 않으면 모이는 차례대로 쌓여서, 같은 상황인데도 볼 때마다
            // 줄이 뒤바뀐다. 자리가 고정되어야 눈이 위치로 기억한다.
            _scratch.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));

            if (SameAsActive())
            {
                return;
            }

            _active.Clear();
            _active.AddRange(_scratch);
            Changed?.Invoke();
        }

        private void Dedup()
        {
            for (int i = _scratch.Count - 1; i >= 0; i--)
            {
                if (_scratch[i] == null || _scratch.IndexOf(_scratch[i]) != i)
                {
                    _scratch.RemoveAt(i);
                }
            }
        }

        private void CollectTimed()
        {
            _expired.Clear();

            foreach (KeyValuePair<DebuffDefinition, float> pair in _timed)
            {
                if (Time.time >= pair.Value)
                {
                    _expired.Add(pair.Key);
                }
                else if (!_scratch.Contains(pair.Key))
                {
                    _scratch.Add(pair.Key);
                }
            }

            foreach (DebuffDefinition debuff in _expired)
            {
                _timed.Remove(debuff);
            }
        }

        private bool SameAsActive()
        {
            if (_scratch.Count != _active.Count)
            {
                return false;
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                if (_scratch[i] != _active[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
