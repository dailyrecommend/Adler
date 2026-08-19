using System;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 미사일 발사대. 조준이 완성돼야 쏜다.
    /// <para>
    /// 손에 들려 있는 동안에만 조준이 돌아간다. 무기를 바꾸면 조준도 함께 풀려서,
    /// 기총을 쥔 채 조준을 쌓아두었다가 바꿔서 곧바로 쏘는 일이 생기지 않는다.
    /// 겨누고 있던 시간이 이 무기의 값이기 때문이다.
    /// </para>
    /// </summary>
    public sealed class MissileLauncher : AircraftWeapon
    {
        [Header("무기")]
        [SerializeField] private MissileDefinition _missile;

        [Tooltip("조준을 맡는 곳. 비워두면 기체에서 찾는다.")]
        [SerializeField] private LockOnTargeting _targeting;

        public override WeaponDefinition Definition => _missile;

        /// <summary>조준이 걸려야 쏠 수 있다.</summary>
        public override bool CanFire => base.CanFire && _targeting != null && _targeting.HasLock;

        /// <summary>미사일이 터졌을 때. 폭발은 발사체가 사라진 뒤에 보고된다.</summary>
        public event Action<MissileDefinition, BlastReport> Detonated;

        protected override void Awake()
        {
            base.Awake();

            if (_targeting == null && _aircraft != null)
            {
                _targeting = _aircraft.GetComponentInChildren<LockOnTargeting>(includeInactive: true);
            }

            if (_targeting == null)
            {
                Debug.LogError($"{nameof(MissileLauncher)}: 조준을 맡을 컴포넌트를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        public override void OnDrawn() => _targeting.SetRules(_missile);

        public override void OnStowed()
        {
            base.OnStowed();
            _targeting.SetRules(null);
        }

        protected override void FireOnce()
        {
            Transform muzzle = ResolveMuzzle();
            Transform target = _targeting.Target;

            if (_missile.Prefab == null)
            {
                Debug.LogError($"{nameof(MissileLauncher)}: '{_missile.DisplayName}'에 프리팹이 없습니다.", this);
                return;
            }

            GameObject instance = Instantiate(
                _missile.Prefab, muzzle.position, Quaternion.LookRotation(muzzle.forward));

            if (instance.TryGetComponent(out Missile missile))
            {
                // 미사일은 터지면 사라지므로 화면 표시가 직접 붙을 수 없다.
                missile.Detonated += report => Detonated?.Invoke(_missile, report);

                missile.Launch(
                    _missile,
                    Owner,
                    target,
                    (muzzle.forward * _missile.LaunchSpeed) + CarrierVelocity,
                    _hitMask);
            }
            else
            {
                Debug.LogError(
                    $"{nameof(MissileLauncher)}: '{_missile.DisplayName}'의 프리팹에 " +
                    $"{nameof(Missile)}이 없습니다.", this);
                Destroy(instance);
            }

            RaiseFired(muzzle.position, muzzle.forward);
        }
    }
}
