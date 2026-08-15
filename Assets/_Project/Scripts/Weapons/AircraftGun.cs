using System;
using Adler.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Weapons
{
    /// <summary>
    /// 기체에 달린 기총. 방아쇠를 당기는 동안 일정 간격으로 탄을 뿌린다.
    /// <para>
    /// 탄을 날리지 않고 즉시 판정한다(히트스캔). 캐주얼한 조작을 원하므로 편차를 계산해
    /// 앞을 겨냥하는 부담을 주지 않는 편이 맞고, 명중 판정도 확실해진다. 날아가는 탄의
    /// 모양새는 예광탄 연출이 대신한다.
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

        [Header("판정")]
        [Tooltip("탄이 맞을 레이어. 기체 자신이 속한 레이어는 빼야 자기 총에 맞지 않는다.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private InputAction _fireAction;
        private float _cooldown;
        private int _nextMuzzle;

        /// <summary>발사할 때마다. 예광탄과 총구 화염이 구독한다. (시작점, 끝점)</summary>
        public event Action<Vector3, Vector3> Fired;

        /// <summary>무언가를 맞혔을 때. 피격 효과가 구독한다.</summary>
        public event Action<RaycastHit, IDamageable> Hit;

        private void Awake()
        {
            if (_gun == null)
            {
                Debug.LogError($"{nameof(AircraftGun)}: Gun Definition이 비어 있습니다.", this);
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

            Vector3 end = origin + (direction * _gun.Range);
            IDamageable damaged = null;

            if (TryHit(origin, direction, out RaycastHit hit))
            {
                end = hit.point;

                // 콜라이더가 자식에 있어도 본체의 Health를 찾아야 한다.
                damaged = hit.collider.GetComponentInParent<IDamageable>();
                if (damaged != null && damaged.IsAlive)
                {
                    damaged.TakeDamage(new DamageInfo(
                        _gun.Damage, _gun.Penetration, _gun.Demolition,
                        hit.point, hit.normal, gameObject));
                }

                Hit?.Invoke(hit, damaged);
            }

            Fired?.Invoke(origin, end);
        }

        private bool TryHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            // 굵기가 있으면 살짝 빗나가도 맞는다. 보병처럼 작은 표적을 비행 중에
            // 맞히려면 이 관용이 있어야 캐주얼한 사격이 된다.
            if (_gun.HitRadius > 0f)
            {
                return Physics.SphereCast(
                    origin, _gun.HitRadius, direction, out hit,
                    _gun.Range, _hitMask, QueryTriggerInteraction.Ignore);
            }

            return Physics.Raycast(
                origin, direction, out hit,
                _gun.Range, _hitMask, QueryTriggerInteraction.Ignore);
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
