using System;
using Adler.Combat;
using Adler.Controls;
using Adler.Core;
using UnityEngine;

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
        [Tooltip("입력을 읽어오는 곳. 비워두면 이 기체에서 찾는다.")]
        [SerializeField] private PilotInput _input;

        [Tooltip("되돌아갈 자리. 비워두면 시작할 때의 위치와 자세를 기억해 쓴다.")]
        [SerializeField] private Transform _spawnPoint;

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

        [Tooltip("격추되면 키를 누르지 않아도 저절로 되돌아간다.\n" +
                 "무기 균형을 볼 때처럼 같은 상황을 반복해야 하는 동안 켜둔다.")]
        [SerializeField] private bool _autoRespawn;

        [Tooltip("저절로 되돌아가기까지 기다리는 시간(초).\n" +
                 "0이어도 한 프레임은 지난 뒤에 되살린다 — 격추 처리가 끝나기 전에 되살리면\n" +
                 "그 처리가 되살린 것을 도로 덮어써서 기체가 사라진 채로 남는다.")]
        [Min(0f)]
        [SerializeField] private float _autoRespawnDelay = 1.5f;

        // 음수면 기다리는 중이 아니다. 0은 이번 프레임에 되살린다는 뜻이라 쓸 수 없다.
        private float _autoRespawnRemaining = -1f;

        private AircraftRig _aircraft;
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        /// <summary>격추됐을 때.</summary>
        public event Action Destroyed;

        /// <summary>되돌린 직후.</summary>
        public event Action Respawned;

        private Clock _clock;

        private void Awake()
        {
            _input = _input != null ? _input : GetComponent<PilotInput>();
            _clock = TimeScale.For(this);
            _aircraft = GetComponent<AircraftRig>();

            _startPosition = transform.position;
            _startRotation = transform.rotation;
        }

        private void OnEnable()
        {
            if (_aircraft.Health != null)
            {
                _aircraft.Health.Died += OnDied;
            }

        }

        private void OnDisable()
        {
            if (_aircraft.Health != null)
            {
                _aircraft.Health.Died -= OnDied;
            }

        }

        private void Update()
        {
            UpdateAutoRespawn();

            if (_input == null || !_input.RespawnPressed)
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
            ClearStates();
            Destroyed?.Invoke();

            if (_autoRespawn)
            {
                _autoRespawnRemaining = _autoRespawnDelay;
            }

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

        /// <summary>
        /// 걸려 있던 상태를 죽는 즉시 전부 놓는다. 줄·커맨드 창·디버프·돌던 행동.
        /// <para>
        /// 리스폰까지 기다리지 않는다. 컴포넌트를 끄는 것은 앞으로의 입력을 막을 뿐
        /// 이미 걸린 것은 못 푼다 — 잔해에 줄이 매달려 있고, 커맨드 창이 열린 채
        /// 남고, 되살아난 기체가 죽기 전의 창과 디버프를 물려받는다.
        /// </para>
        /// </summary>
        private void ClearStates()
        {
            _aircraft.Grapple?.Release();
            _aircraft.Stratagems?.SetCommandMode(false);
            _aircraft.Debuffs?.Clear();

            // 돌던 행동은 여기서도 멈춘다. 리스폰의 Stop은 죽은 채 기다리는 동안
            // 프레임락 같은 것이 계속 돌지 않게 하는 이 호출의 뒷단속이다.
            _aircraft.Abilities?.Stop();
        }

        // ------------------------------------------------------------------
        // 리스폰
        // ------------------------------------------------------------------

        /// <summary>
        /// 저절로 되돌아가기를 기다린다.
        /// <para>
        /// 격추된 그 자리에서 되살리지 않고 다음 프레임까지 미룬다. <see cref="Health"/>는
        /// 죽었다고 알린 <b>뒤에</b> 오브젝트를 끄므로, 알림 안에서 되살리면 그 처리가
        /// 되살린 것을 도로 덮어써 기체가 사라진 채로 남는다.
        /// </para>
        /// </summary>
        private void UpdateAutoRespawn()
        {
            if (_autoRespawnRemaining < 0f)
            {
                return;
            }

            _autoRespawnRemaining -= _clock.Delta;

            if (_autoRespawnRemaining <= 0f)
            {
                _autoRespawnRemaining = -1f;
                Respawn();
            }
        }

        /// <summary>밖에서도 부를 수 있다. 임무 실패 화면 같은 데서 쓴다.</summary>
        public void Respawn()
        {
            // 기다리던 것이 있으면 접는다. 키를 눌러 먼저 돌아갔는데 뒤늦게 한 번 더
            // 되살아나면, 그 사이에 날던 기체가 출발 지점으로 끌려간다.
            _autoRespawnRemaining = -1f;

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

            // 죽기 전에 걸어둔 행동이 남아 있으면 되살아난 기체에 그대로 이어지고,
            // 지난 출격의 쿨타임과 횟수가 남아 있으면 새 출격이 빚을 안고 시작한다.
            _aircraft.Abilities?.Stop();
            _aircraft.Abilities?.ResetSortie();

            // 연출은 이 신호를 듣는 쪽이 알아서 한다. 여기서 하나씩 불러주면 연출을
            // 붙일 때마다 기체가 그것을 알아야 해서, 목록이 계속 늘어난다.
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
