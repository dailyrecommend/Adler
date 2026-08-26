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
        /// <summary>꽂힌 에셋을 기총의 말로 읽는다. 종류는 장착 때 이미 검사됐다.</summary>
        private GunDefinition Gun => (GunDefinition)Definition;

        protected override bool Accepts(WeaponDefinition definition) => definition is GunDefinition;

        protected override void FireOnce()
        {
            Transform muzzle = ResolveMuzzle();
            Vector3 origin = muzzle.position;

            // 조준 보정은 흩어짐보다 먼저 건다. 뒤에 걸면 보정이 흩어짐까지 없애버려
            // 탄이 한 점으로 모이고, 기총이 저격총처럼 굴게 된다.
            SoftLock softLock = _root != null ? _root.Find<SoftLock>() : null;
            Vector3 aim = softLock != null
                ? softLock.Adjust(origin, muzzle.forward, Gun)
                : muzzle.forward;

            Vector3 direction = ProjectileLauncher.ApplySpread(aim, Gun.SpreadDegrees);

            // 기체 속도를 얹는다. 이게 없으면 빠르게 날면서 쏠 때 탄이 뒤로 처지는
            // 것처럼 보이고, 기체가 자기 탄을 따라잡는 상황까지 나온다.
            Projectile projectile = ProjectileLauncher.Fire(
                Gun, origin, direction, CarrierVelocity, Owner, _hitMask);

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
