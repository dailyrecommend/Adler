using System;
using Adler.CameraRig;
using Adler.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Flight
{
    /// <summary>
    /// 기체가 격추되고 다시 뜨는 과정을 맡는다.
    /// <para>
    /// 격추와 리스폰을 나눠두면 서로를 참조해야 한다 — 되살리는 쪽이 무엇을 껐는지
    /// 알아야 하기 때문이다. 같은 목록을 두 곳에 적어두면 한쪽만 고쳤을 때 조종이
    /// 돌아오지 않는데, 원인을 짐작하기 어려운 종류의 고장이다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(AircraftRig))]
    [DisallowMultipleComponent]
    public sealed class AircraftLifecycle : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("되돌아갈 자리. 비워두면 시작할 때의 위치와 자세를 기억해 쓴다.")]
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("둘러보던 시야. 비워두면 기체 아래에서 찾는다.")]
        [SerializeField] private FreeLookPivot _freeLook;

        [Header("잔해")]
        [Tooltip("격추되면 중력을 되돌려 잔해가 떨어진다.")]
        [SerializeField] private bool _restorePhysics = true;

        [Tooltip("잔해가 회전하며 떨어지는 정도.")]
        [SerializeField] private float _tumbleTorque = 4f;

        [Tooltip("조종 외에 추가로 끌 것이 있으면 넣는다. 보통은 비워둔다.")]
        [SerializeField] private Behaviour[] _alsoDisableOnDeath = Array.Empty<Behaviour>();

        [Header("리스폰")]
        [Tooltip("격추됐을 때만 되돌린다. 끄면 살아 있어도 언제든 처음으로 돌아간다.")]
        [SerializeField] private bool _onlyWhenDead;

        private AircraftRig _aircraft;
        private InputAction _respawnAction;
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        /// <summary>격추됐을 때.</summary>
        public event Action Destroyed;

        /// <summary>되돌린 직후.</summary>
        public event Action Respawned;

        private void Awake()
        {
            _aircraft = GetComponent<AircraftRig>();

            if (_freeLook == null)
            {
                _freeLook = GetComponentInChildren<FreeLookPivot>(includeInactive: true);
            }

            _startPosition = transform.position;
            _startRotation = transform.rotation;
        }

        private void OnEnable()
        {
            if (_aircraft.Health != null)
            {
                _aircraft.Health.Died += OnDied;
            }

            if (_controls == null)
            {
                Debug.LogError($"{nameof(AircraftLifecycle)}: Controls 에셋이 비어 있습니다.", this);
                return;
            }

            _respawnAction = _controls.FindActionMap("Flight", throwIfNotFound: true)
                                      .FindAction("Respawn", throwIfNotFound: true);
            _respawnAction.Enable();
        }

        private void OnDisable()
        {
            if (_aircraft.Health != null)
            {
                _aircraft.Health.Died -= OnDied;
            }

            _respawnAction?.Disable();
        }

        private void Update()
        {
            if (_respawnAction == null || !_respawnAction.WasPressedThisFrame())
            {
                return;
            }

            if (_onlyWhenDead && _aircraft.Health != null && _aircraft.Health.IsAlive)
            {
                return;
            }

            Respawn();
        }

        // ------------------------------------------------------------------
        // 격추
        // ------------------------------------------------------------------

        /// <summary>
        /// 죽었다고 표시하는 것만으로는 아무것도 달라지지 않는다. 비행 모델이 매 물리
        /// 스텝 속도와 회전을 덮어쓰므로, 조종을 꺼야 비로소 기체가 날기를 멈춘다.
        /// </summary>
        private void OnDied(Health health, DamageInfo damage)
        {
            SetControlEnabled(false);
            Destroyed?.Invoke();

            if (!_restorePhysics)
            {
                return;
            }

            // 비행 모델이 꺼둔 것들을 되돌린다. 여기서 되살리지 않으면 잔해가
            // 부딪힌 자리에 그대로 떠 있게 된다.
            Rigidbody body = _aircraft.Body;
            body.useGravity = true;
            body.linearDamping = 0.2f;
            body.angularDamping = 0.4f;

            if (_tumbleTorque > 0f)
            {
                body.AddTorque(UnityEngine.Random.onUnitSphere * _tumbleTorque, ForceMode.VelocityChange);
            }
        }

        /// <summary>
        /// 끌 것을 목록으로 받지 않고 기체에서 꺼내 쓴다. 손으로 적어두면 컴포넌트를
        /// 하나 붙일 때마다 여기에도 넣어야 하고, 빠뜨리면 죽은 기체가 계속 쏜다.
        /// </summary>
        private void SetControlEnabled(bool enabled)
        {
            Toggle(_aircraft.Control, enabled);
            Toggle(_aircraft.Weapons, enabled);
            Toggle(_aircraft.Stratagems, enabled);

            foreach (Behaviour behaviour in _alsoDisableOnDeath)
            {
                Toggle(behaviour, enabled);
            }
        }

        private static void Toggle(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }

        // ------------------------------------------------------------------
        // 리스폰
        // ------------------------------------------------------------------

        /// <summary>밖에서도 부를 수 있다. 임무 실패 화면 같은 데서 쓴다.</summary>
        public void Respawn()
        {
            // 조종을 먼저 되살린다. 꺼진 채로 위치만 옮기면 기체가 그 자리에서 떨어진다.
            SetControlEnabled(true);

            if (_aircraft.Health != null)
            {
                _aircraft.Health.Revive();
            }

            MoveToSpawn();

            // 물리와 위치가 정리된 뒤에 비행 상태를 잡아야, 추락하던 속도와 자세가
            // 남은 채로 되살아나지 않는다.
            _aircraft.Control.ResetFlight();

            _aircraft.Weapons?.RestockAll();
            _aircraft.Boost?.Refill();
            _aircraft.Stratagems?.ResetRestrictions();

            // 죽기 전에 걸어둔 수리가 남아 있으면 가득 찬 기체를 계속 채우게 된다.
            _aircraft.Repair?.Stop(completed: false);

            if (_freeLook != null)
            {
                _freeLook.SnapToCenter();
            }

            Respawned?.Invoke();
        }

        private void MoveToSpawn()
        {
            Vector3 position = _spawnPoint != null ? _spawnPoint.position : _startPosition;
            Quaternion rotation = _spawnPoint != null ? _spawnPoint.rotation : _startRotation;

            // 물리 위치를 함께 옮긴다. transform만 바꾸면 보간이 옛 자리에서 새 자리까지
            // 한 줄로 이어 그려서, 기체가 맵을 가로질러 날아간 것처럼 보인다.
            Rigidbody body = _aircraft.Body;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
