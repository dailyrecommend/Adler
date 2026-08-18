using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>오브젝트의 로컬 축 하나.</summary>
    public enum LocalAxis
    {
        Forward = 0,
        Back = 1,
        Up = 2,
        Down = 3,
        Right = 4,
        Left = 5,
    }

    /// <summary>
    /// 지상에 고정된 대공포. 사거리 안에 들어온 기체를 쫓아 예측 사격을 한다.
    /// <para>
    /// 플레이어와 같은 탄을 쓴다. 날아오는 것이 눈에 보여야 피할 수 있고, 맞았다는
    /// 사실만 통보받는 것과는 전혀 다른 경험이 된다.
    /// </para>
    /// <para>
    /// 정확히 맞히도록 만들지 않았다. 예측 지점을 일부러 어긋내고 사격을 끊어서,
    /// 피할 수 있는 여지와 다시 진입할 틈을 남긴다. 완벽한 조준은 도전이 아니라
    /// 그저 저공 비행을 금지하는 규칙이 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AntiAirGun : MonoBehaviour
    {
        [Header("무장")]
        [SerializeField] private GunDefinition _gun;

        [Tooltip("총구. 여러 개면 번갈아 쏜다. 비어 있으면 회전하는 포신에서 나간다.")]
        [SerializeField] private Transform[] _muzzles = System.Array.Empty<Transform>();

        [Tooltip("총구에서 탄이 나가는 축.\n" +
                 "빈 오브젝트를 직접 만들었다면 Forward(+Z), 본을 그대로 썼다면 대개 Up(+Y)이다.\n" +
                 "이 축은 조준 판정에도 쓰이므로 어긋나면 옆을 보고 쏘거나 아예 쏘지 않는다.")]
        [SerializeField] private LocalAxis _muzzleForwardAxis = LocalAxis.Up;

        [Tooltip("탄이 맞을 레이어. 자기 진영은 빼둘 것.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("포탑")]
        [Tooltip("좌우로 도는 부분.")]
        [SerializeField] private Transform _yawPivot;

        [Tooltip("위아래로 드는 부분. 비워두면 좌우만 돈다.")]
        [SerializeField] private Transform _pitchPivot;

        [SerializeField] private float _traverseSpeed = 70f;
        [SerializeField] private float _elevationSpeed = 50f;

        [Tooltip("포신을 들 수 있는 범위(도). 아래로는 지면을 쏘지 않게 막는다.")]
        [SerializeField] private Vector2 _elevationRange = new Vector2(-3f, 85f);

        [Header("교전")]
        [Tooltip("이 안에 들어온 기체를 노린다 (m).")]
        [Min(1f)]
        [SerializeField] private float _range = 220f;

        [Tooltip("노릴 대상의 레이어. 플레이어 기체를 넣는다.")]
        [SerializeField] private LayerMask _targetMask;

        [Tooltip("이 각도 안까지 조준이 맞아야 쏜다(도).")]
        [SerializeField] private float _aimTolerance = 6f;

        [Tooltip("사이에 지형이 끼어 있으면 쏘지 않는다.")]
        [SerializeField] private bool _requireLineOfSight = true;

        [SerializeField] private LayerMask _sightBlockers;

        [Header("사격 리듬")]
        [Tooltip("한 번에 쏘는 시간(초).")]
        [Min(0.1f)]
        [SerializeField] private float _burstSeconds = 1.2f;

        [Tooltip("쏜 뒤 쉬는 시간(초). 이 틈이 다시 진입할 여지가 된다.")]
        [Min(0f)]
        [SerializeField] private float _burstCooldown = 1.8f;

        [Tooltip("예측 지점을 어긋내는 정도. 거리에 비례해 빗나간다.\n" +
                 "0이면 이론상 완벽하게 맞혀서 피할 방법이 없어진다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _leadError = 0.12f;

        private Transform _target;
        private Rigidbody _targetBody;
        private float _scanTimer;
        private float _burstTimer;
        private bool _firing;
        private float _cooldown;
        private int _nextMuzzle;
        private Vector3 _aimOffset;

        private readonly Collider[] _scanBuffer = new Collider[8];
        private readonly RaycastHit[] _sightBuffer = new RaycastHit[8];

        /// <summary>지금 노리고 있는 대상. 없으면 null.</summary>
        public Transform Target => _target;

        private void Awake()
        {
            if (_gun == null)
            {
                Debug.LogError($"{nameof(AntiAirGun)}: Gun Definition이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_yawPivot == null)
            {
                _yawPivot = transform;
            }
        }

        private void Update()
        {
            ScanForTarget();

            if (_target == null)
            {
                _firing = false;
                return;
            }

            Vector3 aimPoint = PredictAimPoint();
            bool onTarget = AimAt(aimPoint);

            UpdateBurst(onTarget);
        }

        /// <summary>
        /// 매 프레임 훑지 않는다. 한 대뿐인 기체를 찾자고 모든 포대가 프레임마다
        /// 물리 질의를 돌릴 이유가 없다.
        /// </summary>
        private void ScanForTarget()
        {
            if (_target != null && IsStillValid())
            {
                return;
            }

            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f)
            {
                return;
            }

            _scanTimer = 0.25f;
            _target = null;
            _targetBody = null;

            int found = Physics.OverlapSphereNonAlloc(
                transform.position, _range, _scanBuffer, _targetMask, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;

            for (int i = 0; i < found; i++)
            {
                Collider candidate = _scanBuffer[i];
                float distance = Vector3.SqrMagnitude(candidate.transform.position - transform.position);

                if (distance < nearest)
                {
                    nearest = distance;
                    _target = candidate.transform;
                    _targetBody = candidate.GetComponentInParent<Rigidbody>();
                }
            }
        }

        private bool IsStillValid()
        {
            if (!_target.gameObject.activeInHierarchy)
            {
                return false;
            }

            return Vector3.Distance(_target.position, transform.position) <= _range;
        }

        /// <summary>
        /// 탄이 도착할 무렵 기체가 있을 자리를 짚는다. 도착 시간이 거리에 따라 달라지고
        /// 거리는 다시 예측 지점에 따라 달라지므로, 두 번 반복해 맞춰 간다.
        /// </summary>
        private Vector3 PredictAimPoint()
        {
            Vector3 muzzle = ResolveMuzzle().position;
            Vector3 targetPosition = _target.position;
            Vector3 targetVelocity = _targetBody != null ? _targetBody.linearVelocity : Vector3.zero;

            Vector3 predicted = targetPosition;

            for (int i = 0; i < 2; i++)
            {
                float flightTime = Vector3.Distance(muzzle, predicted) / _gun.MuzzleVelocity;
                predicted = targetPosition + (targetVelocity * flightTime);
            }

            return predicted + _aimOffset;
        }

        /// <summary>
        /// 포탑을 돌린다. 조준이 허용 각도 안에 들어왔으면 참.
        /// <para>
        /// 피벗에 특정 방향을 요구하지 않는다. 총구가 지금 어디를 보고 있는지 재서 그
        /// 오차만큼 세계 좌표로 돌리므로, 본이 어떤 축으로 누워 있든 그대로 동작한다.
        /// 본은 만든 도구마다 축 관례가 달라서, 로컬 회전을 직접 써넣으면 리그를 다시
        /// 만들 때마다 코드를 고치게 된다.
        /// </para>
        /// </summary>
        private bool AimAt(Vector3 aimPoint)
        {
            Transform muzzle = ResolveMuzzle();
            Vector3 toTarget = aimPoint - muzzle.position;

            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 desired = toTarget.normalized;
            Vector3 current = AimDirectionOf(muzzle);
            float dt = Time.deltaTime;

            TraverseTowards(current, desired, dt);
            ElevateTowards(current, desired, dt);

            // 돌린 뒤의 총구로 다시 잰다. 돌리기 전 값으로 판단하면 한 프레임 늦게 쏜다.
            float error = Vector3.Angle(AimDirectionOf(muzzle), aimPoint - muzzle.position);
            return error <= _aimTolerance && HasLineOfSight(muzzle.position, aimPoint);
        }

        /// <summary>
        /// 포탑이 도는 축. 받침대가 기울어져 있어도 그 기울기를 따라간다.
        /// <para>
        /// 좌우로 도는 부분은 자기 축을 기준으로만 돌기 때문에, 아무리 돌려도 이 축은
        /// 변하지 않는다. 세계 좌표의 위쪽을 쓰면 비탈에 놓인 포탑이 돌면서 받침대에서
        /// 어긋나 버린다.
        /// </para>
        /// </summary>
        private Vector3 TurretUp => _yawPivot.up;

        /// <summary>수평면에서의 각도 차이만큼 좌우로 돌린다.</summary>
        private void TraverseTowards(Vector3 current, Vector3 desired, float deltaTime)
        {
            Vector3 up = TurretUp;
            Vector3 flatCurrent = Vector3.ProjectOnPlane(current, up);
            Vector3 flatDesired = Vector3.ProjectOnPlane(desired, up);

            if (flatCurrent.sqrMagnitude < 0.0001f || flatDesired.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float error = Vector3.SignedAngle(flatCurrent, flatDesired, up);
            float step = Mathf.Clamp(error, -_traverseSpeed * deltaTime, _traverseSpeed * deltaTime);

            _yawPivot.Rotate(up, step, Space.World);
        }

        /// <summary>
        /// 고각 차이만큼 포신을 든다.
        /// 회전축은 좌우로 돈 뒤의 포탑 옆 방향이라, 포신이 어디를 향하든 늘 수직으로 든다.
        /// </summary>
        private void ElevateTowards(Vector3 current, Vector3 desired, float deltaTime)
        {
            if (_pitchPivot == null)
            {
                return;
            }

            Vector3 up = TurretUp;
            float currentElevation = ElevationOf(current, up);
            float desiredElevation = Mathf.Clamp(
                ElevationOf(desired, up), _elevationRange.x, _elevationRange.y);

            float error = desiredElevation - currentElevation;
            float step = Mathf.Clamp(error, -_elevationSpeed * deltaTime, _elevationSpeed * deltaTime);

            // 이 축을 기준으로 양의 회전은 총구를 내린다. 들어 올리려면 부호를 뒤집는다.
            _pitchPivot.Rotate(_yawPivot.right, -step, Space.World);
        }

        /// <summary>포탑이 놓인 면을 0으로 본 위아래 각도.</summary>
        private static float ElevationOf(Vector3 direction, Vector3 up)
        {
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(direction.normalized, up), -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 사이를 가로막는 것이 있는지 본다.
        /// <para>
        /// 자기 몸통은 세지 않는다. 포탑이 적 진영 레이어에 있고 그 레이어가 시야를
        /// 막도록 설정돼 있으면, 총구가 자기 콜라이더 안에 있는 순간 스스로에게 막혀
        /// 한 발도 쏘지 못한다. 원인을 짐작하기 어려운 종류의 침묵이다.
        /// </para>
        /// </summary>
        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            if (!_requireLineOfSight)
            {
                return true;
            }

            Vector3 travel = to - from;
            float distance = travel.magnitude;

            if (distance <= 0.0001f)
            {
                return true;
            }

            int found = Physics.RaycastNonAlloc(
                from, travel / distance, _sightBuffer, distance,
                _sightBlockers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < found; i++)
            {
                if (!_sightBuffer[i].transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateBurst(bool onTarget)
        {
            _burstTimer -= Time.deltaTime;

            if (_firing)
            {
                if (_burstTimer <= 0f)
                {
                    _firing = false;
                    _burstTimer = _burstCooldown;
                    return;
                }

                if (onTarget)
                {
                    FireWhileReady();
                }

                return;
            }

            if (_burstTimer > 0f || !onTarget)
            {
                return;
            }

            _firing = true;
            _burstTimer = _burstSeconds;

            // 사격을 시작할 때 한 번만 어긋냄을 정한다. 매 발 새로 뽑으면 탄이 사방으로
            // 흩어져 그냥 부정확해 보이고, 이렇게 두면 한 줄기가 빗나가는 것으로 읽힌다.
            _aimOffset = Random.onUnitSphere * (_range * _leadError * Random.value);
        }

        private void FireWhileReady()
        {
            _cooldown -= Time.deltaTime;

            const int MaxShotsPerFrame = 3;
            int shots = 0;

            while (_cooldown <= 0f && shots < MaxShotsPerFrame)
            {
                Transform muzzle = ResolveMuzzle();
                Vector3 direction = ProjectileLauncher.ApplySpread(
                    AimDirectionOf(muzzle), _gun.SpreadDegrees);

                ProjectileLauncher.Fire(
                    _gun, muzzle.position, direction, Vector3.zero, gameObject, _hitMask);

                _cooldown += _gun.ShotInterval;
                shots++;
            }

            if (_cooldown < 0f)
            {
                _cooldown = 0f;
            }
        }

        /// <summary>
        /// 이 총구에서 탄이 나가는 방향.
        /// <para>
        /// 본을 그대로 총구로 쓰면 축이 Unity 관례와 다르다. 만든 도구마다 관례가
        /// 달라서 코드에 박아두면 리그를 손볼 때마다 함께 고쳐야 한다.
        /// </para>
        /// </summary>
        private Vector3 AimDirectionOf(Transform muzzle)
        {
            return _muzzleForwardAxis switch
            {
                LocalAxis.Forward => muzzle.forward,
                LocalAxis.Back => -muzzle.forward,
                LocalAxis.Up => muzzle.up,
                LocalAxis.Down => -muzzle.up,
                LocalAxis.Right => muzzle.right,
                LocalAxis.Left => -muzzle.right,
                _ => muzzle.forward,
            };
        }

        private Transform ResolveMuzzle()
        {
            if (_muzzles.Length == 0)
            {
                return _pitchPivot != null ? _pitchPivot : _yawPivot;
            }

            Transform muzzle = _muzzles[_nextMuzzle];
            _nextMuzzle = (_nextMuzzle + 1) % _muzzles.Length;
            return muzzle != null ? muzzle : _yawPivot;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _range);

            if (_target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _target.position);
            }
        }
    }
}
