using System;
using System.Collections.Generic;
using Adler.Combat;
using Adler.Flight;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 조준선 앞의 표적을 붙잡는다.
    /// <para>
    /// 미사일과 나눠둔 이유는 화면 표시 때문이다. 조준이 얼마나 진행됐는지, 무엇을 잡고
    /// 있는지는 무기 안에 갇혀 있으면 안 되고, 조준 자체는 미사일을 들지 않았을 때도
    /// 돌아가야 표적 전환이 자연스럽다.
    /// </para>
    /// <para>
    /// 조준선에서 벗어나도 곧바로 풀리지 않는다. 급기동 중에 잠깐 시야를 놓치는 것은
    /// 흔한 일이라, 그때마다 처음부터 다시 잡게 하면 미사일을 쏠 수가 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockOnTargeting : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("조준의 기준이 되는 방향. 비워두면 기체 정면을 쓴다.")]
        [SerializeField] private Transform _boresight;

        [Header("탐색")]
        [Tooltip("잡을 대상의 레이어.")]
        [SerializeField] private LayerMask _targetMask;

        [Tooltip("사이에 지형이 있으면 잡지 않는다.")]
        [SerializeField] private bool _requireLineOfSight = true;

        [SerializeField] private LayerMask _sightBlockers;

        [Tooltip("후보를 다시 훑는 간격(초). 매 프레임 돌 이유가 없다.")]
        [Min(0.05f)]
        [SerializeField] private float _scanInterval = 0.2f;

        [Header("자동 전환")]
        [Tooltip("지금 겨눈 것보다 이 각도만큼 더 가까워야 옮겨간다.\n" +
                 "0으로 두면 비슷한 각도의 두 표적 사이에서 매 프레임 오가며 조준이 차지 않는다.")]
        [Min(0f)]
        [SerializeField] private float _switchMargin = 3f;

        [Tooltip("조준이 다 찬 뒤에는 옮겨가지 않는다.\n" +
                 "끄면 완성된 조준도 더 가까운 표적이 나타나면 넘어간다.")]
        [SerializeField] private bool _retainCompletedLock = true;

        [Tooltip("표적 전환 키를 누른 뒤 이 시간 동안은 자동으로 옮기지 않는다(초).")]
        [Min(0f)]
        [SerializeField] private float _manualHoldSeconds = 2.5f;

        [Tooltip("시야 판정 광선을 표적 앞에서 이만큼 끊는다 (m).\n" +
                 "지면에 붙어 있는 표적은 겨누는 지점이 지면과 맞닿아 있어, 끝까지 쏘면 " +
                 "지면에 막힌 것으로 읽힌다.")]
        [Min(0f)]
        [SerializeField] private float _sightPadding = 0.5f;

        private readonly Collider[] _scanBuffer = new Collider[32];
        private readonly List<Collider> _candidates = new();

        private MissileDefinition _rules;
        private Collider _target;
        private float _progress;
        private float _graceRemaining;
        private float _scanTimer;
        private int _cycleIndex;
        private float _manualHoldRemaining;

        /// <summary>잡고 있는 표적이 바뀔 때. null이면 놓쳤다는 뜻.</summary>
        public event Action<Transform> TargetChanged;

        /// <summary>조준이 완성됐을 때.</summary>
        public event Action<Transform> Locked;

        /// <summary>지금 겨누고 있는 표적. 조준이 완성되지 않았을 수도 있다.</summary>
        public Transform Target => _target != null ? _target.transform : null;

        /// <summary>
        /// 겨누는 지점. 원점이 아니라 콜라이더 한가운데다.
        /// <para>
        /// 지상 표적은 원점이 바닥에 있는 경우가 많아, 그곳을 겨누면 시야 판정 광선이
        /// 지면에 막힌 것으로 읽힌다. 화면에 띄우는 표시도 발밑이 아니라 몸통에 붙어야 한다.
        /// </para>
        /// </summary>
        public Vector3 TargetPoint =>
            _target != null ? _target.bounds.center : Vector3.zero;

        /// <summary>조준 진행도. 1이면 쏠 수 있다.</summary>
        public float Progress => _progress;

        public bool HasLock => _target != null && _progress >= 1f;

        /// <summary>
        /// 미사일을 들고 있는 동안 그 미사일의 조준 규칙을 넘겨받는다.
        /// 놓으면 null이 들어와 조준이 풀린다.
        /// </summary>
        public void SetRules(MissileDefinition rules)
        {
            if (_rules == rules)
            {
                return;
            }

            _rules = rules;

            if (rules == null)
            {
                Clear();
            }
        }

        /// <summary>다음 후보로 넘어간다. 표적 전환 키가 부른다.</summary>
        public void CycleTarget()
        {
            if (_rules == null || _candidates.Count == 0)
            {
                return;
            }

            _cycleIndex = (_cycleIndex + 1) % _candidates.Count;
            _manualHoldRemaining = _manualHoldSeconds;
            SetTarget(_candidates[_cycleIndex]);
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
            if (_rules == null)
            {
                return;
            }

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                RefreshCandidates();
            }

            _manualHoldRemaining -= Time.deltaTime;

            UpdateSelection();
            UpdateProgress();
        }

        /// <summary>
        /// 조준선에 가장 가까운 표적으로 계속 옮겨간다.
        /// <para>
        /// 옮기려면 지금 겨눈 것보다 눈에 띄게 가까워야 한다. 조금이라도 가까우면 옮기게
        /// 두면 비슷한 각도의 두 표적 사이에서 매 프레임 오가고, 옮길 때마다 진행도가
        /// 0으로 돌아가므로 조준이 영영 차지 않는다.
        /// </para>
        /// </summary>
        private void UpdateSelection()
        {
            Collider best = BestCandidate();

            if (best == null || ReferenceEquals(best, _target))
            {
                return;
            }

            if (_target == null)
            {
                SetTarget(best);
                return;
            }

            // 손으로 고른 직후에는 자동 전환이 그것을 곧바로 덮어쓰지 않게 한다.
            if (_manualHoldRemaining > 0f)
            {
                return;
            }

            // 다 찬 조준은 붙잡는다. 애써 채운 것이 시선이 살짝 흔들렸다고 풀리면
            // 미사일을 쏠 수 있는 순간을 잡기 어려워진다.
            if (_retainCompletedLock && _progress >= 1f)
            {
                return;
            }

            if (AngleTo(TargetPoint) - AngleTo(best.bounds.center) < _switchMargin)
            {
                return;
            }

            SetTarget(best);
        }

        private void RefreshCandidates()
        {
            _candidates.Clear();

            int found = Physics.OverlapSphereNonAlloc(
                _boresight.position, _rules.LockRange, _scanBuffer,
                _targetMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < found; i++)
            {
                Collider candidate = _scanBuffer[i];
                IDamageable damageable = candidate.GetComponentInParent<IDamageable>();

                if (damageable == null || !damageable.IsAlive || !IsInCone(candidate.bounds.center))
                {
                    continue;
                }

                _candidates.Add(candidate);
            }

            if (_cycleIndex >= _candidates.Count)
            {
                _cycleIndex = 0;
            }
        }

        private Collider BestCandidate()
        {
            Collider best = null;
            float narrowest = float.MaxValue;

            foreach (Collider candidate in _candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                float angle = AngleTo(candidate.bounds.center);
                if (angle < narrowest)
                {
                    narrowest = angle;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 조준선 안에 있으면 차오르고, 벗어나면 잠시 버티다 풀린다.
        /// </summary>
        private void UpdateProgress()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                Clear();
                return;
            }

            bool held = IsInCone(TargetPoint);

            if (held)
            {
                _graceRemaining = _rules.LockGraceSeconds;

                if (_progress < 1f)
                {
                    float step = _rules.LockSeconds > 0f
                        ? Time.deltaTime / _rules.LockSeconds
                        : 1f;

                    _progress = Mathf.Clamp01(_progress + step);

                    if (_progress >= 1f)
                    {
                        Locked?.Invoke(Target);
                    }
                }

                return;
            }

            _graceRemaining -= Time.deltaTime;
            if (_graceRemaining <= 0f)
            {
                Clear();
            }
        }

        private bool IsInCone(Vector3 position)
        {
            Vector3 toTarget = position - _boresight.position;

            if (toTarget.sqrMagnitude > _rules.LockRange * _rules.LockRange)
            {
                return false;
            }

            if (Vector3.Angle(_boresight.forward, toTarget) > _rules.LockAngle)
            {
                return false;
            }

            return HasLineOfSight(toTarget);
        }

        /// <summary>
        /// 사이를 가로막는 것이 있는지 본다.
        /// <para>
        /// 표적 바로 앞에서 광선을 끊는다. 지면에 붙어 있는 표적은 겨누는 지점이 지면과
        /// 맞닿아 있어, 끝까지 쏘면 표적을 딛고 선 땅에 스스로 막힌 것으로 읽힌다.
        /// </para>
        /// </summary>
        private bool HasLineOfSight(Vector3 toTarget)
        {
            if (!_requireLineOfSight)
            {
                return true;
            }

            float distance = toTarget.magnitude - _sightPadding;
            if (distance <= 0.0001f)
            {
                return true;
            }

            return !Physics.Raycast(_boresight.position, toTarget.normalized, distance,
                _sightBlockers, QueryTriggerInteraction.Ignore);
        }

        private float AngleTo(Vector3 position)
            => Vector3.Angle(_boresight.forward, position - _boresight.position);

        private void SetTarget(Collider target)
        {
            if (ReferenceEquals(target, _target))
            {
                return;
            }

            _target = target;
            _progress = 0f;
            _graceRemaining = _rules != null ? _rules.LockGraceSeconds : 0f;
            TargetChanged?.Invoke(Target);
        }

        private void Clear() => SetTarget(null);

        private void OnDrawGizmosSelected()
        {
            if (_target == null)
            {
                return;
            }

            Gizmos.color = HasLock ? Color.red : Color.yellow;
            Gizmos.DrawLine(_boresight != null ? _boresight.position : transform.position, TargetPoint);
        }
    }
}
