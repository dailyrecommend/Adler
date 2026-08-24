using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.CameraRig
{
    /// <summary>
    /// 기체 주위를 둘러보는 시점 조작.
    /// <para>
    /// 카메라를 직접 돌리지 않고 이 피벗을 돌린다. Cinemachine 카메라가 피벗을 따라오게
    /// 해두면, 피벗이 도는 만큼 카메라가 기체 주위를 공전하면서 그 방향을 바라본다.
    /// 카메라 쪽 설정을 건드리지 않으므로 추적 방식이나 감쇠 값이 그대로 유지된다.
    /// </para>
    /// <para>
    /// 이 오브젝트는 기체의 자식이어야 한다. 회전을 로컬로 다루기 때문에 기체가 기울면
    /// 시야도 함께 기울고, 둘러본 방향은 기체 기준으로 유지된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FreeLookPivot : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("재출격할 때 정면으로 돌아가려고 지켜본다. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("커서")]
        [Tooltip("커서를 화면 중앙에 묶고 숨긴다.\n" +
                 "풀어두면 커서가 화면 밖으로 나가면서 이동량이 끊기고, 창을 벗어난 순간\n" +
                 "시야가 튄다. 나중에 일시정지 메뉴를 만들면 그쪽에서 풀어줘야 한다.")]
        [SerializeField] private bool _lockCursor = true;

        [Header("감도")]
        [Tooltip("마우스 감도. 마우스 이동량은 프레임당 픽셀이라 시간을 곱하지 않는다.")]
        [SerializeField] private float _mouseSensitivity = 0.12f;

        [Tooltip("한 프레임에 돌아갈 수 있는 최대 각도.\n" +
                 "창 전환이나 재생 시작 직후에 들어오는 비정상적으로 큰 이동량이\n" +
                 "시야를 한 번에 꺾어버리는 것을 막는다.")]
        [Min(1f)]
        [SerializeField] private float _maxStepDegrees = 20f;

        [Tooltip("스틱 감도 (초당 회전 각도).")]
        [SerializeField] private float _gamepadSensitivity = 180f;

        [SerializeField] private bool _invertPitch;

        [Header("범위")]
        [Tooltip("좌우로 돌아볼 수 있는 최대 각도. 160이면 뒤쪽까지 거의 다 보인다.")]
        [Range(0f, 180f)]
        [SerializeField] private float _yawLimit = 160f;

        [Range(0f, 89f)]
        [SerializeField] private float _pitchLimit = 70f;

        [Header("복귀")]
        [Tooltip("정면으로 돌아오는 속도. 클수록 빠르게 제자리를 찾는다.")]
        [SerializeField] private float _recenterSpeed = 8f;

        [Tooltip("둘러보기를 멈추면 저절로 정면으로 돌아온다.\n" +
                 "끄면 휠 클릭(스틱 클릭)으로만 복귀한다.")]
        [SerializeField] private bool _autoRecenter = true;

        [Tooltip("입력이 멈추고 이만큼 지나면 복귀를 시작한다(초).\n" +
                 "너무 짧으면 잠깐 손을 뗀 사이에 시야가 돌아가 버린다.")]
        [Min(0f)]
        [SerializeField] private float _autoRecenterDelay = 1.2f;

        [Tooltip("이 상태가 시작되는 순간 정면으로 돌려보낸다.\n\n" +
                 "부스터가 대표적이다. 가속하는 순간 시야가 옆을 보고 있으면 어디로\n" +
                 "튀어나가는지 알 수 없어서, 빨라졌다는 감각이 불안으로 바뀐다.")]
        [SerializeField] private bool _recenterOnCondition = true;

        [Tooltip("무엇이 시작될 때 돌려보낼지.")]
        [SerializeField] private AircraftCondition _recenterWhen = AircraftCondition.Boosting;

        [Tooltip("Debuff를 고른 경우에만 쓴다.")]
        [SerializeField] private DebuffDefinition _recenterDebuff;


        private InputAction _lookAction;
        private InputAction _recenterAction;

        private float _yaw;
        private float _pitch;
        private bool _recentering;
        private bool _conditionWasMet;
        private float _idleTime;

        /// <summary>지금 정면을 보고 있는지. 조준 보조나 HUD 표시가 참조할 수 있다.</summary>
        public bool IsCentered => Mathf.Approximately(_yaw, 0f) && Mathf.Approximately(_pitch, 0f);

        private AircraftLifecycle _lifecycle;

        /// <summary>
        /// 지켜볼 기체를 찾아둔다.
        /// <para>
        /// 재출격이 카메라를 되돌리게 하지 않고 카메라가 재출격을 지켜본다. 반대로 두면
        /// 기체가 카메라를 알아야 해서, 연출을 하나 붙일 때마다 기체 쪽 목록이 늘어난다.
        /// </para>
        /// </summary>
        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _lifecycle = _aircraft != null ? _aircraft.Lifecycle : null;
        }

        private void OnEnable()
        {
            if (_lifecycle != null)
            {
                _lifecycle.Respawned += SnapToCenter;
            }

            if (_controls == null)
            {
                Debug.LogError($"{nameof(FreeLookPivot)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            InputActionMap map = _controls.FindActionMap("Flight", throwIfNotFound: true);
            _lookAction = map.FindAction("CameraLook", throwIfNotFound: true);
            _recenterAction = map.FindAction("RecenterCamera", throwIfNotFound: true);

            _lookAction.Enable();
            _recenterAction.Enable();

            // 켜질 때마다 정면에서 시작한다. 이 초기화가 없으면 재생 직후 들어오는
            // 첫 이동량이 그대로 반영돼, 이미 돌아간 시야로 시작하게 된다.
            SnapToCenter();
            ApplyCursorLock(_lockCursor);
        }

        private void OnDisable()
        {
            if (_lifecycle != null)
            {
                _lifecycle.Respawned -= SnapToCenter;
            }

            _lookAction?.Disable();
            _recenterAction?.Disable();
            ApplyCursorLock(false);
        }

        /// <summary>일시정지 메뉴처럼 커서가 필요한 상황에서 밖에서 풀어준다.</summary>
        public void SetCursorLocked(bool locked)
        {
            _lockCursor = locked;
            ApplyCursorLock(locked);
        }

        private static void ApplyCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            // 먼저 재둔다. 단락 평가에 맡기면 복귀 키를 누른 프레임에는 모서리
            // 판정이 건너뛰어져, 그다음 프레임에 한 번 더 돌아간다.
            bool started = ConditionJustStarted();

            if (_recenterAction.WasPressedThisFrame() || started)
            {
                _recentering = true;
            }

            Vector2 look = ReadLook();

            if (look.sqrMagnitude > 0f)
            {
                // 둘러보기 시작하면 복귀를 그만둔다. 복귀 중에 마우스를 움직였는데
                // 시야가 계속 정면으로 끌려가면 조작을 빼앗긴 느낌이 든다.
                _recentering = false;
                _idleTime = 0f;

                _yaw = Mathf.Clamp(_yaw + look.x, -_yawLimit, _yawLimit);
                _pitch = Mathf.Clamp(_pitch + (_invertPitch ? look.y : -look.y), -_pitchLimit, _pitchLimit);
            }
            else
            {
                _idleTime += _clock.Delta;

                if (_autoRecenter && _idleTime >= _autoRecenterDelay)
                {
                    _recentering = true;
                }

                if (_recentering)
                {
                    float t = 1f - Mathf.Exp(-_recenterSpeed * _clock.Delta);
                    _yaw = Mathf.Lerp(_yaw, 0f, t);
                    _pitch = Mathf.Lerp(_pitch, 0f, t);

                    if (Mathf.Abs(_yaw) < 0.05f && Mathf.Abs(_pitch) < 0.05f)
                    {
                        _yaw = 0f;
                        _pitch = 0f;
                        _recentering = false;
                    }
                }
            }

            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        /// <summary>
        /// 마우스와 스틱은 값의 성질이 달라 같은 배율을 쓸 수 없다.
        /// 마우스 델타는 그 프레임에 움직인 픽셀 수라 이미 프레임과 무관하지만,
        /// 스틱은 기울인 정도라서 시간을 곱해야 프레임률에 따라 속도가 달라지지 않는다.
        /// </summary>
        /// <summary>
        /// 정해둔 상태가 이번 프레임에 <b>시작됐는지</b>.
        /// <para>
        /// 이어지는 동안이 아니라 시작하는 모서리만 본다. 부스터를 켜고 있는 내내
        /// 정면으로 끌어당기면 그동안 둘러볼 수가 없어서, 시야를 되찾아주는 것이
        /// 아니라 빼앗는 것이 된다.
        /// </para>
        /// </summary>
        private bool ConditionJustStarted()
        {
            if (!_recenterOnCondition)
            {
                return false;
            }

            bool met = AircraftConditions.IsMet(_aircraft, _recenterWhen, _recenterDebuff);
            bool started = met && !_conditionWasMet;

            _conditionWasMet = met;
            return started;
        }

        private Vector2 ReadLook()
        {
            Vector2 raw = _lookAction.ReadValue<Vector2>();
            if (raw.sqrMagnitude <= 0f)
            {
                return Vector2.zero;
            }

            bool fromGamepad = _lookAction.activeControl?.device is Gamepad;

            Vector2 scaled = fromGamepad
                ? raw * (_gamepadSensitivity * _clock.Delta)
                : raw * _mouseSensitivity;

            // 창을 되찾거나 재생이 시작되는 프레임에는 이동량이 비정상적으로 크게 들어온다.
            // 감도를 낮춰서 해결할 수 있는 문제가 아니라 한 번의 튐이므로 상한으로 막는다.
            return Vector2.ClampMagnitude(scaled, _maxStepDegrees);
        }

        /// <summary>밖에서 시야를 즉시 정면으로 되돌린다. 리스폰이나 연출 전환에 쓴다.</summary>
        public void SnapToCenter()
        {
            _yaw = 0f;
            _pitch = 0f;
            _recentering = false;
            transform.localRotation = Quaternion.identity;
        }
    }
}
