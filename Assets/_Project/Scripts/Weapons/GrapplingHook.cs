using System;
using System.Collections.Generic;
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
                 "유지 시간과 함께 봐야 한다 — 이 값 × 유지 시간보다 멀리서 걸면\n" +
                 "도착하기 전에 시간이 끝나 끌려가다 만다.")]
        [Min(1f)]
        [SerializeField] private float _closeRate = 50f;

        [Tooltip("나아가는 방향이 표적 쪽으로 휘는 정도.\n" +
                 "1이면 조종간과 무관하게 표적으로 끌려간다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _pathBend = 0.9f;

        [Tooltip("붙은 뒤 유지할 거리 (m).\n\n" +
                 "여기까지 오면 다가가기를 멈추고 상대와 같은 속도로 난다. 계속 더 빠르면\n" +
                 "그대로 지나쳐 버리고, 지나치면 상대가 뒤로 가서 놓친다.\n\n" +
                 "가까울수록 표적이 화면을 채워 맞히기 쉬워지지만, 같은 움직임이\n" +
                 "화면에서는 그만큼 격렬해진다. 코앞까지 붙일 거면 아래 충돌 무시를 켜둘 것.")]
        [Min(0f)]
        [SerializeField] private float _holdRange = 5f;

        [Tooltip("붙어 있는 동안 잡아둔 상대와 부딪히지 않는다.\n\n" +
                 "코앞까지 끌어당기면 콜라이더가 겹쳐 둘 다 격추된다. 줄로 매달린 것은\n" +
                 "들이받는 것과 다르므로, 걸려 있는 동안만 서로를 통과시킨다.")]
        [SerializeField] private bool _ignoreCollisionWhileHooked = true;

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

        private LockOnTargeting _targeting;
        private InputAction _grappleAction;

        private Transform _hooked;
        private Rigidbody _hookedBody;
        private float _remaining;
        private float _cooldownRemaining;

        // 걸린 동안 통과시킨 콜라이더 짝. 놓을 때 그대로 되돌린다.
        private readonly List<(Collider Mine, Collider Theirs)> _ignoredPairs = new();
        private readonly List<Collider> _myColliders = new();
        private readonly List<Collider> _theirColliders = new();

        /// <summary>걸리거나 끊어질 때. 표적이 없으면 끊어진 것이다.</summary>
        public event Action<Transform> Changed;

        /// <summary>지금 걸려 있는 표적. 없으면 null.</summary>
        public Transform Hooked => _hooked;

        /// <summary>남은 유지 시간(초).</summary>
        public float Remaining => _remaining;

        /// <summary>다시 걸 수 있을 때까지 남은 시간(초).</summary>
        public float CooldownRemaining => _cooldownRemaining;

        private void Awake()
        {
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
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
            }

            if (_grappleAction.WasPressedThisFrame())
            {
                Toggle();
            }

            UpdateHook();
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
            if (_hooked == null)
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
                toTarget / distance, _pathBend, _aimAssist, SpeedFor(distance), _turnRate));
        }

        /// <summary>
        /// 상대 속도에 다가가는 몫을 더한 속도.
        /// <para>
        /// 내 속도에 배율을 곱하지 않는다. 그러면 상대가 부스터를 켰을 때 나는 그대로라
        /// 놓치는데, 줄로 이어졌다면 그런 일이 있어서는 안 된다.
        /// </para>
        /// <para>
        /// 다가가는 몫은 유지 거리에 닿으면 0이 된다. 끝까지 더 빠르면 상대를 지나쳐
        /// 앞질러 버리는데, 그러면 상대가 뒤로 가서 겨눌 수도 따라갈 수도 없다.
        /// 붙은 뒤에는 나란히 나는 것이 쫓는 것이다.
        /// </para>
        /// </summary>
        private float SpeedFor(float distance)
        {
            if (_hookedBody == null)
            {
                return 0f;
            }

            float closing = Mathf.Clamp(distance - _holdRange, 0f, _closeRate);

            return _hookedBody.linearVelocity.magnitude + closing;
        }

        /// <summary>같은 키로 걸고 놓는다. 급할 때 놓을 수단이 없으면 갇힌 셈이 된다.</summary>
        private void Toggle()
        {
            if (_hooked != null)
            {
                Release();
                return;
            }

            if (_cooldownRemaining > 0f || !_targeting.HasLock)
            {
                return;
            }

            _hooked = _targeting.Target;
            _hookedBody = _hooked != null ? _hooked.GetComponentInParent<Rigidbody>() : null;
            _remaining = _duration;

            SetCollisionIgnored(true);
            Changed?.Invoke(_hooked);
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

            _remaining -= Time.deltaTime;

            float distance = Vector3.Distance(_hooked.position, _origin.position);

            // 다 감기면 놓는다. 줄은 끌어당기는 물건이지 붙잡아 매는 물건이 아니라,
            // 도착한 뒤에도 매달려 있으면 싸움이 아니라 끌려다니는 것이 된다.
            if (_remaining <= 0f || distance > _breakRange || distance <= _holdRange)
            {
                Release();
            }
        }

        private void Release()
        {
            if (_hooked == null)
            {
                return;
            }

            SetCollisionIgnored(false);

            _hooked = null;
            _hookedBody = null;
            _remaining = 0f;
            _cooldownRemaining = _cooldown;

            Changed?.Invoke(null);
        }

        /// <summary>
        /// 걸려 있는 동안 서로를 통과시킨다.
        /// <para>
        /// 콜라이더 짝을 기억해뒀다가 놓을 때 그대로 되돌린다. 다시 찾아서 되돌리면
        /// 그 사이에 기체가 부서져 콜라이더가 사라졌을 때 짝이 남아, 다음에 같은 상대를
        /// 걸었을 때 이유 없이 통과하게 된다.
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

            ShowLine(true);
            _line.positionCount = 2;
            _line.SetPosition(0, _origin.position);
            _line.SetPosition(1, _hooked.position);
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
