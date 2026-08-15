using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 기총 한 종류의 성능.
    /// <para>
    /// 지금은 무기 스탯이 기체 스탯과 분리돼 있다. 나중에 무기도 정비 대상이 되면
    /// <c>AircraftStatSheet</c>로 옮겨 부품 보정을 받게 하면 되고, 그때 이 에셋은
    /// 기본값 역할만 남는다 — 기체 스탯이 이미 그 구조다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Gun", menuName = "Adler/Weapons/Gun Definition")]
    public sealed class GunDefinition : ScriptableObject
    {
        [Header("표시")]
        public string DisplayName = "Unnamed Gun";

        [Header("발사")]
        [Tooltip("분당 발사 수.")]
        [Min(1f)]
        public float RoundsPerMinute = 900f;

        [Tooltip("탄이 닿는 최대 거리 (m). 1m 기체 기준이므로 실제 항공기보다 훨씬 짧다.")]
        [Min(1f)]
        public float Range = 300f;

        [Header("위력")]
        [Tooltip("관문을 통과했을 때 들어가는 피해량.")]
        public float Damage = 12f;

        [Tooltip("관통력. 표적의 장갑 이상이어야 피해가 들어간다.\n" +
                 "보병은 장갑이 0이라 지금은 영향이 없다.")]
        [Min(0f)]
        public float Penetration = 5f;

        [Tooltip("철거력. 건물이 요구하는 수준 이상이어야 부술 수 있다.\n" +
                 "기총은 건물을 부수지 못하므로 0이 정상이다. 철거는 폭탄의 몫이다.")]
        [Min(0f)]
        public float Demolition;

        [Header("정확도")]
        [Tooltip("탄이 흩어지는 각도. 0이면 정확히 조준선으로 나간다.")]
        [Range(0f, 10f)]
        public float SpreadDegrees = 0.6f;

        [Tooltip("탄의 판정 굵기 (m). 0보다 크면 살짝 빗나가도 맞는다.\n" +
                 "보병처럼 작은 표적을 비행 중에 맞히려면 이 관용이 필요하다.")]
        [Min(0f)]
        public float HitRadius = 0.25f;

        /// <summary>한 발과 다음 발 사이의 간격(초).</summary>
        public float ShotInterval => 60f / RoundsPerMinute;
    }
}
