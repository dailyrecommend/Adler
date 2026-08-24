using System;
using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 지대공 미사일 발사대. 멀리서 접근을 막는다.
    /// <para>
    /// 대공포와 하는 일이 다르다. 대공포는 이미 들어온 기체를 괴롭히는 것이고, 이쪽은
    /// 들어오기 전에 <b>어느 길로 갈 것인가</b>를 묻는다. 그래서 사거리가 훨씬 길다.
    /// </para>
    /// <para>
    /// 대응 수단이 둘이다. 발사 전에는 지형에 몸을 숨겨 시야를 끊으면 조준이 풀리고,
    /// 이미 날아온 뒤에는 급선회나 플레어로 떨궈야 한다. 앞의 것은 어디로 갈지의 문제고
    /// 뒤의 것은 언제 꺾을지의 문제라, 하나를 익혀도 다른 하나가 남는다.
    /// </para>
    /// <para>
    /// 코앞은 쏘지 못한다. 실제 발사대의 최소 사거리를 흉내 낸 것이지만, 게임에서는
    /// "붙어버리면 안전하다"는 답을 하나 만들어 주는 쪽이 더 중요하다. 그 답이 없으면
    /// 사거리 안의 모든 자리가 똑같이 위험해서 판단할 것이 없어진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SamSite : MonoBehaviour
    {
        private static readonly List<SamSite> Active = new();

        [Header("무장")]
        [SerializeField] private MissileDefinition _missile;

        [Tooltip("미사일이 나가는 자리. 여러 개면 번갈아 쓴다. 비우면 이 오브젝트에서 나간다.")]
        [SerializeField] private Transform[] _launchPoints = Array.Empty<Transform>();

        [Tooltip("폭발이 닿을 레이어. 자기 진영은 빼둘 것.")]
        [SerializeField] private LayerMask _blastMask = ~0;

        [Header("교전")]
        [Tooltip("이 안에 들어온 기체를 노린다 (m). 대공포보다 훨씬 길게 잡는다.")]
        [Min(1f)]
        [SerializeField] private float _range = 700f;

        [Tooltip("이보다 가까우면 쏘지 못한다 (m). 붙어버리는 것이 답이 되게 하는 값이다.")]
        [Min(0f)]
        [SerializeField] private float _minRange = 80f;

        [Tooltip("노릴 대상의 레이어. 플레이어 기체를 넣는다.")]
        [SerializeField] private LayerMask _targetMask;

        [Tooltip("이것이 부서지면 발사대가 멈춘다. 비워두면 자기 내구도를 쓴다.")]
        [SerializeField] private Health _health;

        [Header("시야")]
        [Tooltip("사이에 지형이 끼면 조준이 풀린다. 이것이 지형에 숨는 길을 만든다.")]
        [SerializeField] private bool _requireLineOfSight = true;

        [SerializeField] private LayerMask _sightBlockers;

        [Header("조준")]
        [Tooltip("조준이 완성되기까지 걸리는 시간(초).\n" +
                 "이 시간이 예고다. 없으면 미사일이 아무 신호 없이 날아와 불합리하게 느껴진다.")]
        [Min(0.1f)]
        [SerializeField] private float _lockSeconds = 3f;

        [Tooltip("조준이 끊겼을 때 진행도가 빠지는 빠르기(배).\n" +
                 "1이면 쌓인 만큼 그대로 되돌아간다. 크게 잡으면 잠깐 숨는 것만으로 풀린다.")]
        [Min(0.1f)]
        [SerializeField] private float _lockDecayRate = 2f;

        [Header("발사")]
        [Tooltip("한 번에 쏘는 발수.")]
        [Min(1)]
        [SerializeField] private int _salvoCount = 2;

        [Tooltip("연달아 쏠 때의 간격(초).")]
        [Min(0.05f)]
        [SerializeField] private float _salvoInterval = 0.5f;

        [Tooltip("쏜 뒤 다시 쏘기까지의 시간(초). 이 틈이 진입할 여지가 된다.")]
        [Min(0f)]
        [SerializeField] private float _reloadSeconds = 15f;

        [Header("연출")]
        [Tooltip("표적 쪽으로 도는 부분. 비워둬도 된다 — 발사에는 영향이 없다.")]
        [SerializeField] private Transform _turret;

        [SerializeField] private float _traverseSpeed = 45f;

        private Transform _target;
        private float _lockProgress;
        private float _reloadRemaining;
        private float _salvoRemaining;
        private int _salvoLeft;
        private int _nextLaunchPoint;
        private float _scanTimer;

        private readonly Collider[] _scanBuffer = new Collider[8];
        private readonly List<Missile> _inFlight = new();

        /// <summary>지금 노리고 있는 대상. 없으면 null.</summary>
        public Transform Target => _target;

        public float Range => _range;

        public bool IsOperational => isActiveAndEnabled && (_health == null || _health.IsAlive);

        /// <summary>
        /// 이 기체가 어느 발사대의 교전 범위 안에 있는지.
        /// <para>
        /// 조준이 얼마나 찼는지는 보지 않는다. 조준은 지형에 가리면 빠지고 나오면 다시
        /// 차는데, 그때마다 경고가 켜졌다 꺼지면 깜빡이는 글자가 되어 오히려 안 읽힌다.
        /// 여기 있는 동안은 언제든 조준이 시작될 수 있다는 사실 자체가 알려야 할 것이다.
        /// </para>
        /// <para>
        /// 쏘지 못하는 안쪽은 뺀다. 그 자리가 안전하다는 것도 알려줘야 하는 정보고,
        /// 파고들었을 때 경고가 꺼지는 것보다 분명한 방법은 없다.
        /// </para>
        /// </summary>
        public static bool AnyCovering(Transform target)
        {
            foreach (SamSite site in Active)
            {
                if (!site.IsOperational)
                {
                    continue;
                }

                float distance = Vector3.Distance(target.position, site.transform.position);

                if (distance <= site._range && distance >= site._minRange)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 이 기체를 아직 쫓고 있는 미사일이 있는지.
        /// <para>
        /// 발사대가 아니라 미사일에게 묻는다. 발사대는 쏜 뒤 다른 표적으로 옮겨갈 수 있고,
        /// 미사일은 스쳐 지나가면 쫓기를 그만두므로, 지금 위협인지는 미사일만 안다.
        /// </para>
        /// </summary>
        public static bool AnyIncoming(Transform target)
        {
            foreach (SamSite site in Active)
            {
                foreach (Missile missile in site._inFlight)
                {
                    if (missile != null && missile.Target == target)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_missile == null)
            {
                Debug.LogError($"{nameof(SamSite)}: Missile Definition이 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            PruneInFlight();

            if (!IsOperational)
            {
                return;
            }

            ScanForTarget();
            AimTurret();

            if (_salvoLeft > 0)
            {
                UpdateSalvo();
                return;
            }

            if (_reloadRemaining > 0f)
            {
                _reloadRemaining -= _clock.Delta;
                return;
            }

            UpdateLock();
        }

        /// <summary>
        /// 조준을 쌓거나 되돌린다.
        /// <para>
        /// 끊겼다고 0으로 버리지 않는다. 쌓인 것이 서서히 빠져야 잠깐 스치듯 가린 것과
        /// 제대로 숨은 것이 갈리고, 지형을 어떻게 타는지가 실력이 된다.
        /// </para>
        /// </summary>
        private void UpdateLock()
        {
            bool engageable = _target != null && CanEngage();

            _lockProgress += engageable
                ? _clock.Delta / _lockSeconds
                : -_clock.Delta / _lockSeconds * _lockDecayRate;

            _lockProgress = Mathf.Clamp01(_lockProgress);

            if (_lockProgress >= 1f)
            {
                BeginSalvo();
            }
        }

        private bool CanEngage()
        {
            float distance = Vector3.Distance(_target.position, transform.position);

            if (distance > _range || distance < _minRange)
            {
                return false;
            }

            return !_requireLineOfSight || HasLineOfSight();
        }

        private bool HasLineOfSight()
        {
            Vector3 from = ResolveLaunchPoint().position;
            Vector3 to = _target.position;
            Vector3 delta = to - from;

            return !Physics.Raycast(
                from, delta.normalized, delta.magnitude, _sightBlockers, QueryTriggerInteraction.Ignore);
        }

        private void BeginSalvo()
        {
            _lockProgress = 0f;
            _salvoLeft = _salvoCount;
            _salvoRemaining = 0f;
        }

        private void UpdateSalvo()
        {
            _salvoRemaining -= _clock.Delta;

            if (_salvoRemaining > 0f)
            {
                return;
            }

            Fire();
            _salvoLeft--;
            _salvoRemaining = _salvoInterval;

            if (_salvoLeft <= 0)
            {
                _reloadRemaining = _reloadSeconds;
            }
        }

        private void Fire()
        {
            if (_target == null || _missile.Prefab == null)
            {
                return;
            }

            Transform point = ResolveLaunchPoint();

            // 다음 발은 옆 발사대에서 나간다. 시야 확인도 같은 자리를 쓰므로
            // 여기서만 넘긴다 — 확인할 때마다 넘기면 한 자리에서만 쏘게 된다.
            _nextLaunchPoint++;

            var missile = Instantiate(_missile.Prefab, point.position, point.rotation)
                .GetComponent<Missile>();

            if (missile == null)
            {
                Debug.LogError($"{nameof(SamSite)}: Missile 프리팹에 Missile 컴포넌트가 없습니다.", this);
                return;
            }

            missile.Launch(_missile, gameObject, _target, point.forward * _missile.LaunchSpeed, _blastMask);
            _inFlight.Add(missile);
        }

        /// <summary>
        /// 터졌거나 사라진 미사일을 목록에서 뺀다.
        /// <para>
        /// 미사일이 터지면서 스스로를 지우므로 이벤트를 구독해 두면 이미 사라진 것을
        /// 붙잡게 된다. 매 프레임 훑어 비어 있는 칸을 버리는 편이 확실하다.
        /// </para>
        /// </summary>
        private void PruneInFlight()
        {
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                if (_inFlight[i] == null)
                {
                    _inFlight.RemoveAt(i);
                }
            }
        }

        private void ScanForTarget()
        {
            if (_target != null && _target.gameObject.activeInHierarchy
                && Vector3.Distance(_target.position, transform.position) <= _range)
            {
                return;
            }

            _scanTimer -= _clock.Delta;
            if (_scanTimer > 0f)
            {
                return;
            }

            _scanTimer = 0.25f;
            _target = null;

            int found = Physics.OverlapSphereNonAlloc(
                transform.position, _range, _scanBuffer, _targetMask, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;

            for (int i = 0; i < found; i++)
            {
                float distance = Vector3.SqrMagnitude(_scanBuffer[i].transform.position - transform.position);

                if (distance < nearest)
                {
                    nearest = distance;
                    _target = _scanBuffer[i].transform;
                }
            }
        }

        /// <summary>
        /// 표적 쪽으로 돌린다. 발사와는 무관한 연출이다.
        /// <para>
        /// 발사를 여기에 묶지 않는 이유는, 묶으면 도는 속도가 곧 발사 조건이 되어
        /// 조준 시간이라는 예고가 두 군데로 나뉘기 때문이다.
        /// </para>
        /// </summary>
        private void AimTurret()
        {
            if (_turret == null || _target == null)
            {
                return;
            }

            Vector3 flat = _target.position - _turret.position;
            flat.y = 0f;

            if (flat.sqrMagnitude < 0.001f)
            {
                return;
            }

            _turret.rotation = Quaternion.RotateTowards(
                _turret.rotation,
                Quaternion.LookRotation(flat, Vector3.up),
                _traverseSpeed * _clock.Delta);
        }

        private Transform ResolveLaunchPoint()
        {
            if (_launchPoints.Length == 0)
            {
                return transform;
            }

            Transform point = _launchPoints[_nextLaunchPoint % _launchPoints.Length];
            return point != null ? point : transform;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _range);

            // 쏘지 못하는 안쪽. 여기까지 파고드는 것이 이 표적의 답 중 하나다.
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _minRange);
        }
    }
}
