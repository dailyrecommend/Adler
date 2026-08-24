using System;
using System.Collections.Generic;
using Adler.Core;
using Adler.Flight;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Weapons
{
    /// <summary>
    /// 조준한 적에게 줄을 걸어 매달린다. 따라가기와 맞추기를 함께 돕는다.
    /// <para>
    /// 걸린 동안에는 놓칠 수 없다. 속도를 <b>상대가 내는 속도에서</b> 계산하므로
    /// 상대가 부스터를 켜든 급기동을 하든 함께 빨라진다. 내 속도에 배율을 곱하는
    /// 방식이면 상대가 가속한 순간 그대로 떨어져 나가는데, 줄로 이어졌다면 그런 일이
    /// 있어서는 안 된다.
    /// </para>
    /// <para>
    /// 조준이 걸려 있어야 쏠 수 있다. 조준에 이미 시간이라는 값이 붙어 있으므로,
    /// 그것을 치른 사람만 쓸 수 있게 하면 별도의 대가를 만들 필요가 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrapplingHook : MonoBehaviour
    {
        /// <summary>
        /// 처짐이 목표를 따라가는 속도. 팽팽해지는 것이 한 순간에 끝나지 않을 만큼만
        /// 늦다 — 이 정도가 눈에는 줄이 채이는 것으로 보인다.
        /// </summary>
        private const float SlackResponse = 14f;

        /// <summary>줄 한 번이 거치는 단계.</summary>
        private enum Phase
        {
            Idle,

            /// <summary>갈고리가 표적을 향해 날아가는 중.</summary>
            Flying,

            /// <summary>물렸지만 줄이 아직 늘어져 있다.</summary>
            Biting,

            /// <summary>줄이 팽팽해져 끌려가는 중.</summary>
            Pulling,
        }

        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("이 장비를 실은 기체. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("줄이 나가는 자리. 비워두면 기체에서 나간다.")]
        [SerializeField] private Transform _origin;

        [Header("끌려가기")]
        [Tooltip("상대보다 이만큼 빠르게 난다 (m/s).\n\n" +
                 "상대가 내는 속도에 그대로 더한다. 상대가 부스터를 켜든 급기동을 하든\n" +
                 "언제나 이만큼씩 가까워지므로, 걸려 있는 동안 거리는 계속 줄어든다.\n\n" +
                 "거리와 무관하게 끝까지 이 속도다. 가까워질수록 늦추면 가장 빨라야 할\n" +
                 "마지막 구간이 가장 느려진다.\n\n" +
                 "유지 시간과 함께 봐야 한다 — 이 값 × 유지 시간보다 멀리서 걸면\n" +
                 "도착하기 전에 시간이 끝나 끌려가다 만다.")]
        [Min(1f)]
        [SerializeField] private float _closeRate = 50f;

        [Tooltip("나아가는 방향이 표적 쪽으로 휘는 정도.\n" +
                 "1이면 조종간과 무관하게 표적으로 끌려간다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _pathBend = 0.9f;

        [Tooltip("이 거리까지 당겨지면 줄이 끊어진다 (m).\n\n" +
                 "다 감았다는 뜻이다. 도착한 뒤에도 매달려 있으면 싸움이 아니라\n" +
                 "끌려다니는 것이 되므로, 붙는 순간이 곧 놓는 순간이다.\n\n" +
                 "가까울수록 표적이 화면을 채워 맞히기 쉬워지지만, 같은 움직임이\n" +
                 "화면에서는 그만큼 격렬해진다. 코앞까지 붙일 거면 아래 충돌 무시를 켤 것.")]
        [Min(0f)]
        [SerializeField] private float _holdRange = 0.5f;

        [Tooltip("붙어 있는 동안 잡아둔 상대와 부딪히지 않는다.\n\n" +
                 "코앞까지 끌어당기면 콜라이더가 겹쳐 둘 다 격추된다. 줄로 매달린 것은\n" +
                 "들이받는 것과 다르므로, 걸려 있는 동안만 서로를 통과시킨다.")]
        [SerializeField] private bool _ignoreCollisionWhileHooked = true;

        [Tooltip("줄이 끊어진 뒤에도 이만큼 떨어질 때까지는 계속 통과시킨다 (m).\n\n" +
                 "끊기는 거리가 가까우면 놓는 순간 두 콜라이더가 겹쳐 있다. 거기서\n" +
                 "충돌을 되살리면 물리가 둘을 밀어내며 튕겨 나가거나 그대로 격추된다.\n" +
                 "빠져나올 때까지 기다렸다가 되살려야 한다.\n\n" +
                 "끊기는 거리보다 넉넉히 크게, 두 기체가 확실히 겹치지 않을 만큼 둘 것.")]
        [Min(0f)]
        [SerializeField] private float _clearRange = 25f;

        [Header("맞추기")]
        [Tooltip("기수가 표적을 따라가는 정도.\n\n" +
                 "줄을 걸면 기수가 표적을 향한다. 그래야 끌려가는 동안 쏠 수 있고,\n" +
                 "그러라고 거는 물건이다 — 향하지 않으면 따라붙어봐야 소용이 없다.\n\n" +
                 "1이면 완전히 고정되어 조종간으로 떼어낼 수 없다. 0.9쯤 두면 거의\n" +
                 "붙어 있으면서도 크게 꺾으면 조금씩 밀어낼 수 있다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _aimAssist = 0.9f;

        [Tooltip("기수가 표적을 따라 돌 수 있는 최대 속도 (도/초).\n\n" +
                 "기체 자신의 선회율(120쯤)과 무관하다. 줄에 매달린 것이므로 혼자서는\n" +
                 "낼 수 없는 속도로 홱 돌아가는 것이 맞고, 기체 성능으로 막아두면\n" +
                 "상대가 급기동하는 순간 기수가 뒤처져 겨눌 수가 없다.")]
        [Range(60f, 720f)]
        [SerializeField] private float _turnRate = 360f;

        [Header("사출")]
        [Tooltip("갈고리가 날아가는 속도 (m/s).\n\n" +
                 "곧바로 걸리면 버튼이 곧 결과라 던졌다는 느낌이 없다. 갈고리가 실제로\n" +
                 "날아가 물리게 하면 멀수록 오래 걸리므로, 가까이 붙어 거는 것과 멀리서\n" +
                 "던지는 것이 서로 다른 선택이 된다.\n\n" +
                 "기체 속도(20~40)보다 충분히 빨라야 한다. 비슷하면 달아나는 상대를\n" +
                 "따라잡지 못해 사거리 밖으로 나갈 때까지 쫓다 빗나간다.")]
        [Min(10f)]
        [SerializeField] private float _travelSpeed = 250f;

        [Tooltip("갈고리가 물린 뒤 당기기 시작할 때까지의 시간(초).\n\n" +
                 "물리자마자 당기면 도착과 견인이 한 순간이라 둘을 구분할 수 없다.\n" +
                 "잠깐 늘어졌다가 팽팽해지면 확 채인다는 느낌이 살고, 끌려가기 직전에\n" +
                 "숨을 고르는 박자가 생긴다.\n\n" +
                 "길게 잡으면 걸어놓고 기다리는 시간이 되므로 짧게 둘 것.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _biteSeconds = 0.15f;

        [Header("제한")]
        [Tooltip("한 번 걸어 유지할 수 있는 시간(초).\n\n" +
                 "다 감기면 저절로 풀리므로 이것은 안전장치에 가깝다. 짧게 잡으면\n" +
                 "도착하기도 전에 끝나서, 끌려가다 마는 것이 줄이 끊긴 것처럼 보인다.\n" +
                 "감기는 속도 × 이 시간이 조준 사거리보다 넉넉해야 한다.")]
        [Min(0.5f)]
        [SerializeField] private float _duration = 10f;

        [Tooltip("끊긴 뒤 다시 걸 수 있을 때까지의 시간(초).")]
        [Min(0f)]
        [SerializeField] private float _cooldown = 6f;

        [Tooltip("이 거리를 넘어가면 줄이 끊어진다 (m).\n" +
                 "조준 사거리보다 넉넉해야 걸자마자 끊기지 않는다.")]
        [Min(1f)]
        [SerializeField] private float _breakRange = 500f;

        [Header("연출")]
        [Tooltip("기체와 표적을 잇는 줄. 비워둬도 동작은 한다.")]
        [SerializeField] private LineRenderer _line;

        [Tooltip("줄을 몇 토막으로 그릴지. 적으면 곡선이 각져 보인다.")]
        [Range(2, 64)]
        [SerializeField] private int _segments = 16;

        [Tooltip("줄이 늘어지는 정도. 줄 길이에 대한 비율이다.\n\n" +
                 "길이에 비례시키는 이유는, 고정값으로 두면 짧을 때는 우스울 만큼\n" +
                 "늘어지고 길 때는 곧은 선처럼 보이기 때문이다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _sag = 0.12f;

        [Tooltip("끌려가는 동안 남는 처짐. 0이면 완전히 곧게 펴진다.\n\n" +
                 "물렸다가 당겨지는 순간 줄이 팽팽해지는 것이 눈에 보여야, 소리와\n" +
                 "몸으로 느끼는 것과 화면이 같은 이야기를 한다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _taut = 0.2f;

        [Tooltip("처지는 방향. 0이면 아래로만, 1이면 지나온 쪽으로만 끌린다.\n\n" +
                 "빠르게 나는 기체에 매달린 줄은 중력보다 공기에 더 끌리므로,\n" +
                 "아래로만 늘어뜨리면 멈춰 있는 것처럼 보인다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _trail = 0.55f;

        // 점선은 재질이 맡는다. Line Renderer의 Texture Mode를 Tile로 두면 무늬가
        // 월드 1미터당 한 번씩 깔리므로, 재질 타일링만 정하면 칸 길이가 미터로
        // 고정된다 — 줄이 늘어나든 휘든 코드가 손댈 일이 없다.

        private LockOnTargeting _targeting;
        private InputAction _grappleAction;

        private Transform _hooked;
        private Rigidbody _hookedBody;
        private float _remaining;
        private float _cooldownRemaining;

        private Phase _phase = Phase.Idle;

        // 날아가는 갈고리 끝의 월드 좌표. 물리기 전까지만 뜻이 있다.
        private Vector3 _tip;

        // 물린 뒤 당기기 시작할 때까지 남은 시간.
        private float _biteRemaining;

        // 지금 줄이 얼마나 늘어져 있는지 (0~1). 팽팽해지는 것을 눈에 보이게 하려고
        // 곧바로 바꾸지 않고 따라가게 둔다.
        private float _slack = 1f;

        // 놓은 뒤 아직 통과시켜 둔 상대. 충분히 떨어지면 충돌을 되살린다.
        private Transform _clearing;
        private float _clearTimeout;

        // 걸린 동안 통과시킨 콜라이더 짝. 되살릴 때 그대로 되돌린다.
        private readonly List<(Collider Mine, Collider Theirs)> _ignoredPairs = new();
        private readonly List<Collider> _myColliders = new();
        private readonly List<Collider> _theirColliders = new();

        /// <summary>갈고리가 날아가기 시작할 때.</summary>
        public event Action<Transform> Fired;

        /// <summary>갈고리가 표적에 닿아 물렸을 때. 아직 끌지는 않는다.</summary>
        public event Action<Transform> Arrived;

        /// <summary>줄이 팽팽해져 끌기 시작할 때.</summary>
        public event Action<Transform> PullStarted;

        /// <summary>줄이 끊어질 때. 날아가는 중에 놓친 경우도 포함한다.</summary>
        public event Action Released;

        /// <summary>쏘아둔 표적. 날아가는 중에도 들어 있다. 없으면 null.</summary>
        public Transform Hooked => _hooked;

        /// <summary>갈고리가 날아가는 중.</summary>
        public bool IsFlying => _phase == Phase.Flying;

        /// <summary>물렸지만 아직 당기기 전.</summary>
        public bool IsBiting => _phase == Phase.Biting;

        /// <summary>줄이 감기며 끌려가는 중.</summary>
        public bool IsPulling => _phase == Phase.Pulling;

        /// <summary>표적에 물려 있다. 버티는 중이든 끌리는 중이든.</summary>
        public bool IsAttached => _phase is Phase.Biting or Phase.Pulling;

        /// <summary>남은 유지 시간(초). 물리기 전에는 0이다.</summary>
        public float Remaining => _remaining;

        /// <summary>다시 걸 수 있을 때까지 남은 시간(초).</summary>
        public float CooldownRemaining => _cooldownRemaining;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _targeting = _aircraft != null ? _aircraft.Targeting : null;

            if (_origin == null)
            {
                _origin = transform;
            }

            if (_aircraft == null || _targeting == null || _controls == null)
            {
                Debug.LogError($"{nameof(GrapplingHook)}: 기체, 조준, Controls 중 빠진 것이 있습니다.", this);
                enabled = false;
                return;
            }

            // 줄의 점들을 월드 좌표로 넘기므로 여기가 켜져 있어야 한다. 꺼져 있으면
            // 기체를 따라 도는 로컬 좌표로 읽혀서, 줄이 엉뚱한 데로 뻗는다.
            if (_line != null)
            {
                _line.useWorldSpace = true;
            }

            ShowLine(false);
        }

        private void OnEnable()
        {
            _grappleAction = _controls.FindActionMap("Flight", throwIfNotFound: true)
                                      .FindAction("Grapple", throwIfNotFound: true);
            _grappleAction.Enable();
        }

        private void OnDisable()
        {
            _grappleAction?.Disable();
            Release();

            // 꺼질 때는 기다리지 않고 바로 되돌린다. 지켜볼 Update가 더는 돌지 않으므로,
            // 미뤄두면 그 짝이 영영 남아 서로를 통과하는 기체가 된다.
            _clearing = null;
            SetCollisionIgnored(false);
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= _clock.Delta;
            }

            if (_grappleAction.WasPressedThisFrame())
            {
                Toggle();
            }

            UpdateHook();
            UpdateClearing();
            DrawLine();
        }

        /// <summary>
        /// 견인은 물리 스텝에 맞춰 넣는다.
        /// <para>
        /// 비행 모델은 넣어준 스텝에만 견인을 쓰고 비운다. 프레임에서 넣으면 화면이
        /// 물리보다 느릴 때 빈 스텝이 생겨, 끌려가다 말다 한다.
        /// </para>
        /// </summary>
        private void FixedUpdate()
        {
            // 끌기 시작한 뒤에만 당긴다. 날아가는 동안 당기면 갈고리보다 기체가 먼저
            // 도착하고, 물자마자 당기면 늘어졌다 팽팽해지는 사이가 사라진다.
            if (_phase != Phase.Pulling)
            {
                return;
            }

            Vector3 toTarget = _hooked.position - _origin.position;
            float distance = toTarget.magnitude;

            if (distance < 0.001f)
            {
                return;
            }

            _aircraft.Model?.SetTether(new Tether(
                toTarget / distance, _pathBend, _aimAssist, SpeedFor(), _turnRate));
        }

        /// <summary>
        /// 상대 속도에 다가가는 몫을 더한 속도.
        /// <para>
        /// 내 속도에 배율을 곱하지 않는다. 그러면 상대가 부스터를 켰을 때 나는 그대로라
        /// 놓치는데, 줄로 이어졌다면 그런 일이 있어서는 안 된다.
        /// </para>
        /// <para>
        /// 거리와 무관하게 끝까지 같은 속도로 당긴다. 가까워질수록 늦추면 가장 빨라야 할
        /// 마지막 구간이 가장 느려져서, 확 끌려가는 것이 아니라 스르르 다가가는 것이 된다.
        /// 어차피 다 감기면 줄이 끊어지므로 지나칠 일도 없다.
        /// </para>
        /// </summary>
        private float SpeedFor()
        {
            if (_hookedBody == null)
            {
                return 0f;
            }

            return _hookedBody.linearVelocity.magnitude + _closeRate;
        }

        /// <summary>같은 키로 걸고 놓는다. 급할 때 놓을 수단이 없으면 갇힌 셈이 된다.</summary>
        private void Toggle()
        {
            // 날아가는 중에 다시 누르면 거둬들인다. 쏘고 나서야 잘못 걸었다는 것을
            // 아는 경우가 있는데, 물릴 때까지 기다려야만 놓을 수 있으면 그 사이가
            // 조작을 빼앗긴 시간이 된다.
            if (_hooked != null)
            {
                Release();
                return;
            }

            if (_cooldownRemaining > 0f || !_targeting.HasLock)
            {
                return;
            }

            // 조준이 걸렸다고 해서 표적이 아직 있다는 보장은 없다. 없는 것에 대고
            // 쏘면 물리지도 끊어지지도 않는 채로 남아 다시 걸 수도 없게 된다.
            Transform target = _targeting.Target;
            if (target == null)
            {
                return;
            }

            _hooked = target;
            _hookedBody = _hooked.GetComponentInParent<Rigidbody>();
            _remaining = 0f;

            _phase = Phase.Flying;
            _tip = _origin.position;

            // 늘어진 채로 시작한다. 지난번에 팽팽했던 값이 남아 있으면 던지자마자
            // 곧게 뻗어서, 아직 날아가는 중인데 이미 당기는 것처럼 보인다.
            _slack = 1f;

            // 통과 처리와 유지 시간은 물릴 때 시작한다. 여기서 켜면 날아가는 동안
            // 상대를 그냥 뚫고 지나가고, 유지 시간도 날아가는 몫만큼 깎여 나간다.
            Fired?.Invoke(_hooked);
        }

        /// <summary>갈고리가 표적에 닿았다. 물기만 하고 아직 당기지 않는다.</summary>
        private void Bite()
        {
            _phase = Phase.Biting;
            _biteRemaining = _biteSeconds;
            _remaining = _duration;

            SetCollisionIgnored(true);
            Arrived?.Invoke(_hooked);

            if (_biteRemaining <= 0f)
            {
                Pull();
            }
        }

        /// <summary>줄이 팽팽해졌다. 여기서부터 끌려간다.</summary>
        private void Pull()
        {
            _phase = Phase.Pulling;
            _biteRemaining = 0f;

            PullStarted?.Invoke(_hooked);
        }

        /// <summary>줄이 아직 유효한지 본다. 실제 견인은 물리 스텝에서 넣는다.</summary>
        private void UpdateHook()
        {
            if (_hooked == null)
            {
                return;
            }

            if (!_hooked.gameObject.activeInHierarchy)
            {
                Release();
                return;
            }

            float distance = Vector3.Distance(_hooked.position, _origin.position);

            // 날아가는 동안에도 사거리는 본다. 쏘자마자 상대가 달아나면 갈고리가
            // 허공에서 물리는 셈이 되는데, 그건 걸린 것이 아니라 빗나간 것이다.
            if (_phase == Phase.Flying)
            {
                if (distance > _breakRange)
                {
                    Release();
                    return;
                }

                Fly();
                return;
            }

            _remaining -= _clock.Delta;

            if (_remaining <= 0f || distance > _breakRange)
            {
                Release();
                return;
            }

            if (_phase == Phase.Biting)
            {
                _biteRemaining -= _clock.Delta;

                if (_biteRemaining <= 0f)
                {
                    Pull();
                }

                return;
            }

            // 다 감기면 놓는다. 줄은 끌어당기는 물건이지 붙잡아 매는 물건이 아니라,
            // 도착한 뒤에도 매달려 있으면 싸움이 아니라 끌려다니는 것이 된다.
            //
            // 버티는 동안에는 보지 않는다. 이미 코앞인 상대에게 걸면 물리자마자
            // 끊겨서, 당기는 소리도 손맛도 없이 끝나 버린다.
            if (distance <= _holdRange)
            {
                Release();
            }
        }

        /// <summary>
        /// 갈고리 끝을 표적 쪽으로 옮긴다. 닿으면 물린다.
        /// <para>
        /// 남은 시간을 세지 않고 실제로 날려 보내는 이유는, 그래야 거리가 저절로
        /// 값이 되기 때문이다. 멀리서 던지면 오래 걸리고 그동안 상대는 움직이므로,
        /// 붙어서 거는 것과 멀리서 거는 것이 서로 다른 선택이 된다.
        /// </para>
        /// <para>
        /// 표적의 지금 위치를 쫓아간다. 쏠 때의 위치로 날아가면 상대가 비켜선 자리에
        /// 가서 물리는데, 조준이 걸린 상대에게 거는 물건이라 놓치는 쪽이 이상하다.
        /// </para>
        /// </summary>
        private void Fly()
        {
            Vector3 toTarget = _hooked.position - _tip;
            float step = _travelSpeed * _clock.Delta;

            if (toTarget.sqrMagnitude <= step * step)
            {
                Bite();
                return;
            }

            _tip += toTarget.normalized * step;
        }

        private void Release()
        {
            if (_hooked == null)
            {
                return;
            }

            // 충돌은 여기서 되살리지 않는다. 끊기는 거리가 가까우면 이 순간 두
            // 콜라이더가 겹쳐 있어서, 되살리는 즉시 물리가 둘을 밀어내거나
            // 부딪힌 것으로 쳐서 격추시킨다. 빠져나올 때까지 기다린다.
            BeginClearing();

            _hooked = null;
            _hookedBody = null;
            _remaining = 0f;
            _biteRemaining = 0f;
            _phase = Phase.Idle;
            _cooldownRemaining = _cooldown;

            Released?.Invoke();
        }

        /// <summary>
        /// 놓은 상대에게서 빠져나올 때까지 통과를 유지한다.
        /// <para>
        /// 걸어둔 짝을 놓는 순간 되돌리지 않는 이유는, 다 감겨서 끊어졌다면 그때 둘이
        /// 가장 가까이 있기 때문이다. 겹친 채로 충돌을 되살리면 물리가 둘을 밀어내며
        /// 튕겨 나가거나, 부딪힌 것으로 쳐서 방금 따라잡은 상대와 함께 떨어진다.
        /// </para>
        /// </summary>
        private void BeginClearing()
        {
            if (_ignoredPairs.Count == 0)
            {
                return;
            }

            _clearing = _hooked;

            // 못 벗어나는 경우를 대비한 안전장치. 상대가 나와 같은 속도로 나란히 날면
            // 거리가 영영 벌어지지 않는데, 그렇다고 영원히 통과시켜 둘 수는 없다.
            _clearTimeout = 5f;
        }

        private void UpdateClearing()
        {
            if (_clearing == null)
            {
                // 상대가 사라졌으면 기다릴 것도 없다. 짝만 남기고 되돌리지 않으면
                // 다음에 같은 상대를 걸었을 때 이유 없이 통과하게 된다.
                if (_ignoredPairs.Count > 0 && _hooked == null)
                {
                    SetCollisionIgnored(false);
                }

                return;
            }

            _clearTimeout -= _clock.Delta;

            bool clear = !_clearing.gameObject.activeInHierarchy
                         || Vector3.Distance(_clearing.position, _origin.position) > _clearRange;

            if (!clear && _clearTimeout > 0f)
            {
                return;
            }

            _clearing = null;
            SetCollisionIgnored(false);
        }

        /// <summary>
        /// 걸려 있는 동안 서로를 통과시킨다.
        /// <para>
        /// 콜라이더 짝을 기억해뒀다가 그대로 되돌린다. 다시 찾아서 되돌리면 그 사이에
        /// 기체가 부서져 콜라이더가 사라졌을 때 짝이 남아, 다음에 같은 상대를 걸었을 때
        /// 이유 없이 통과하게 된다.
        /// </para>
        /// </summary>
        private void SetCollisionIgnored(bool ignored)
        {
            if (!_ignoreCollisionWhileHooked)
            {
                return;
            }

            if (!ignored)
            {
                foreach ((Collider mine, Collider theirs) in _ignoredPairs)
                {
                    if (mine != null && theirs != null)
                    {
                        Physics.IgnoreCollision(mine, theirs, false);
                    }
                }

                _ignoredPairs.Clear();
                return;
            }

            if (_hooked == null)
            {
                return;
            }

            _aircraft.GetComponentsInChildren(includeInactive: false, _myColliders);
            _hooked.root.GetComponentsInChildren(includeInactive: false, _theirColliders);

            foreach (Collider mine in _myColliders)
            {
                foreach (Collider theirs in _theirColliders)
                {
                    Physics.IgnoreCollision(mine, theirs, true);
                    _ignoredPairs.Add((mine, theirs));
                }
            }
        }

        /// <summary>
        /// 줄을 곡선으로 그린다.
        /// <para>
        /// 날아가는 동안에는 끝이 표적을 향해 뻗어나간다. 쏘자마자 표적까지 이어
        /// 그리면 이미 걸린 것처럼 보여서, 정작 물릴 때까지 끌려가지 않는 그 사이가
        /// 고장으로 읽힌다.
        /// </para>
        /// <para>
        /// 처짐은 단계를 따라간다. 물고 버티는 동안은 늘어져 있다가 당기기 시작하면
        /// 팽팽해진다. 곧바로 바꾸지 않고 빠르게 따라가게 두는 이유는, 순간이동하면
        /// 그리는 방식이 바뀐 것처럼 보이고 조금 늦으면 줄이 채이는 것으로 읽히기
        /// 때문이다 — 소리와 화면이 같은 순간을 가리키게 된다.
        /// </para>
        /// </summary>
        private void DrawLine()
        {
            if (_line == null)
            {
                return;
            }

            if (_hooked == null)
            {
                ShowLine(false);
                return;
            }

            Vector3 start = _origin.position;
            Vector3 end = _phase == Phase.Flying ? _tip : _hooked.position;

            float wanted = _phase == Phase.Pulling ? _taut : 1f;
            _slack = Mathf.Lerp(_slack, wanted, 1f - Mathf.Exp(-SlackResponse * _clock.Delta));

            Vector3 middle = Vector3.Lerp(start, end, 0.5f)
                             + (SagDirection() * (Vector3.Distance(start, end) * _sag * _slack));

            int count = Mathf.Max(2, _segments);
            if (_line.positionCount != count)
            {
                _line.positionCount = count;
            }

            ShowLine(true);

            for (int i = 0; i < count; i++)
            {
                _line.SetPosition(i, Bend(start, middle, end, (float)i / (count - 1)));
            }
        }

        /// <summary>
        /// 줄이 늘어지는 쪽.
        /// <para>
        /// 중력만 쓰지 않는다. 빠르게 나는 기체에 매달린 줄은 무게보다 공기에 훨씬
        /// 더 끌리므로, 아래로만 늘어뜨리면 기체가 멈춰 있는 것처럼 보인다.
        /// </para>
        /// </summary>
        private Vector3 SagDirection()
        {
            Vector3 drift = _aircraft.Body != null ? -_aircraft.Body.linearVelocity : Vector3.zero;

            Vector3 direction = drift.sqrMagnitude > 0.0001f
                ? Vector3.Lerp(Vector3.down, drift.normalized, _trail)
                : Vector3.down;

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.down;
        }

        /// <summary>가운데 점 하나로 휘는 2차 베지에. 줄 하나 그리는 데는 이걸로 충분하다.</summary>
        private static Vector3 Bend(Vector3 start, Vector3 middle, Vector3 end, float t)
        {
            float u = 1f - t;

            return (u * u * start) + (2f * u * t * middle) + (t * t * end);
        }

        private void ShowLine(bool visible)
        {
            if (_line != null)
            {
                _line.enabled = visible;
            }
        }
    }
}
