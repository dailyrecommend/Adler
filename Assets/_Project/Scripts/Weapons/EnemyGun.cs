using System;
using Adler.Core;
using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 적기의 기총. 조종사가 기수를 표적에 얹어야 쏠 수 있다.
    /// <para>
    /// 포탑처럼 따로 돌지 않는다. 전투기의 총은 기수에 고정돼 있고, 그 제약이 곧 이
    /// 무기가 읽히는 이유다 — 적이 나를 향해 기수를 돌리는 것이 보이므로, 맞기 전에
    /// 위험해지고 있다는 것을 알 수 있다. 아무 자세에서나 쏠 수 있으면 피격이 통보가
    /// 된다.
    /// </para>
    /// <para>
    /// 겨눔이 맞아도 곧바로 쏘지 않는다. 짧게 뜸을 들이는 동안 경고가 뜨고, 그 틈이
    /// 빠져나갈 여지가 된다. 죽었을 때 "못 봤다"가 아니라 "봤는데 못 피했다"가 되어야
    /// 다음 번에 무엇을 다르게 할지 알 수 있다.
    /// </para>
    /// <para>
    /// 정확히 맞히도록 만들지 않았다. 예측 지점을 거리에 비례해 어긋내고 사격을
    /// 끊는다. 완벽한 조준은 도전이 아니라 그저 앞에 나서지 말라는 규칙이 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyGun : MonoBehaviour
    {

        [Header("무장")]
        [SerializeField] private GunDefinition _gun;

        [Tooltip("총구. 여러 개면 번갈아 쏜다. 비어 있으면 이 오브젝트에서 나간다.")]
        [SerializeField] private Transform[] _muzzles = System.Array.Empty<Transform>();

        [Tooltip("탄이 맞을 레이어. 자기 진영은 빼둘 것.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("참조")]
        [Tooltip("기수 방향. 비워두면 이 기체의 정면을 쓴다.")]
        [SerializeField] private Transform _boresight;

        [Header("교전")]
        [Tooltip("이 거리 안에서만 쏜다 (m).\n" +
                 "기총 사거리보다 짧게 둘 것. 사거리 끝에서 쏘면 탄이 도착할 무렵에는\n" +
                 "표적이 벌써 다른 곳에 있어서, 맞지도 않으면서 화면만 어지럽힌다.")]
        [Min(1f)]
        [SerializeField] private float _range = 180f;

        [Tooltip("기수가 이 각도 안까지 맞아야 쏜다.\n" +
                 "넓히면 스치듯 지나가면서도 쏘게 되어, 언제 위험한지 읽을 수 없어진다.")]
        [Range(1f, 30f)]
        [SerializeField] private float _aimTolerance = 8f;

        [Tooltip("사이에 지형이 끼어 있으면 쏘지 않는다.")]
        [SerializeField] private bool _requireLineOfSight = true;

        [SerializeField] private LayerMask _sightBlockers;

        [Header("사격 리듬")]
        [Tooltip("겨눔이 맞은 뒤 쏘기까지 뜸을 들이는 시간(초).\n\n" +
                 "이 동안 경고가 뜬다. 0으로 두면 조준선에 들어서는 순간 맞기 시작해서,\n" +
                 "피할 수 있었다는 감각 없이 체력만 줄어든다.")]
        [Min(0f)]
        [SerializeField] private float _warnSeconds = 0.45f;

        [Tooltip("겨눔이 빗나간 동안 쌓아둔 뜸이 다 풀리기까지의 시간(초).\n\n" +
                 "0으로 두면 한 프레임만 벗어나도 처음부터 다시 쌓는다. 선회전 중에는\n" +
                 "각도가 허용치 언저리에서 계속 떨리므로, 그러면 뜸이 영영 차지 않아\n" +
                 "적기가 어쩌다 한 번씩만 쏘게 된다.\n\n" +
                 "뜸 시간과 비슷하게 두면 잠깐의 흔들림은 넘어가고 확실히 떨쳐낸 것만\n" +
                 "처음으로 돌린다.")]
        [Min(0f)]
        [SerializeField] private float _warnForgetSeconds = 0.5f;

        [Tooltip("한 번에 쏘는 시간(초).")]
        [Min(0.1f)]
        [SerializeField] private float _burstSeconds = 0.9f;

        [Tooltip("쏜 뒤 쉬는 시간(초). 이 틈이 자세를 고쳐 잡을 여지가 된다.")]
        [Min(0f)]
        [SerializeField] private float _burstCooldown = 1.6f;

        [Tooltip("예측 지점을 어긋내는 정도. 표적까지의 실제 거리에 곱해진다.\n" +
                 "0.06이면 100m 밖 표적을 최대 6m 빗나간다 — 가까울수록 정확해진다.\n" +
                 "0이면 이론상 완벽하게 맞혀서 피할 방법이 없어진다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _leadError = 0.06f;

        private Rigidbody _body;
        private Transform _target;
        private Rigidbody _targetBody;
        private float _onTargetFor;
        private int _nextMuzzle;
        private Vector3 _aimOffset;

        // 점사 리듬과 발사 간격은 대공포와 같은 부품을 쓴다. 시작 조건(뜸)만 이쪽 사정이다.
        private readonly ShotCadence _cadence = new();
        private BurstCycle _burst;
        private Action _fireOne;

        private readonly RaycastHit[] _sightBuffer = new RaycastHit[8];

        /// <summary>겨눔이 맞아 뜸을 들이는 중. 아직 쏘지는 않았다.</summary>
        public bool IsAiming => _target != null && !IsFiring && _onTargetFor > 0f;

        /// <summary>지금 쏘고 있는지.</summary>
        public bool IsFiring => _burst.IsFiring;

        /// <summary>
        /// 이 기체를 겨누고 있는 적기가 있는지. 화면 경고가 물어본다.
        /// <para>
        /// 쏘는 중까지 포함한다. 뜸을 들이는 동안에만 켜면 첫 발이 나가는 순간 경고가
        /// 꺼지는데, 정작 그때부터가 맞고 있는 시간이다.
        /// </para>
        /// <para>
        /// 아래로 훑어 견준다. 조종사가 잡아둔 것은 콜라이더의 트랜스폼이라 기체의
        /// 뿌리가 아닌 경우가 많은데, 같은지만 보면 자식 콜라이더를 겨눈 적기가
        /// 아무도 겨누지 않는 것으로 셈해진다.
        /// </para>
        /// </summary>
        public static bool AnyAimingAt(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            foreach (EnemyGun gun in Registry<EnemyGun>.All)
            {
                if (gun._target != null
                    && gun._target.IsChildOf(target)
                    && (gun.IsAiming || gun.IsFiring))
                {
                    return true;
                }
            }

            return false;
        }

        private Clock _clock;
        private ITargetSource _targets;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            // 조종사 클래스가 아니라 "표적을 아는 자"로 찾는다. 이름으로 찾으면 장비가
            // 기체 층을 올려다보게 되고, 조종사를 갈아치울 때 여기도 함께 고쳐야 한다.
            _targets = GetComponentInParent<ITargetSource>();

            if (_boresight == null)
            {
                _boresight = _targets is Component pilot ? pilot.transform : transform;
            }

            if (_gun == null || _targets == null)
            {
                Debug.LogError($"{nameof(EnemyGun)}: Gun Definition 또는 표적을 아는 것이 없습니다.", this);
                enabled = false;
                return;
            }

            _body = _boresight.GetComponentInParent<Rigidbody>();

            _burst = new BurstCycle(_burstSeconds, _burstCooldown);
            _fireOne = FireSingle;
        }

        private void OnEnable() => Registry<EnemyGun>.Add(this);

        private void OnDisable()
        {
            Registry<EnemyGun>.Remove(this);
            Reset();
        }

        private void Update()
        {
            // 표적이 바뀔 때만 리지드바디를 찾는다. 쏘는 동안 매 발 뒤져 올라가면
            // 초당 스물다섯 번씩 계층을 훑게 된다.
            Transform target = _targets.Target;

            if (!ReferenceEquals(target, _target))
            {
                _target = target;
                _targetBody = target != null ? target.GetComponentInParent<Rigidbody>() : null;
                _onTargetFor = 0f;
            }

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                Reset();
                return;
            }

            UpdateFiring(IsOnTarget());
        }

        /// <summary>
        /// 기수가 쏴야 할 곳을 향하고 있고, 사거리 안이고, 사이가 트여 있는지.
        /// <para>
        /// 지금 있는 자리가 아니라 <b>탄이 도착할 자리</b>를 기준으로 잰다. 쏘는 것은
        /// 그쪽인데 판정만 표적 자체로 하면 둘이 어긋나서, 가로지르는 표적을 앞에 두고도
        /// 못 쏘거나 반대로 기수와 전혀 다른 방향으로 탄을 뱉게 된다.
        /// </para>
        /// </summary>
        private bool IsOnTarget()
        {
            Vector3 muzzle = ResolveMuzzle().position;
            float distance = Vector3.Distance(_target.position, muzzle);

            if (distance > _range || distance < 0.0001f)
            {
                return false;
            }

            Vector3 toAim = LeadPoint(muzzle) - muzzle;

            if (Vector3.Angle(_boresight.forward, toAim) > _aimTolerance)
            {
                return false;
            }

            return HasLineOfSight(muzzle, toAim, toAim.magnitude);
        }

        /// <summary>
        /// 겨눔 → 뜸 → 사격 → 휴식을 오간다.
        /// <para>
        /// 뜸은 겨눔이 빗나가면 줄어들 뿐 처음으로 돌아가지는 않는다. 선회전 중에는
        /// 각도가 허용치 언저리에서 끊임없이 떨리므로, 벗어나는 순간 지워버리면 뜸이
        /// 영영 차지 않아 적기가 어쩌다 한 번씩만 쏘게 된다. 잠깐의 흔들림은 넘어가고
        /// 확실히 떨쳐낸 것만 되돌리는 것이 맞다.
        /// </para>
        /// <para>
        /// 뜸과 휴식은 함께 흐른다. 쉬는 동안 겨눠 오는 것을 막을 이유가 없고, 차례로
        /// 두면 둘을 더한 만큼 조용해져서 쫓기는 느낌이 사라진다.
        /// </para>
        /// </summary>
        private void UpdateFiring(bool onTarget)
        {
            float deltaTime = _clock.Delta;

            if (_burst.IsFiring)
            {
                if (!_burst.Tick(deltaTime))
                {
                    // 다 쐈으면 뜸도 처음으로. 방금 얻어맞은 참이라 겨눔이 이어지고
                    // 있다는 이유로 곧바로 다음 점사가 오면 쉬는 시간이 뜻을 잃는다.
                    _onTargetFor = 0f;
                    return;
                }

                // 쏘는 도중 겨눔이 빗나가도 탄은 계속 나간다. 사격을 시작한 뒤에는
                // 총이 기수를 따라가는 것이 맞고, 그 빗나간 탄줄이 피했다는 신호가 된다.
                if (onTarget || _cadence.CoolingDown)
                {
                    _cadence.Run(deltaTime, _gun.ShotInterval, _fireOne);
                }

                return;
            }

            _burst.Tick(deltaTime);
            _onTargetFor = Mathf.Clamp(_onTargetFor + AimStep(onTarget, deltaTime), 0f, _warnSeconds);

            if (_onTargetFor < _warnSeconds || !_burst.RestDone)
            {
                return;
            }

            _burst.Begin();
            _aimOffset = Ballistics.BurstScatter(transform.position, _target.position, _leadError);
        }

        /// <summary>이번 프레임에 뜸이 차거나 풀릴 몫.</summary>
        private float AimStep(bool onTarget, float deltaTime)
        {
            if (onTarget)
            {
                return deltaTime;
            }

            if (_warnForgetSeconds <= 0f)
            {
                return -_warnSeconds;
            }

            return -deltaTime * (_warnSeconds / _warnForgetSeconds);
        }

        private void Reset()
        {
            _burst.Interrupt();
            _cadence.Reset();
            _onTargetFor = 0f;
            _target = null;
            _targetBody = null;
        }

        /// <summary>
        /// 탄이 도착할 무렵 표적이 있을 자리를 짚는다. 도착 시간이 거리에 따라 달라지고
        /// 거리는 다시 예측 지점에 따라 달라지므로, 두 번 반복해 맞춰 간다.
        /// <para>
        /// 어긋냄은 여기에 얹지 않는다. 겨눔 판정과 사격이 같은 자리를 봐야 하는데,
        /// 어긋냄은 사격을 시작할 때 한 번만 정해지므로 판정에 섞이면 지난번 사격이
        /// 뽑아둔 값으로 이번 겨눔을 재게 된다.
        /// </para>
        /// </summary>
        private Vector3 LeadPoint(Vector3 muzzle)
        {
            // 기체 속도가 탄에 얹혀 나가므로 자기 속도를 함께 넘긴다. 그러면 표적이
            // 상대 속도로 계산된다.
            return Ballistics.LeadPoint(
                muzzle,
                _target.position,
                _targetBody != null ? _targetBody.linearVelocity : Vector3.zero,
                _body != null ? _body.linearVelocity : Vector3.zero,
                _gun.MuzzleVelocity);
        }

        /// <summary>한 발을 내보낸다. 리듬은 <see cref="ShotCadence"/>가 지킨다.</summary>
        private void FireSingle()
        {
            Transform muzzle = ResolveMuzzle();
            Vector3 toAim = LeadPoint(muzzle.position) + _aimOffset - muzzle.position;

            Vector3 direction = ProjectileLauncher.ApplySpread(
                toAim.sqrMagnitude > 0.0001f ? toAim.normalized : muzzle.forward,
                _gun.SpreadDegrees);

            ProjectileLauncher.Fire(
                _gun, muzzle.position, direction, CarrierVelocity, gameObject, _hitMask);
        }

        /// <summary>
        /// 사이를 가로막는 것이 있는지 본다.
        /// <para>
        /// 자기 몸통은 세지 않는다. 적기가 시야를 막는 레이어에 있으면 총구가 자기
        /// 콜라이더 안에 있는 순간 스스로에게 막혀 한 발도 쏘지 못한다. 원인을 짐작하기
        /// 어려운 종류의 침묵이다.
        /// </para>
        /// </summary>
        private bool HasLineOfSight(Vector3 from, Vector3 travel, float distance)
        {
            if (!_requireLineOfSight)
            {
                return true;
            }

            int found = Physics.RaycastNonAlloc(
                from, travel / distance, _sightBuffer, distance,
                _sightBlockers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < found; i++)
            {
                if (!_sightBuffer[i].transform.IsChildOf(transform.root))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>기체가 움직이는 만큼 탄에 얹어줄 속도.</summary>
        private Vector3 CarrierVelocity => _body != null ? _body.linearVelocity : Vector3.zero;

        private Transform ResolveMuzzle()
        {
            if (_muzzles.Length == 0)
            {
                return _boresight != null ? _boresight : transform;
            }

            Transform muzzle = _muzzles[_nextMuzzle];
            _nextMuzzle = (_nextMuzzle + 1) % _muzzles.Length;

            return muzzle != null ? muzzle : transform;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _range);

            if (_target != null)
            {
                Gizmos.color = IsFiring ? Color.red : Color.yellow;
                Gizmos.DrawLine(transform.position, _target.position);
            }
        }
    }
}
