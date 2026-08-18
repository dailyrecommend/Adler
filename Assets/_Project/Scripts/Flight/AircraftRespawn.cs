using System;
using Adler.CameraRig;
using Adler.Combat;
using Adler.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Flight
{
    /// <summary>
    /// 기체를 출발 지점으로 되돌린다.
    /// <para>
    /// 격추가 생긴 뒤로는 한 번 죽을 때마다 씬을 다시 재생해야 했다. 수치를 만지고
    /// 확인하는 일이 잦은 단계에서 그 왕복은 그대로 작업 속도가 된다.
    /// </para>
    /// <para>
    /// 되돌릴 것이 여러 곳에 흩어져 있어 순서가 중요하다. 물리와 위치를 먼저 정리한
    /// 뒤에 비행 상태를 다시 잡아야, 추락하던 속도가 남은 채로 되살아나지 않는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftRespawn : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("되돌아갈 자리. 비워두면 시작할 때의 위치와 자세를 기억해 쓴다.")]
        [SerializeField] private Transform _spawnPoint;

        [SerializeField] private Rigidbody _body;
        [SerializeField] private AircraftController _controller;
        [SerializeField] private Health _health;
        [SerializeField] private AircraftWreck _wreck;

        [Header("함께 되돌릴 것")]
        [Tooltip("탄약. 비워두면 건드리지 않는다.")]
        [SerializeField] private GunAmmo _ammo;

        [Tooltip("부스터 연료. 비워두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private BoostFuel _boostFuel;

        [Tooltip("스트라타젬 쿨타임과 사용 횟수를 되돌린다. 비워두면 부모에서 찾는다.")]
        [SerializeField] private StratagemBay _stratagemBay;

        [Tooltip("둘러보던 시야. 비워두면 건드리지 않는다.")]
        [SerializeField] private FreeLookPivot _freeLook;

        [Header("조건")]
        [Tooltip("격추됐을 때만 되돌린다. 끄면 살아 있어도 언제든 처음으로 돌아간다.")]
        [SerializeField] private bool _onlyWhenDead;

        private InputAction _respawnAction;
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        /// <summary>되돌린 직후. 화면 표시나 소리가 구독한다.</summary>
        public event Action Respawned;

        private void Awake()
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_controller == null)
            {
                _controller = GetComponent<AircraftController>();
            }

            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_wreck == null)
            {
                _wreck = GetComponent<AircraftWreck>();
            }

            if (_boostFuel == null)
            {
                _boostFuel = GetComponent<BoostFuel>();
            }

            if (_stratagemBay == null)
            {
                _stratagemBay = GetComponentInChildren<StratagemBay>();
            }

            if (_body == null || _controller == null)
            {
                Debug.LogError($"{nameof(AircraftRespawn)}: 기체의 Rigidbody나 조종 컴포넌트를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _startPosition = transform.position;
            _startRotation = transform.rotation;
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                Debug.LogError($"{nameof(AircraftRespawn)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _respawnAction = _controls.FindActionMap("Flight", throwIfNotFound: true)
                                      .FindAction("Respawn", throwIfNotFound: true);
            _respawnAction.Enable();
        }

        private void OnDisable() => _respawnAction?.Disable();

        private void Update()
        {
            if (!_respawnAction.WasPressedThisFrame())
            {
                return;
            }

            if (_onlyWhenDead && _health != null && _health.IsAlive)
            {
                return;
            }

            Respawn();
        }

        /// <summary>밖에서도 부를 수 있다. 임무 실패 화면 같은 데서 쓴다.</summary>
        public void Respawn()
        {
            // 격추 처리가 꺼둔 것들을 먼저 되살린다. 조종이 꺼진 채로 위치만 옮기면
            // 기체가 그 자리에서 떨어지기만 한다.
            if (_wreck != null)
            {
                _wreck.Restore();
            }

            if (_health != null)
            {
                _health.Revive();
            }

            MoveToSpawn();

            // 물리와 위치가 정리된 뒤에 비행 상태를 잡아야, 추락하던 속도와 자세가
            // 남은 채로 되살아나지 않는다.
            _controller.ResetFlight();

            if (_ammo != null)
            {
                _ammo.Restock();
            }

            if (_stratagemBay != null)
            {
                _stratagemBay.ResetRestrictions();
            }

            if (_boostFuel != null)
            {
                _boostFuel.Refill();
            }

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
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
