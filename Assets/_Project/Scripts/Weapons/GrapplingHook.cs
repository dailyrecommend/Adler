using System;
using Adler.Abilities;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>줄 한 번이 거치는 단계.</summary>
    public enum GrapplePhase
    {
        /// <summary>걸린 것이 없다.</summary>
        Idle,

        /// <summary>갈고리가 표적을 향해 날아가는 중.</summary>
        Flying,

        /// <summary>물렸지만 줄이 아직 늘어져 있다.</summary>
        Biting,

        /// <summary>줄이 팽팽해져 끌려가는 중.</summary>
        Pulling,
    }

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
        /// <summary>못 벗어나는 경우를 대비한 안전장치(초). 상대가 나란히 날면 거리가 영영 벌어지지 않는다.</summary>
        private const float ClearTimeout = 5f;

        [Header("참조")]
        [Tooltip("이 장비를 실은 기체의 뿌리. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRoot _root;

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

        [Tooltip("이 거리를 넘어가면 줄이 끊어진다 (m).\n" +
                 "조준 사거리보다 넉넉해야 걸자마자 끊기지 않는다.")]
        [Min(1f)]
        [SerializeField] private float _breakRange = 500f;

        private LockOnTargeting _targeting;
        private IMovementDriver _mover;

        private Transform _hooked;
        private Rigidbody _hookedBody;
        private float _remaining;

        private readonly StateMachine<GrapplePhase> _phase = new(GrapplePhase.Idle);

        // 날아가는 갈고리 끝의 월드 좌표. 물리기 전까지만 뜻이 있다.
        private Vector3 _tip;

        // 걸어둔 상대를 통과시키는 일은 통째로 맡긴다. 되돌리는 시점이 까다로운데,
        // 그 까다로움은 들이받기도 똑같이 겪는 것이라 한곳에 모아뒀다.
        private CollisionPassage _passage;

        /// <summary>쏘아둔 표적. 날아가는 중에도 들어 있다. 없으면 null.</summary>
        public Transform Hooked => _hooked;

        /// <summary>줄이 나가는 자리. 그리는 쪽이 여기서 시작한다.</summary>
        public Transform Origin => _origin;

        /// <summary>
        /// 날아가는 갈고리 끝. <see cref="GrapplePhase.Flying"/> 동안만 뜻이 있다.
        /// </summary>
        public Vector3 Tip => _tip;

        /// <summary>지금 어느 단계인지. 소리와 화면이 이것 하나만 보면 된다.</summary>
        public GrapplePhase Phase => _phase.Current;

        /// <summary>지금 단계에 머문 시간(초).</summary>
        public float PhaseElapsed => _phase.Elapsed;

        /// <summary>단계가 바뀔 때. (떠난 단계, 들어선 단계)</summary>
        public event Action<GrapplePhase, GrapplePhase> PhaseChanged
        {
            add => _phase.Changed += value;
            remove => _phase.Changed -= value;
        }

        /// <summary>표적에 물려 있다. 버티는 중이든 끌리는 중이든.</summary>
        public bool IsAttached => Phase is GrapplePhase.Biting or GrapplePhase.Pulling;

        /// <summary>남은 유지 시간(초). 물리기 전에는 0이다.</summary>
        public float Remaining => _remaining;

        // 쿨타임은 여기 없다. 행동의 제한은 실행기의 장부 하나에 오르고, 이 부품은
        // 줄의 물리만 안다 — 언제 또 던질 수 있는지는 이쪽이 답할 질문이 아니다.

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _root = AircraftRoot.Resolve(this, _root);
            _targeting = _root != null ? _root.Find<LockOnTargeting>() : null;
            _mover = _root != null ? _root.Find<IMovementDriver>() : null;

            if (_origin == null)
            {
                _origin = transform;
            }

            if (_root == null || _targeting == null || _mover == null)
            {
                Debug.LogError($"{nameof(GrapplingHook)}: 기체 뿌리, 조준, 조종 중 빠진 것이 있습니다.", this);
                enabled = false;
                return;
            }

            _passage = new CollisionPassage(_root.transform);
        }

        private void OnDisable()
        {
            Release();

            // 꺼질 때는 기다리지 않고 바로 되돌린다. 지켜볼 Update가 더는 돌지 않으므로,
            // 미뤄두면 그 짝이 영영 남아 서로를 통과하는 기체가 된다.
            _passage.CloseAll();
        }

        private void Update()
        {
            _phase.Advance(_clock.Delta);

            UpdateHook();
            UpdateContact();

            _passage.Tick(_clock.Delta);
        }

        /// <summary>
        /// 부딪히는 것이 목적인 동안은 참. 들이받기 쪽이 매 프레임 알려준다.
        /// <para>
        /// 이쪽이 그쪽에게 묻지 않고 그쪽이 이쪽에 써넣는 이유는 방향 때문이다.
        /// 기체(위층)가 장비(아래층)를 아는 것은 자연스럽지만, 장비가 기체의 다른
        /// 부품을 알아 올려다보기 시작하면 층이 서로를 물고 돈다.
        /// </para>
        /// </summary>
        public bool KeepContact { get; set; }

        /// <summary>
        /// 부딪히는 것이 목적이 아닌 동안만 통과시킨다.
        /// <para>
        /// 서로를 통과시키는 것은 코앞까지 끌어당겼을 때 둘 다 격추되는 것을 막으려는
        /// 것이었다. 그런데 들이받기가 생기면서 그 전제가 바뀌었다 — 매달린 채로
        /// 부스터를 밟는 것이 곧 "이대로 박겠다"는 뜻이 되므로, 통과시켜 버리면
        /// 그 선택이 화면에서 아무 일도 아닌 것이 된다.
        /// </para>
        /// </summary>
        private void UpdateContact()
        {
            if (!IsAttached || !_ignoreCollisionWhileHooked)
            {
                return;
            }

            if (KeepContact)
            {
                _passage.Close(_hooked);
            }
            else
            {
                _passage.Open(_hooked);
            }
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
            if (!_phase.Is(GrapplePhase.Pulling))
            {
                return;
            }

            Vector3 toTarget = _hooked.position - _origin.position;
            float distance = toTarget.magnitude;

            if (distance < 0.001f)
            {
                return;
            }

            _mover.Pull(new Tether(
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

        /// <summary>
        /// 조준한 표적에 갈고리를 던진다. 던져졌으면 참.
        /// <para>
        /// 쿨타임은 묻지 않는다. 그것은 실행기가 이미 문턱에서 거른 뒤고, 여기서 또
        /// 물으면 같은 제한을 두 곳이 세게 된다.
        /// </para>
        /// </summary>
        public bool Fire()
        {
            if (_hooked != null || !_targeting.HasLock)
            {
                return false;
            }

            // 조준이 걸렸다고 해서 표적이 아직 있다는 보장은 없다. 없는 것에 대고
            // 쏘면 물리지도 끊어지지도 않는 채로 남아 다시 걸 수도 없게 된다.
            Transform target = _targeting.Target;
            if (target == null)
            {
                return false;
            }

            _hooked = target;
            _hookedBody = _hooked.GetComponentInParent<Rigidbody>();
            _remaining = 0f;

            _phase.Set(GrapplePhase.Flying);
            _tip = _origin.position;

            // 통과 처리와 유지 시간은 물릴 때 시작한다. 여기서 켜면 날아가는 동안
            // 상대를 그냥 뚫고 지나가고, 유지 시간도 날아가는 몫만큼 깎여 나간다.

            return true;
        }

        /// <summary>갈고리가 표적에 닿았다. 물기만 하고 아직 당기지 않는다.</summary>
        private void Bite()
        {
            _phase.Set(GrapplePhase.Biting);
            _remaining = _duration;

            if (_ignoreCollisionWhileHooked)
            {
                _passage.Open(_hooked);
            }

            // 뜸이 0이면 물자마자 당긴다. 다음 프레임을 기다리면 그 한 프레임 동안
            // 물렸는데도 끌려가지 않는 상태가 생긴다.
            if (_biteSeconds <= 0f)
            {
                Pull();
            }
        }

        /// <summary>줄이 팽팽해졌다. 여기서부터 끌려간다.</summary>
        private void Pull()
        {
            _phase.Set(GrapplePhase.Pulling);

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
            if (_phase.Is(GrapplePhase.Flying))
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

            if (_phase.Is(GrapplePhase.Biting))
            {
                // 머문 시간은 상태기계가 센다. 상태마다 남은 시간을 따로 담아두면
                // 옮겨갈 때 그것을 되돌리는 일을 잊을 수 있다.

                if (_phase.Elapsed >= _biteSeconds)
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

        /// <summary>줄을 놓는다. 날아가는 중이든 매달린 중이든, 이미 놓였으면 아무 일도 없다.</summary>
        public void Release()
        {
            if (_hooked == null)
            {
                return;
            }

            // 충돌은 여기서 되살리지 않는다. 끊기는 거리가 가까우면 이 순간 두
            // 콜라이더가 겹쳐 있어서, 되살리는 즉시 물리가 둘을 밀어내거나
            // 부딪힌 것으로 쳐서 격추시킨다. 빠져나올 때까지 기다린다.
            //
            // 기한을 함께 주는 것은 상대가 나와 나란히 날면 거리가 영영 벌어지지 않기
            // 때문이다. 그렇다고 영원히 통과시켜 둘 수는 없다.
            _passage.Release(_hooked, _clearRange, ClearTimeout);

            _hooked = null;
            _hookedBody = null;
            _remaining = 0f;
            _phase.Set(GrapplePhase.Idle);
        }

    }
}
