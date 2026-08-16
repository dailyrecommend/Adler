using System;
using Adler.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Weapons
{
    /// <summary>
    /// 기체에 달린 기총. 방아쇠를 당기는 동안 일정 간격으로 탄을 뿌린다.
    /// <para>
    /// 탄은 실제로 날아간다. 그래서 움직이는 표적은 앞을 겨냥해야 맞고, 명중은 쏜 순간이
    /// 아니라 탄이 도달한 뒤에 정해진다. 탄의 사정은 <see cref="Projectile"/>이 맡고,
    /// 이 컴포넌트는 방아쇠와 발사 간격만 다룬다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftGun : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;
        [SerializeField] private GunDefinition _gun;

        [Tooltip("총구 위치. 여러 개면 번갈아 발사한다. 비어 있으면 이 오브젝트에서 나간다.")]
        [SerializeField] private Transform[] _muzzles = Array.Empty<Transform>();

        [Tooltip("탄에 속도를 물려줄 기체. 비워두면 부모에서 찾는다.")]
        [SerializeField] private Rigidbody _carrier;

        [Header("판정")]
        [Tooltip("탄이 맞을 레이어. 기체 자신이 속한 레이어는 빼야 자기 총에 맞지 않는다.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private InputAction _fireAction;
        private float _cooldown;
        private int _nextMuzzle;

        /// <summary>발사할 때마다. 총구 화염과 소리가 구독한다. (총구 위치, 발사 방향)</summary>
        public event Action<Vector3, Vector3> Fired;

        /// <summary>
        /// 무언가를 맞혔을 때. 피격 효과와 화면 표시가 구독한다.
        /// <see cref="DamageResult"/>는 표적을 맞혔을 때만 의미가 있다 — 지형에 맞으면
        /// <see cref="IDamageable"/>이 null이고 결과는 비어 있다.
        /// </summary>
        public event Action<RaycastHit, IDamageable, DamageResult> Hit;

        private void Awake()
        {
            if (_carrier == null)
            {
                _carrier = GetComponentInParent<Rigidbody>();
            }

            if (_gun == null)
            {
                Debug.LogError($"{nameof(AircraftGun)}: Gun Definition이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_gun.Prefab == null)
            {
                Debug.LogError($"{nameof(AircraftGun)}: '{_gun.DisplayName}'에 탄환 프리팹이 없습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                Debug.LogError($"{nameof(AircraftGun)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _fireAction = _controls.FindActionMap("Flight", throwIfNotFound: true)
                                   .FindAction("Fire", throwIfNotFound: true);
            _fireAction.Enable();
            _cooldown = 0f;
        }

        private void OnDisable() => _fireAction?.Disable();

        /// <summary>
        /// 물리 주기가 아니라 프레임마다 돈다. 방아쇠 반응이 늦으면 곧바로 체감되고,
        /// 기체 위치는 보간된 값을 읽으므로 총구도 화면에 보이는 자리에서 나간다.
        /// </summary>
        private void Update()
        {
            _cooldown -= Time.deltaTime;

            if (!_fireAction.IsPressed())
            {
                // 방아쇠를 놓았을 때 남은 시간을 지워, 짧게 끊어 쏴도 첫 발이 즉시 나가게 한다.
                _cooldown = Mathf.Min(_cooldown, 0f);
                return;
            }

            // 프레임이 길어지면 그 사이 나갔어야 할 탄이 여러 발이다. 다만 프레임 하락이
            // 연쇄로 더 큰 부하를 부르지 않도록 한 프레임에 쏘는 수를 제한한다.
            const int MaxShotsPerFrame = 4;
            int shots = 0;

            while (_cooldown <= 0f && shots < MaxShotsPerFrame)
            {
                FireOnce();
                _cooldown += _gun.ShotInterval;
                shots++;
            }

            if (_cooldown < 0f)
            {
                _cooldown = 0f;
            }
        }

        private void FireOnce()
        {
            Transform muzzle = ResolveMuzzle();
            Vector3 origin = muzzle.position;
            Vector3 direction = ApplySpread(muzzle.forward);

            // 기체 속도를 얹는다. 이게 없으면 빠르게 날면서 쏠 때 탄이 뒤로 처지는
            // 것처럼 보이고, 기체가 자기 탄을 따라잡는 상황까지 나온다.
            Vector3 velocity = (direction * _gun.MuzzleVelocity)
                               + (_carrier != null ? _carrier.linearVelocity : Vector3.zero);

            GameObject instance = Instantiate(_gun.Prefab, origin, Quaternion.LookRotation(direction));

            if (instance.TryGetComponent(out Projectile projectile))
            {
                // 탄은 맞는 순간 사라지므로 화면 표시가 직접 붙을 수 없다.
                // 총이 대신 받아 자기 신호로 다시 내보낸다.
                projectile.Struck += (hit, damaged, result) => Hit?.Invoke(hit, damaged, result);
                projectile.Launch(_gun, _carrier != null ? _carrier.gameObject : gameObject,
                    velocity, _hitMask);
            }
            else
            {
                Debug.LogError(
                    $"{nameof(AircraftGun)}: '{_gun.DisplayName}'의 탄환 프리팹에 " +
                    $"{nameof(Projectile)}이 없습니다.", this);
                Destroy(instance);
            }

            Fired?.Invoke(origin, direction);
        }

        private Transform ResolveMuzzle()
        {
            if (_muzzles.Length == 0)
            {
                return transform;
            }

            Transform muzzle = _muzzles[_nextMuzzle];
            _nextMuzzle = (_nextMuzzle + 1) % _muzzles.Length;
            return muzzle != null ? muzzle : transform;
        }

        private Vector3 ApplySpread(Vector3 forward)
        {
            if (_gun.SpreadDegrees <= 0f)
            {
                return forward;
            }

            // 원뿔 안에서 고르게 뽑는다. 각도를 두 축에 따로 흔들면 대각선이 더 벌어진다.
            Vector2 offset = UnityEngine.Random.insideUnitCircle * _gun.SpreadDegrees;
            return Quaternion.Euler(offset.y, offset.x, 0f) * forward;
        }
    }
}
