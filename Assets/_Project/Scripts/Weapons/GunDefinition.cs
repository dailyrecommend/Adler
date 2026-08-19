using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 기총 한 종류의 성능.
    /// <para>
    /// 탄이 표적을 쫓지 않으므로 맞히는 일이 온전히 조준에 달려 있다. 대신 쏘는 즉시
    /// 나가고 탄이 많다 — 미사일과 반대편에 있는 무기다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Gun", menuName = "Adler/Weapons/Gun Definition")]
    public sealed class GunDefinition : WeaponDefinition
    {
        [Header("탄도")]
        [Tooltip("탄이 총구를 떠나는 속도 (m/s). 여기에 기체 속도가 더해진다.")]
        [Min(1f)]
        public float MuzzleVelocity = 120f;

        [Tooltip("탄이 받는 중력의 비율. 0이면 직선, 1이면 온전히 떨어진다.")]
        [Range(0f, 1f)]
        public float GravityScale = 0.3f;

        [Header("정확도")]
        [Tooltip("탄이 흩어지는 각도. 0이면 정확히 조준선으로 나간다.")]
        [Range(0f, 10f)]
        public float SpreadDegrees = 0.6f;

        [Tooltip("탄의 판정 굵기 (m). 0보다 크면 살짝 빗나가도 맞는다.\n" +
                 "보병처럼 작은 표적을 비행 중에 맞히려면 이 관용이 필요하다.")]
        [Min(0f)]
        public float HitRadius = 0.25f;

        /// <summary>총구를 떠난 탄이 사거리 끝까지 가는 데 걸리는 시간(초).</summary>
        public float TimeOfFlight => Range / MuzzleVelocity;
    }
}
