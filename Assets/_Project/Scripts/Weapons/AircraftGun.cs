using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 기체에 달린 기총. 방아쇠를 당기는 동안 탄을 뿌린다.
    /// <para>
    /// 탄은 실제로 날아간다. 그래서 움직이는 표적은 앞을 겨냥해야 맞고, 명중은 쏜 순간이
    /// 아니라 탄이 도달한 뒤에 정해진다. 탄의 사정은 <see cref="Projectile"/>이 맡는다.
    /// </para>
    /// </summary>
    public sealed class AircraftGun : AircraftWeapon
    {
        [Header("무기")]
        [SerializeField] private GunDefinition _gun;

        public override WeaponDefinition Definition => _gun;

        protected override void FireOnce()
        {
            Transform muzzle = ResolveMuzzle();
            Vector3 origin = muzzle.position;
            Vector3 direction = ProjectileLauncher.ApplySpread(muzzle.forward, _gun.SpreadDegrees);

            // 기체 속도를 얹는다. 이게 없으면 빠르게 날면서 쏠 때 탄이 뒤로 처지는
            // 것처럼 보이고, 기체가 자기 탄을 따라잡는 상황까지 나온다.
            Projectile projectile = ProjectileLauncher.Fire(
                _gun, origin, direction, CarrierVelocity, Owner, _hitMask);

            if (projectile != null)
            {
                // 탄은 맞는 순간 사라지므로 화면 표시가 직접 붙을 수 없다.
                // 총이 대신 받아 자기 신호로 다시 내보낸다.
                projectile.Struck += (hit, damaged, result) => RaiseHit(hit, damaged, result);
            }

            RaiseFired(origin, direction);
        }
    }
}
