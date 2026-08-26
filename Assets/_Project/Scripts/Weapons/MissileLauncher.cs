using System;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 미사일 발사대. 표적이 잡혀 있어야 쏜다.
    /// <para>
    /// 조준은 이 무기의 것이 아니다. 화면에 담기만 하면 걸리고, 무엇을 들고 있든
    /// 돌아간다. 그래서 이 무기가 치르는 값은 겨누고 있던 시간이 아니라 사거리와
    /// 발수다 — 멀리 있는 것은 못 쏘고, 쏠 수 있는 횟수가 적다.
    /// </para>
    /// </summary>
    public sealed class MissileLauncher : AircraftWeapon
    {
        [Header("참조")]
        [Tooltip("조준을 맡는 곳. 비워두면 기체에서 찾는다.")]
        [SerializeField] private LockOnTargeting _targeting;

        /// <summary>꽂힌 에셋을 발사기의 말로 읽는다. 종류는 장착 때 이미 검사됐다.</summary>
        private MissileDefinition Missile => (MissileDefinition)Definition;

        protected override bool Accepts(WeaponDefinition definition) => definition is MissileDefinition;

        /// <summary>
        /// 표적이 잡혀 있고, 사거리 안이어야 쏠 수 있다.
        /// <para>
        /// 거리를 여기서 보는 이유는 그것이 이 무기의 사정이기 때문이다. 조준은 화면에
        /// 보이는 한 훨씬 멀리까지 잡히는데, 잡힌다고 다 쏠 수 있으면 미사일이 화면 끝의
        /// 점까지 닿는 무기가 된다.
        /// </para>
        /// </summary>
        public override bool CanFire =>
            base.CanFire
            && _targeting != null
            && _targeting.HasLock
            && Vector3.Distance(transform.position, _targeting.TargetPoint) <= Missile.LockRange;

        /// <summary>미사일이 터졌을 때. 폭발은 발사체가 사라진 뒤에 보고된다.</summary>
        public event Action<MissileDefinition, BlastReport> Detonated;

        protected override void Awake()
        {
            base.Awake();

            if (_targeting == null && _root != null)
            {
                _targeting = _root.Find<LockOnTargeting>();
            }

            if (_targeting == null)
            {
                Debug.LogError($"{nameof(MissileLauncher)}: 조준을 맡을 컴포넌트를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        protected override void FireOnce()
        {
            Transform muzzle = ResolveMuzzle();
            Transform target = _targeting.Target;

            if (Missile.Prefab == null)
            {
                Debug.LogError($"{nameof(MissileLauncher)}: '{Missile.DisplayName}'에 프리팹이 없습니다.", this);
                return;
            }

            GameObject instance = Instantiate(
                Missile.Prefab, muzzle.position, Quaternion.LookRotation(muzzle.forward));

            if (instance.TryGetComponent(out Missile missile))
            {
                // 어떤 미사일이었는지를 지금 붙잡아 둔다. 신호가 올 때 Definition을
                // 읽으면, 날아가는 사이 다른 에셋으로 갈아입었을 때 남의 이름이 나간다.
                MissileDefinition fired = Missile;

                // 미사일은 터지면 사라지므로 화면 표시가 직접 붙을 수 없다.
                missile.Detonated += report => Detonated?.Invoke(fired, report);

                missile.Launch(
                    fired,
                    Owner,
                    target,
                    (muzzle.forward * fired.LaunchSpeed) + CarrierVelocity,
                    _hitMask);
            }
            else
            {
                Debug.LogError(
                    $"{nameof(MissileLauncher)}: '{Missile.DisplayName}'의 프리팹에 " +
                    $"{nameof(Missile)}이 없습니다.", this);
                Destroy(instance);
            }

            RaiseFired(muzzle.position, muzzle.forward);
        }
    }
}
