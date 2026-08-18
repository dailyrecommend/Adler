using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 탄약 재보급 요청.
    /// <para>
    /// 폭탄과 같은 커맨드 체계를 쓴다. 탄이 떨어졌을 때 방향키를 눌러 요청해야 채워지므로,
    /// 재장전이 그냥 기다리는 시간이 아니라 <em>지금 손을 뗄 수 있는가</em>를 판단하는
    /// 순간이 된다. 적진 한가운데서 탄이 떨어지는 것이 그래서 위험하다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Resupply", menuName = "Adler/Weapons/Resupply Definition")]
    public sealed class ResupplyDefinition : StratagemDefinition
    {
        [Header("보급량")]
        [Tooltip("최대 장탄수를 기준으로 채워 넣을 비율(%).\n" +
                 "탄 수가 아니라 비율로 두는 이유는, 정비로 장탄수를 늘렸을 때 보급량도 " +
                 "함께 따라오게 하기 위해서다. 고정 수치면 큰 탄창일수록 재보급이 초라해진다.")]
        [Range(0f, 100f)]
        public float RefillPercent = 100f;

        /// <summary>주어진 최대 장탄수에서 이번 보급으로 채울 탄 수.</summary>
        public int RoundsFor(int capacity)
        {
            return Mathf.CeilToInt(capacity * (RefillPercent / 100f));
        }
    }
}
