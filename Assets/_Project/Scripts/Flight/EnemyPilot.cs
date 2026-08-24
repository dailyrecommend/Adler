using Adler.Abilities;
using Adler.Aircraft;
using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// Flies an enemy aircraft with the same flight model the player uses.
    /// <para>
    /// It only fills a <see cref="FlightInput"/> — no direct transform writes, no
    /// turn rates the player cannot reach. Whatever the enemy pulls off, the player
    /// can pull off too, and tuning the handling moves both at once.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    // ITargetSource를 입는 이유는 기총 때문이다. 기총이 조종사 클래스를 직접 알면
    // 장비가 기체 층을 올려다보게 되므로, "표적을 아는 자"라는 낮은 어휘로만 보이게 한다.
    public sealed class EnemyPilot : MonoBehaviour, ITargetSource
    {
        private enum Move
        {
            /// <summary>표적을 향해 돈다.</summary>
            Pursue,

            /// <summary>짧고 강하게 뿌리친다.</summary>
            Evade,

            /// <summary>거의 직선으로 난다. 쫓는 쪽의 사격 기회다.</summary>
            Steady,
        }

        /// <summary>
        /// 뿌리치는 방식.
        /// <para>
        /// 난이도는 얼마나 자주 피하느냐가 아니라 몇 가지로 피하느냐에서 온다. 같은
        /// 동작을 자주 하면 성가시기만 하고, 다른 동작을 가끔 하면 읽는 재미가 생긴다.
        /// </para>
        /// </summary>
        private enum Escape
        {
            BreakLeft,
            BreakRight,
            ClimbOver,
            DiveAway,
        }

        [Header("Airframe")]
        [Tooltip("이 기체의 소재 성능. 플레이어와 같은 것을 써도 된다.")]
        [SerializeField] private AirframeDefinition _airframe;

        [Header("Target")]
        [Tooltip("쫓을 대상. 비워두면 아래 레이어에서 찾는다.")]
        [SerializeField] private Transform _target;

        [Tooltip("표적을 찾을 레이어. 플레이어 기체를 넣는다.")]
        [SerializeField] private LayerMask _targetMask;

        [Min(1f)]
        [SerializeField] private float _searchRange = 600f;

        [Header("Steering")]
        [Tooltip("표적이 위아래로 벗어난 만큼 기수를 당기는 정도.\n" +
                 "3이면 19°만 벗어나도 조종간이 끝까지 간다 — 낮출수록 부드럽게 겨눈다.")]
        [Min(0.1f)]
        [SerializeField] private float _pitchGain = 1.5f;

        [Tooltip("좌우로 얼마나 벗어나야 최대로 기우는가.\n" +
                 "2면 정면에서 30°쯤 벗어났을 때 끝까지 기운다.")]
        [Min(0.1f)]
        [SerializeField] private float _bankGain = 2f;

        [Tooltip("가장 크게 기울 수 있는 각도.\n\n" +
                 "회피 기동의 급함을 정하는 상한선이다. 크게 열어두면 홱홱 돌아 쫓기가\n" +
                 "고돼지고, 좁히면 크고 느긋한 호를 그려 어디로 갈지 미리 보인다.\n" +
                 "잘 피하는 적보다 읽히는 적이 쫓는 재미를 만든다.")]
        [Range(10f, 89f)]
        [SerializeField] private float _maxBank = 50f;

        [Tooltip("기울기가 이만큼 어긋나면 조종간을 끝까지 민다 (도).\n" +
                 "작을수록 원하는 자세로 급하게 맞춘다.")]
        [Range(5f, 90f)]
        [SerializeField] private float _bankTolerance = 30f;

        [Tooltip("기울어져 선회하는 동안 함께 당기는 정도.\n" +
                 "기울이기만 하고 당기지 않으면 미끄러지듯 도는 모습이 되어 어색하다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _turnPull = 0.5f;

        [Tooltip("이미 돌고 있는 만큼 조종간을 되돌리는 정도.\n\n" +
                 "이것이 없으면 목표를 지나칠 때마다 반대로 끝까지 꺾어 좌우로 흔들린다.\n" +
                 "얼마나 벗어났는지만 보고 얼마나 빨리 돌고 있는지는 보지 않기 때문이다.\n" +
                 "0.25면 대개 잦아들고, 높이면 굼떠진다.")]
        [Min(0f)]
        [SerializeField] private float _damping = 0.4f;

        [Tooltip("가려는 방향이 바뀌는 것을 따라가는 빠르기.\n\n" +
                 "표적의 속도로 앞을 내다보는데, 상대가 선회 중이면 그 속도가 매 프레임\n" +
                 "휘둘려 조준점이 튄다. 그것을 하나하나 쫓으면 잘게 흔들린다.\n" +
                 "낮출수록 한 번 정한 선을 밀고 나가고, 높이면 즉각 반응한다.")]
        [Min(0.5f)]
        [SerializeField] private float _aimSmoothing = 4f;

        [Tooltip("이 각도 안이면 겨눈 것으로 보고 조종간을 놓는다 (도).\n" +
                 "0이면 아무리 정확히 겨눠도 미세하게 계속 움직인다.")]
        [Range(0f, 15f)]
        [SerializeField] private float _deadzone = 2f;

        [Tooltip("이 거리보다 멀면 부스터로 따라붙는다 (m). 0이면 쓰지 않는다.")]
        [Min(0f)]
        [SerializeField] private float _boostBeyond = 200f;

        [Header("Engagement")]
        [Tooltip("이보다 가까워지면 지나쳐 나간다 (m).\n" +
                 "끝까지 붙으면 부딪히거나 눈앞에서 맴돌기만 해서 쫓고 쫓기는 모습이 안 나온다.")]
        [Min(1f)]
        [SerializeField] private float _breakOffRange = 40f;

        [Header("Evade")]
        [Tooltip("뿌리치는 기동을 유지하는 시간(초).\n\n" +
                 "짧고 강해야 한다. 길게 끌면 쫓는 쪽은 그동안 계속 따라 돌아야 해서\n" +
                 "화면이 내내 흔들리고, 무엇을 노릴지 정할 틈이 없다.")]
        [Min(0.1f)]
        [SerializeField] private float _evadeSeconds = 1.2f;

        [Tooltip("뿌리친 뒤 반드시 거의 직선으로 나는 시간(초).\n\n" +
                 "이 구간이 쫓는 쪽의 사격 기회다. 없으면 회피가 끝나자마자 다시 회피로\n" +
                 "들어가 영원히 꺾기만 하고, 겨눌 틈이 나지 않는다.")]
        [Min(0.1f)]
        [SerializeField] private float _steadySeconds = 3f;

        [Tooltip("연달아 뿌리칠 수 있는 횟수.\n\n" +
                 "이만큼 쓰고 나면 안정 구간이 두 배로 길어진다. 뒤를 잡힌 채로 계속\n" +
                 "피할 수 있으면 쫓는 쪽이 영영 기회를 못 잡는다.")]
        [Min(1)]
        [SerializeField] private int _maxConsecutiveEvades = 2;

        [Tooltip("뿌리칠 때 위아래로 움직이는 정도.\n\n" +
                 "낮게 두는 것이 중요하다. 쫓는 쪽은 표적을 화면 가운데 두려고 기수를\n" +
                 "따라 올리고 내리는데, 그 위아래 움직임이 3D 멀미를 가장 크게 일으킨다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _evadeDive = 0.3f;

        [Tooltip("뒤쪽 이 각도 안에 표적이 있으면 위협으로 본다 (도).")]
        [Range(0f, 180f)]
        [SerializeField] private float _threatAngle = 70f;

        [Tooltip("이 거리 안에서만 위협으로 본다 (m).\n" +
                 "멀리서 뒤에 있는 것까지 위협으로 치면 계속 뿌리치기만 하고 덤비지 않는다.")]
        [Min(1f)]
        [SerializeField] private float _threatRange = 200f;

        [Header("Lead")]
        [Tooltip("표적이 갈 곳을 내다보는 정도. 1이면 정확히 겨눠서 떼어낼 수 없다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lead = 0.6f;

        [Header("Ground")]
        [Tooltip("이보다 낮아지면 기수를 든다 (m). 0이면 지면을 신경 쓰지 않는다.\n" +
                 "없으면 저공으로 유인하는 것만으로 이길 수 있다.")]
        [Min(0f)]
        [SerializeField] private float _minAltitude = 30f;

        [SerializeField] private LayerMask _groundMask;

        private Clock _clock;
        private Rigidbody _body;
        private Rigidbody _targetBody;
        private ArcadeFlightModel _model;
        private Move _move;
        private float _stateRemaining;
        private int _consecutiveEvades;
        private Escape _escape;
        private Vector3 _steerDirection;
        private float _clearance = Mathf.Infinity;
        private float _scanTimer;

        private readonly Collider[] _scanBuffer = new Collider[8];

        public AircraftStatSheet Stats { get; private set; }

        public Transform Target => _target;

        bool ITargetSource.HasTarget => _target != null;

        Vector3 ITargetSource.TargetPoint => _target != null ? _target.position : transform.position;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _body = GetComponent<Rigidbody>();

            if (_airframe == null)
            {
                Debug.LogError($"{nameof(EnemyPilot)}: Airframe이 비어 있어 날 수 없습니다.", this);
                enabled = false;
                return;
            }

            if (_minAltitude > 0f && _groundMask.value == 0)
            {
                Debug.LogError(
                    $"{nameof(EnemyPilot)}: Ground Mask가 비어 있어 지면을 감지하지 못합니다. " +
                    "지형 레이어를 넣거나 Min Altitude를 0으로 두세요.", this);
            }

            Stats = new AircraftStatSheet(_airframe);
            _model = new ArcadeFlightModel(Stats);
            _model.Initialize(_body);
        }

        private void FixedUpdate()
        {
            ScanForTarget();

            FlightInput input = Decide(_clock.FixedDelta);
            _model.Tick(in input, _clock);
        }

        private FlightInput Decide(float deltaTime)
        {
            // Nothing to chase: hold level so it keeps flying instead of drifting.
            if (_target == null)
            {
                return SteerTowards(LevelOff(transform.forward), float.MaxValue);
            }

            MeasureClearance();

            float distance = Vector3.Distance(_target.position, transform.position);
            UpdateMove(distance, deltaTime);

            Vector3 desired = _move switch
            {
                // 안정 구간에서는 거의 직선으로 난다. 이 몇 초가 쫓는 쪽이 겨누고
                // 쏘는 시간이고, 여기서 또 꺾으면 기회가 영영 오지 않는다.
                Move.Steady => LevelOff(transform.forward),
                Move.Evade => EvadeDirection(),
                _ => AimPoint() - transform.position,
            };

            return SteerTowards(AvoidGround(Smooth(desired, deltaTime)), distance);
        }

        /// <summary>
        /// Pursue until close, then run out for a set time before coming back.
        /// <para>
        /// 시간으로 끊는다. 거리로만 판단하면 이탈하자마자 조건이 풀려 곧바로 재돌입하고,
        /// 코앞에서 붙었다 떨어지기를 반복해 무슨 일이 벌어지는지 알아볼 수 없다.
        /// </para>
        /// </summary>
        private void UpdateMove(float distance, float deltaTime)
        {
            _stateRemaining -= deltaTime;

            switch (_move)
            {
                case Move.Evade:
                    if (_stateRemaining <= 0f)
                    {
                        // 뿌리친 뒤에는 반드시 쉰다. 연달아 쓸수록 더 오래 쉰다 —
                        // 그러지 않으면 뒤를 잡힌 채로 끝없이 피할 수 있다.
                        _move = Move.Steady;
                        _stateRemaining = _consecutiveEvades >= _maxConsecutiveEvades
                            ? _steadySeconds * 2f
                            : _steadySeconds;

                        if (_consecutiveEvades >= _maxConsecutiveEvades)
                        {
                            _consecutiveEvades = 0;
                        }
                    }

                    return;

                case Move.Steady:
                    if (_stateRemaining <= 0f)
                    {
                        _move = Move.Pursue;
                    }

                    return;
            }

            // Two reasons to break: we just merged, or someone is on our tail.
            if (distance <= _breakOffRange || IsThreatened(distance))
            {
                BeginEvade();
            }
        }

        /// <summary>
        /// True when the target sits behind us and close enough to matter.
        /// <para>
        /// 이것이 없으면 뒤를 잡혀도 계속 표적을 향해 돌려고만 한다. 쫓기는 중이라는
        /// 사실을 모르니 뿌리치는 그림이 나올 수가 없다.
        /// </para>
        /// </summary>
        private bool IsThreatened(float distance)
        {
            if (distance > _threatRange)
            {
                return false;
            }

            Vector3 toTarget = (_target.position - transform.position).normalized;

            return Vector3.Angle(-transform.forward, toTarget) <= _threatAngle;
        }

        /// <summary>
        /// 뿌리치기를 시작한다. 방금 쓴 것과 다른 방식을 고른다.
        /// <para>
        /// 같은 동작을 반복하면 자주 피해도 성가시기만 하다. 몇 가지 중에서 골라야
        /// 무엇을 하려는지 보이면서도 매번 다르게 읽힌다.
        /// </para>
        /// </summary>
        private void BeginEvade()
        {
            _move = Move.Evade;
            _stateRemaining = _evadeSeconds;
            _consecutiveEvades++;

            Escape next = _escape;

            for (int i = 0; i < 4 && next == _escape; i++)
            {
                next = (Escape)Random.Range(0, 4);
            }

            _escape = next;
        }

        /// <summary>
        /// A hard turn across the attacker's line, carrying downhill.
        /// <para>
        /// 등지고 직선으로 달아나지 않는다. 속도가 비슷하면 앞뒤로 나란히 날게 되어
        /// 서로의 화면에서 아무것도 움직이지 않고, 도망치는 것이 도망으로 보이지 않는다.
        /// </para>
        /// <para>
        /// 가로질러 꺾으면 쫓는 쪽이 따라 돌아야 하고, 아래로 떨어지면 속도가 붙는다.
        /// 둘 다 화면에서 크게 움직이는 일이라 무슨 일이 벌어지는지 읽힌다.
        /// </para>
        /// </summary>
        private Vector3 EvadeDirection()
        {
            Vector3 toTarget = (_target.position - transform.position).normalized;

            Vector3 across = Vector3.Cross(toTarget, Vector3.up);

            if (across.sqrMagnitude < 0.001f)
            {
                across = transform.right;
            }

            across.Normalize();

            // 고도가 없으면 내려가는 방식을 쓰지 않는다. 언제나 강하하면 뿌리칠 때마다
            // 고도를 잃고, 되찾는 곳이 없으니 결국 바닥까지 걸어 내려간다.
            Escape escape = _escape == Escape.DiveAway && !HasHeightToSpend
                ? Escape.ClimbOver
                : _escape;

            return escape switch
            {
                Escape.BreakLeft => -across + (Vector3.up * -_evadeDive),
                Escape.BreakRight => across + (Vector3.up * -_evadeDive),
                Escape.ClimbOver => (transform.forward * 0.4f) + Vector3.up,
                Escape.DiveAway => (transform.forward * 0.4f) + Vector3.down,
                _ => across,
            };
        }

        /// <summary>지면까지 얼마나 남았는지 재둔다. 회피와 지면 필터가 함께 쓴다.</summary>
        private void MeasureClearance()
        {
            _clearance = Mathf.Infinity;

            if (_minAltitude <= 0f)
            {
                return;
            }

            if (Physics.Raycast(
                    transform.position, Vector3.down, out RaycastHit hit,
                    _minAltitude * 3f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                _clearance = hit.distance;
            }
        }

        /// <summary>강하로 쓸 고도가 남았는지. 여유가 배는 있어야 내려갈 만하다.</summary>
        private bool HasHeightToSpend => _clearance > _minAltitude * 2f;

        /// <summary>
        /// Where the target will be, but deliberately not exactly.
        /// <para>
        /// 현재 위치를 쫓으면 영원히 꼬리만 문다. 반대로 정확히 내다보면 어떤 기동으로도
        /// 떼어낼 수 없다. 얼마나 내다볼지가 곧 난이도다.
        /// </para>
        /// </summary>
        private Vector3 AimPoint()
        {
            if (_lead <= 0f || _targetBody == null)
            {
                return _target.position;
            }

            float speed = Mathf.Max(_model.Speed, 1f);
            float flightTime = Vector3.Distance(_target.position, transform.position) / speed;

            return _target.position + (_targetBody.linearVelocity * flightTime * _lead);
        }

        /// <summary>
        /// Eases the steering target instead of snapping to it every step.
        /// <para>
        /// 사람이 조종할 때도 표적이 조금 움직일 때마다 조종간을 다시 잡지 않는다.
        /// 한 번 정한 선을 잠시 밀고 나가다 크게 어긋났을 때 고쳐 잡는데, 그 뜸이
        /// 없으면 기체가 잘게 흔들린다.
        /// </para>
        /// </summary>
        private Vector3 Smooth(Vector3 desired, float deltaTime)
        {
            if (desired.sqrMagnitude < 0.0001f)
            {
                return _steerDirection;
            }

            Vector3 target = desired.normalized;

            if (_steerDirection.sqrMagnitude < 0.0001f)
            {
                _steerDirection = target;
                return _steerDirection;
            }

            _steerDirection = Vector3.Slerp(
                _steerDirection, target, 1f - Mathf.Exp(-_aimSmoothing * deltaTime));

            return _steerDirection;
        }

        /// <summary>
        /// Pulls the desired direction up when the ground is close.
        /// <para>
        /// 마지막에 걸리는 필터다. 쫓는 것보다 살아남는 것이 먼저라, 어떤 판단을 내렸든
        /// 여기를 거쳐 나간다.
        /// </para>
        /// </summary>
        private Vector3 AvoidGround(Vector3 desired)
        {
            if (_minAltitude <= 0f)
            {
                return desired;
            }

            float urgency = 1f - Mathf.Clamp01(_clearance / _minAltitude);

            return urgency <= 0f
                ? desired
                : Vector3.Slerp(desired.normalized, Vector3.up, urgency);
        }

        /// <summary>Keeps the nose from drifting up or down when there is no target.</summary>
        private static Vector3 LevelOff(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward;
        }

        /// <summary>
        /// Turns a direction into stick input.
        /// <para>
        /// 기체 기준으로 표적이 위에 있으면 당기고 오른쪽에 있으면 오른쪽으로 기운다.
        /// 기운 만큼 저절로 도는 보정이 나머지를 맡으므로 이 둘이면 어디로든 갈 수 있다.
        /// </para>
        /// </summary>
        private FlightInput SteerTowards(Vector3 desired, float distance)
        {
            if (desired.sqrMagnitude < 0.0001f)
            {
                return FlightInput.None;
            }

            bool boost = _boostBeyond > 0f && _move == Move.Pursue && distance > _boostBeyond;
            Vector3 direction = desired.normalized;
            Vector3 spin = transform.InverseTransformDirection(_body.angularVelocity);

            // Close enough. Still roll level rather than freezing at whatever bank we
            // happen to hold — a wing left down keeps swinging the nose off target.
            if (Vector3.Angle(transform.forward, direction) <= _deadzone)
            {
                return new FlightInput
                {
                    Pitch = Mathf.Clamp(spin.x * _damping, -1f, 1f),
                    Roll = RollToward(0f, spin),
                    Boost = boost,
                };
            }

            Vector3 local = transform.InverseTransformDirection(direction);

            // Behind us the lateral parts shrink, so the aircraft turns slowest exactly
            // when it needs to turn most. Commit to a full deflection instead.
            if (local.z < 0f)
            {
                float lateral = new Vector2(local.x, local.y).magnitude;

                if (lateral < 0.001f)
                {
                    local.x = 1f;
                    local.y = 0f;
                }
                else
                {
                    local.x /= lateral;
                    local.y /= lateral;
                }
            }

            // Pick a bank angle to hold, the way a pilot sets one and rides it through
            // the turn. Steering roll straight off the error instead means nudging the
            // wings a little every step, which never settles and reads as flailing.
            float targetBank = Mathf.Clamp(local.x * _bankGain, -1f, 1f) * _maxBank;

            // Pull while banked. Banking alone turns the aircraft here, but a turn with
            // no pull looks like a skid rather than a committed break.
            float pull = _turnPull * Mathf.Abs(targetBank) / _maxBank;
            float pitch = (local.y * _pitchGain) + pull + (spin.x * _damping);

            return new FlightInput
            {
                Pitch = Mathf.Clamp(pitch, -1f, 1f),
                Roll = RollToward(targetBank, spin),
                Boost = boost,
            };
        }

        /// <summary>
        /// Rolls toward a bank angle instead of toward the target.
        /// <para>
        /// 기울기를 목표로 삼는 것이 요점이다. 좌우 오차만 보고 롤을 넣으면 겨누는 순간
        /// 입력이 0이 되는데, 기체는 여전히 기울어 있고 그 기울기가 계속 기수를 돌린다.
        /// 그래서 또 어긋나고 또 고치기를 반복한다 — 어지러워 보이는 진짜 이유다.
        /// </para>
        /// <para>
        /// 자세를 목표로 두면 다 돌았을 때 저절로 수평으로 돌아온다.
        /// </para>
        /// </summary>
        private float RollToward(float targetBank, Vector3 spin)
        {
            // Bank angle now: how far the right wing sits below level.
            float bank = Mathf.Asin(Mathf.Clamp(-Vector3.Dot(transform.right, Vector3.up), -1f, 1f))
                         * Mathf.Rad2Deg;

            float roll = (targetBank - bank) / _bankTolerance;

            return Mathf.Clamp(roll + (spin.z * _damping), -1f, 1f);
        }

        /// <summary>Scans on an interval — one player is not worth a query per step.</summary>
        private void ScanForTarget()
        {
            if (_target != null && _target.gameObject.activeInHierarchy
                && Vector3.Distance(_target.position, transform.position) <= _searchRange)
            {
                return;
            }

            _scanTimer -= _clock.FixedDelta;
            if (_scanTimer > 0f)
            {
                return;
            }

            _scanTimer = 0.25f;
            _target = null;
            _targetBody = null;

            int found = Physics.OverlapSphereNonAlloc(
                transform.position, _searchRange, _scanBuffer, _targetMask, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;

            for (int i = 0; i < found; i++)
            {
                float distance = Vector3.SqrMagnitude(_scanBuffer[i].transform.position - transform.position);

                if (distance < nearest)
                {
                    nearest = distance;
                    _target = _scanBuffer[i].transform;
                    _targetBody = _scanBuffer[i].GetComponentInParent<Rigidbody>();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _searchRange);

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _breakOffRange);
        }
    }
}
