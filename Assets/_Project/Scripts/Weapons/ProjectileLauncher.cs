using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 탄 한 발을 만들어 날려 보낸다.
    /// <para>
    /// 기체의 기총과 지상의 대공포가 같은 절차를 쓴다. 각자 복사해 두면 탄속 처리나
    /// 프리팹 검사 같은 것을 한쪽만 고치게 되고, 그러면 플레이어와 적의 탄이 조용히
    /// 다르게 동작하기 시작한다.
    /// </para>
    /// </summary>
    public static class ProjectileLauncher
    {
        /// <summary>
        /// 탄을 쏜다. 프리팹이 없거나 잘못됐으면 null.
        /// </summary>
        /// <param name="inheritedVelocity">
        /// 쏘는 쪽이 움직이고 있다면 그 속도. 고정된 포대는 <see cref="Vector3.zero"/>.
        /// </param>
        public static Projectile Fire(
            GunDefinition gun,
            Vector3 origin,
            Vector3 direction,
            Vector3 inheritedVelocity,
            GameObject owner,
            LayerMask hitMask)
        {
            if (gun == null || gun.Prefab == null)
            {
                Debug.LogError($"{nameof(ProjectileLauncher)}: 쏠 탄이 지정되지 않았습니다.", owner);
                return null;
            }

            GameObject instance = Object.Instantiate(
                gun.Prefab, origin, Quaternion.LookRotation(direction));

            if (!instance.TryGetComponent(out Projectile projectile))
            {
                Debug.LogError(
                    $"{nameof(ProjectileLauncher)}: '{gun.DisplayName}'의 탄환 프리팹에 " +
                    $"{nameof(Projectile)}이 없습니다.", owner);
                Object.Destroy(instance);
                return null;
            }

            projectile.Launch(
                gun, owner, (direction * gun.MuzzleVelocity) + inheritedVelocity, hitMask);

            return projectile;
        }

        /// <summary>조준선을 원뿔 안에서 흩뜨린다.</summary>
        public static Vector3 ApplySpread(Vector3 forward, float spreadDegrees)
        {
            if (spreadDegrees <= 0f)
            {
                return forward;
            }

            // 원 안에서 고르게 뽑는다. 두 축을 따로 흔들면 대각선이 더 벌어진다.
            Vector2 offset = Random.insideUnitCircle * spreadDegrees;
            return Quaternion.Euler(offset.y, offset.x, 0f) * forward;
        }
    }
}
