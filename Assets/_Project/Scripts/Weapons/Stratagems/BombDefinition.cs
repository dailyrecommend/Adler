using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 폭탄 한 종류의 성능.
    /// <para>
    /// 폭탄은 들고 다니는 무기가 아니라 매번 승인을 받아 쓰는 것이다. 강한 폭탄일수록
    /// 커맨드를 길게 만들면, 위력의 대가를 입력하는 시간으로 치르게 된다 — 비행 중에
    /// 손이 묶이는 그 몇 초가 이 무기의 진짜 비용이다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Bomb", menuName = "Adler/Weapons/Bomb Definition")]
    public sealed class BombDefinition : StratagemDefinition
    {
        [Header("투하")]
        [Tooltip("떨어뜨릴 폭탄 프리팹. Rigidbody와 Bomb 컴포넌트가 있어야 한다.")]
        public GameObject Prefab;

        [Tooltip("투하 직후 이만큼은 터지지 않는다(초).\n" +
                 "저공으로 지나가며 떨군 폭탄에 자기가 휘말리지 않게 해주는 시간이다.")]
        [Min(0f)]
        public float ArmingDelay = 0.8f;

        [Header("폭발")]
        [Tooltip("이 거리 안쪽은 피해량이 그대로 들어간다 (m).")]
        [Min(0f)]
        public float InnerRadius = 4f;

        [Tooltip("폭발이 닿는 최대 거리 (m). 안쪽 반경에서 여기까지 피해가 줄어든다.")]
        [Min(0f)]
        public float BlastRadius = 12f;

        [Tooltip("중심에서의 피해량.")]
        public float Damage = 200f;

        [Tooltip("관통력. 표적의 장갑 이상이어야 피해가 들어간다.")]
        [Min(0f)]
        public float Penetration = 30f;

        [Tooltip("철거력. 건물이 요구하는 수준 이상이어야 부술 수 있다.\n" +
                 "폭탄의 존재 이유이며, 기총으로는 대신할 수 없는 부분이다.")]
        [Min(0f)]
        public float Demolition = 50f;

        private void OnValidate()
        {
            // 안쪽 반경이 더 크면 감쇠 구간이 뒤집혀 바깥이 더 아프게 된다.
            if (InnerRadius > BlastRadius)
            {
                InnerRadius = BlastRadius;
            }
        }
    }
}
