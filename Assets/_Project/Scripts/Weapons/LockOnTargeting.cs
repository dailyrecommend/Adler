using System;
using System.Collections.Generic;
using Adler.Combat;
using Adler.Flight;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 조준선 앞에 둔 적들 중 가장 가운데에 가까운 것을 잡는다.
    /// <para>
    /// 두 단계로 나뉜다. 조준선 안에 <b>얼마 동안</b> 두면 잡을 수 있는 상태가 되고,
    /// 그렇게 된 것들 중 조준선에 가장 가까운 하나가 실제로 잡힌다. 앞은 시간이
    /// 들지만 뒤는 즉시다.
    /// </para>
    /// <para>
    /// 시간을 앞쪽에 두는 이유는, 뒤쪽에 두면 겨눌 대상을 바꿀 때마다 처음부터 다시
    /// 기다려야 하기 때문이다. 여러 대를 상대하는 동안 시선을 옮기는 것은 계속
    /// 일어나는 일이라, 그때마다 벌을 주면 한 대씩 처리하는 것 외에 다른 방법이
    /// 없어진다. 앞쪽에 두면 값은 한 번만 치르고, 치러둔 것들 사이는 자유롭다.
    /// </para>
    /// <para>
    /// 카메라가 아니라 조준선을 기준으로 잰다. 카메라는 둘러보기로 따로 움직이므로
    /// 그쪽을 기준으로 하면 고개를 돌리는 것만으로 잡히는 것이 바뀌고, 화면 구석에
    /// 걸쳐 있을 뿐 겨누지도 않은 적이 후보가 된다. 잡는 것은 기수를 돌려서 하는
    /// 일이어야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockOnTargeting : MonoBehaviour
    {
        /// <summary>
        /// 화면에 표시할 표적 하나의 이번 프레임 상태.
        /// <para>
        /// 겨눌 자리를 함께 담는 이유는 그것이 매 프레임 달라지기 때문이다. 화면 표시가
        /// 스스로 구하려면 콜라이더를 넘겨줘야 하는데, 그러면 표시가 물리를 알게 된다.
        /// </para>
        /// </summary>
        public readonly struct LockMark
        {
            public readonly Transform Target;

            /// <summary>표적의 한가운데. 표시를 붙일 자리다.</summary>
            public readonly Vector3 Point;

            /// <summary>잡을 수 있게 되기까지의 진행도. 1이면 다 찼다.</summary>
            public readonly float Progress;

            /// <summary>지금 잡혀 있는 그 표적인지.</summary>
            public readonly bool Locked;

            /// <summary>조준선에서 벗어난 각도. 0이면 정확히 겨누고 있다.</summary>
            public readonly float Angle;

            public LockMark(Transform target, Vector3 point, float progress, bool locked, float angle)
            {
                Target = target;
                Point = point;
                Progress = progress;
                Locked = locked;
                Angle = angle;
            }

            /// <summary>진행도가 다 차서 잡을 수 있는 상태인지.</summary>
            public bool Lockable => Progress >= 1f;
        }

        /// <summary>지켜보고 있는 표적 하나. 조준선 안에 얼마나 두었는지를 들고 있는다.</summary>
        private struct Tracked
        {
            public Collider Target;
            public float Progress;

            // 이번 프레임에 다시 잰 것들. 한 번 구해서 여러 번 쓴다.
            public bool InSight;
            public float Angle;
        }

        [Header("참조")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("조준선. 기총의 총구를 넣으면 탄이 가는 곳과 잡는 곳이 같아진다.\n" +
                 "비워두면 기체의 정면을 쓴다.")]
        [SerializeField] private Transform _boresight;

        [Header("탐색")]
        [Tooltip("잡을 대상의 레이어.")]
        [SerializeField] private LayerMask _targetMask;

        [Tooltip("조준선에서 이 각도 안에 있어야 센다.\n\n" +
                 "화면에 담기는 범위쯤으로 잡으면 보이는 것이 곧 잡히는 것이 되어 규칙이\n" +
                 "눈에 익는다. 좁히면 정확히 겨눠야 하고, 넓히면 등 뒤의 적까지 잡힌다.")]
        [Range(1f, 90f)]
        [SerializeField] private float _cone = 45f;

        [Tooltip("이 거리 안의 표적만 잡는다 (m).\n" +
                 "겨누고 있더라도 너무 멀면 점에 불과해 잡아봐야 할 일이 없다.")]
        [Min(1f)]
        [SerializeField] private float _range = 400f;

        [Tooltip("사이에 지형이 있으면 세지 않는다.")]
        [SerializeField] private bool _requireLineOfSight = true;

        [SerializeField] private LayerMask _sightBlockers;

        [Tooltip("시야 판정 광선을 표적 앞에서 이만큼 끊는다 (m).\n" +
                 "지면에 붙어 있는 표적은 겨누는 지점이 지면과 맞닿아 있어, 끝까지 쏘면 " +
                 "지면에 막힌 것으로 읽힌다.")]
        [Min(0f)]
        [SerializeField] private float _sightPadding = 0.5f;

        [Tooltip("후보를 다시 훑는 간격(초). 매 프레임 돌 이유가 없다.")]
        [Min(0.02f)]
        [SerializeField] private float _scanInterval = 0.15f;

        [Header("잡을 수 있게 되기까지")]
        [Tooltip("조준선 안에 이만큼 두어야 잡을 수 있게 된다(초).\n\n" +
                 "이 값은 표적마다 따로 쌓인다. 한 번 다 찬 표적들 사이를 오가는 데는\n" +
                 "시간이 들지 않는다 — 값은 겨누는 일에만 치른다.")]
        [Min(0f)]
        [SerializeField] private float _acquireSeconds = 0.8f;

        [Tooltip("사거리 끝에서는 잡는 데 이만큼 배로 오래 걸린다.\n\n" +
                 "멀수록 화면에서 작고 덜 위태로우므로, 같은 값으로 잡히면 눈앞에 붙은\n" +
                 "적과 지평선의 점이 같은 무게를 갖게 된다. 거리를 좁히는 일에 값을\n" +
                 "주려면 가까울수록 빨리 잡혀야 한다.\n\n" +
                 "1로 두면 거리와 무관해진다.")]
        [Min(1f)]
        [SerializeField] private float _farAcquireMultiplier = 3f;

        [Tooltip("조준선에서 벗어난 뒤 쌓인 것이 다 풀리기까지의 시간(초).\n\n" +
                 "쌓이는 시간보다 길게 둘 것. 급기동 중에 잠깐 놓치는 것은 흔한 일이라,\n" +
                 "같거나 짧으면 한 번 스칠 때마다 처음으로 돌아간다.")]
        [Min(0.05f)]
        [SerializeField] private float _forgetSeconds = 2f;

        [Header("고르기")]
        [Tooltip("지금 잡은 것보다 조준선에 이만큼 더 가까워야 옮겨간다 (도).\n\n" +
                 "0으로 두면 비슷한 자리의 두 표적 사이에서 매 프레임 오가며 표시가\n" +
                 "떨린다. 실제로 겨눈 것이 바뀔 때만 옮겨가게 하는 몫이다.")]
        [Min(0f)]
        [SerializeField] private float _switchMargin = 3f;

        [Tooltip("잡을 것이 하나도 없어도 이만큼은 놓지 않는다(초).\n\n" +
                 "급기동 중에 잠깐 조준선을 벗어나는 것은 흔한 일이라, 그때마다 놓으면\n" +
                 "표시가 깜빡이고 그래플을 걸 수 없다.")]
        [Min(0f)]
        [SerializeField] private float _holdSeconds = 0.4f;

        [Tooltip("표적 전환 키를 누른 뒤 이 시간 동안은 저절로 옮기지 않는다(초).")]
        [Min(0f)]
        [SerializeField] private float _manualHoldSeconds = 2.5f;

        private readonly Collider[] _scanBuffer = new Collider[32];
        private readonly List<Tracked> _tracked = new();
        private readonly List<LockMark> _marks = new();

        private Collider _target;
        private float _holdRemaining;
        private float _scanTimer;
        private int _cycleIndex;
        private float _manualHoldRemaining;

        /// <summary>잡은 표적이 바뀔 때. null이면 놓쳤다는 뜻.</summary>
        public event Action<Transform> TargetChanged;

        /// <summary>지금 잡고 있는 표적. 없으면 null.</summary>
        public Transform Target => _target != null ? _target.transform : null;

        /// <summary>
        /// 겨누는 지점. 원점이 아니라 콜라이더 한가운데다.
        /// <para>
        /// 지상 표적은 원점이 바닥에 있는 경우가 많아, 그곳을 겨누면 시야 판정 광선이
        /// 지면에 막힌 것으로 읽힌다. 화면에 띄우는 표시도 발밑이 아니라 몸통에 붙어야 한다.
        /// </para>
        /// </summary>
        public Vector3 TargetPoint => _target != null ? _target.bounds.center : Vector3.zero;

        /// <summary>잡은 것이 있으면 곧 걸린 것이다. 잡히는 데는 시간이 들지 않는다.</summary>
        public bool HasLock => _target != null;

        /// <summary>조준 진행도. 잡히는 데 시간이 들지 않으므로 0 아니면 1이다.</summary>
        public float Progress => HasLock ? 1f : 0f;

        /// <summary>
        /// 지금 조준선 안에 있는 표적들. 매 프레임 다시 만든다.
        /// <para>
        /// 잡을 수 있게 된 것만이 아니라 차오르는 중인 것도 담는다. 표시가 차오르는
        /// 과정을 그릴 수 있어야, 언제부터 잡히는지를 기다리며 배울 수 있다.
        /// </para>
        /// </summary>
        public IReadOnlyList<LockMark> Marks => _marks;

        /// <summary>다음 후보로 넘어간다. 표적 전환 키가 부른다.</summary>
        public void CycleTarget()
        {
            int count = 0;
            for (int i = 0; i < _tracked.Count; i++)
            {
                if (IsSelectable(_tracked[i]))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return;
            }

            _cycleIndex = (_cycleIndex + 1) % count;
            _manualHoldRemaining = _manualHoldSeconds;

            int seen = 0;
            for (int i = 0; i < _tracked.Count; i++)
            {
                if (!IsSelectable(_tracked[i]))
                {
                    continue;
                }

                if (seen++ == _cycleIndex)
                {
                    SetTarget(_tracked[i].Target);
                    return;
                }
            }
        }

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_boresight == null)
            {
                _boresight = _aircraft != null ? _aircraft.transform : transform;
            }
        }

        private void Update()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                Rescan();
            }

            _manualHoldRemaining -= Time.deltaTime;

            UpdateTracked(Time.deltaTime);
            UpdateSelection();
            BuildMarks();
        }

        // ------------------------------------------------------------------
        // 지켜보기
        // ------------------------------------------------------------------

        /// <summary>
        /// 주변을 훑어 새로 들어온 것을 지켜보기 시작하고, 사라진 것을 지운다.
        /// <para>
        /// 훑을 때 조준선 판정은 하지 않는다. 벗어난 것도 계속 지켜봐야 쌓아둔 것이
        /// 서서히 풀린다 — 여기서 빼버리면 조준선이 스칠 때마다 처음부터 다시 쌓게 된다.
        /// </para>
        /// </summary>
        private void Rescan()
        {
            int found = Physics.OverlapSphereNonAlloc(
                Origin, _range, _scanBuffer, _targetMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < found; i++)
            {
                Collider candidate = _scanBuffer[i];

                if (IsAlive(candidate) && IndexOf(candidate) < 0)
                {
                    _tracked.Add(new Tracked { Target = candidate });
                }
            }

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                Collider target = _tracked[i].Target;

                if (IsAlive(target) && WithinRange(target))
                {
                    continue;
                }

                _tracked.RemoveAt(i);

                if (ReferenceEquals(target, _target))
                {
                    SetTarget(null);
                }
            }
        }

        /// <summary>
        /// 조준선 안에 있는 동안 차오르고, 벗어나 있는 동안 풀린다.
        /// <para>
        /// 차오르는 속도는 거리에 따라 다르다. 멀수록 오래 걸리므로, 지평선의 점을
        /// 겨누고 기다리는 것보다 다가가는 편이 빠르다.
        /// </para>
        /// </summary>
        private void UpdateTracked(float deltaTime)
        {
            float loss = deltaTime / _forgetSeconds;

            for (int i = 0; i < _tracked.Count; i++)
            {
                Tracked entry = _tracked[i];
                Vector3 offset = entry.Target.bounds.center - Origin;

                entry.Angle = Vector3.Angle(_boresight.forward, offset);
                entry.InSight = entry.Angle <= _cone && HasLineOfSight(offset);

                entry.Progress = Mathf.Clamp01(entry.Progress
                    + (entry.InSight ? GainFor(offset.magnitude, deltaTime) : -loss));

                _tracked[i] = entry;
            }
        }

        /// <summary>이번 프레임에 차오를 몫. 사거리 끝에 가까울수록 적다.</summary>
        private float GainFor(float distance, float deltaTime)
        {
            if (_acquireSeconds <= 0f)
            {
                return 1f;
            }

            float far = Mathf.Lerp(1f, _farAcquireMultiplier, Mathf.Clamp01(distance / _range));

            return deltaTime / (_acquireSeconds * far);
        }

        // ------------------------------------------------------------------
        // 고르기
        // ------------------------------------------------------------------

        /// <summary>
        /// 잡을 수 있게 된 것들 중 조준선에 가장 가까운 것으로 옮겨간다.
        /// <para>
        /// 하나도 없을 때만 버틴다. 다른 것이 앞에 있으면 곧바로 그쪽으로 넘어가야 한다 —
        /// 붙잡아 두는 것은 놓치지 않으려는 몫이지, 더 나은 표적이 앞에 있는데 옛것을
        /// 붙들고 있으라는 뜻이 아니다.
        /// </para>
        /// </summary>
        private void UpdateSelection()
        {
            int best = BestIndex();

            if (best < 0)
            {
                Hold();
                return;
            }

            _holdRemaining = _holdSeconds;

            Collider winner = _tracked[best].Target;
            if (ReferenceEquals(winner, _target))
            {
                return;
            }

            // 잡고 있던 것이 더는 고를 수 있는 상태가 아니면 재지 않고 곧바로 옮긴다.
            int current = IndexOf(_target);

            if (current >= 0 && IsSelectable(_tracked[current]))
            {
                if (_manualHoldRemaining > 0f)
                {
                    return;
                }

                if (_tracked[current].Angle - _tracked[best].Angle < _switchMargin)
                {
                    return;
                }
            }

            SetTarget(winner);
        }

        private int BestIndex()
        {
            int best = -1;
            float narrowest = float.MaxValue;

            for (int i = 0; i < _tracked.Count; i++)
            {
                Tracked entry = _tracked[i];

                if (!IsSelectable(entry) || entry.Angle >= narrowest)
                {
                    continue;
                }

                narrowest = entry.Angle;
                best = i;
            }

            return best;
        }

        /// <summary>잡을 것이 없는 동안 잠깐 붙들고 있다가 놓는다.</summary>
        private void Hold()
        {
            if (_target == null)
            {
                return;
            }

            _holdRemaining -= Time.deltaTime;

            if (_holdRemaining <= 0f)
            {
                SetTarget(null);
            }
        }

        private static bool IsSelectable(Tracked entry) => entry.InSight && entry.Progress >= 1f;

        private void SetTarget(Collider target)
        {
            if (ReferenceEquals(target, _target))
            {
                return;
            }

            _target = target;
            _holdRemaining = _holdSeconds;
            TargetChanged?.Invoke(Target);
        }

        // ------------------------------------------------------------------
        // 화면 표시
        // ------------------------------------------------------------------

        /// <summary>
        /// 조준선에 가까운 순으로 담는다.
        /// <para>
        /// 화면 표시는 띄울 수 있는 수에 한계가 있는데, 넘칠 때 잘려 나가는 것이 훑은
        /// 차례에 달려 있으면 정작 겨누고 있는 적의 표식이 사라질 수 있다. 넣을 자리를
        /// 찾아 끼우는 것은 표적이 열 남짓일 때 정렬을 부르는 것보다 싸고, 쓰레기도
        /// 남기지 않는다.
        /// </para>
        /// </summary>
        private void BuildMarks()
        {
            _marks.Clear();

            foreach (Tracked entry in _tracked)
            {
                if (!entry.InSight)
                {
                    continue;
                }

                LockMark mark = new(
                    entry.Target.transform,
                    entry.Target.bounds.center,
                    entry.Progress,
                    ReferenceEquals(entry.Target, _target),
                    entry.Angle);

                int at = _marks.Count;
                while (at > 0 && _marks[at - 1].Angle > mark.Angle)
                {
                    at--;
                }

                _marks.Insert(at, mark);
            }
        }

        // ------------------------------------------------------------------
        // 판정
        // ------------------------------------------------------------------

        private int IndexOf(Collider target)
        {
            if (target == null)
            {
                return -1;
            }

            for (int i = 0; i < _tracked.Count; i++)
            {
                if (ReferenceEquals(_tracked[i].Target, target))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsAlive(Collider candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                return false;
            }

            IDamageable damageable = candidate.GetComponentInParent<IDamageable>();

            return damageable != null && damageable.IsAlive;
        }

        private bool WithinRange(Collider candidate)
            => (candidate.bounds.center - Origin).sqrMagnitude <= _range * _range;

        /// <summary>
        /// 사이를 가로막는 것이 있는지 본다.
        /// <para>
        /// 표적 바로 앞에서 광선을 끊는다. 지면에 붙어 있는 표적은 겨누는 지점이 지면과
        /// 맞닿아 있어, 끝까지 쏘면 표적을 딛고 선 땅에 스스로 막힌 것으로 읽힌다.
        /// </para>
        /// </summary>
        private bool HasLineOfSight(Vector3 offset)
        {
            if (!_requireLineOfSight)
            {
                return true;
            }

            float distance = offset.magnitude - _sightPadding;
            if (distance <= 0.0001f)
            {
                return true;
            }

            return !Physics.Raycast(Origin, offset.normalized, distance,
                _sightBlockers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>재는 자리. 조준선이 나가는 곳이다.</summary>
        private Vector3 Origin => _boresight != null ? _boresight.position : transform.position;

        private void OnDrawGizmosSelected()
        {
            if (_target == null)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(Origin, TargetPoint);
        }
    }
}
