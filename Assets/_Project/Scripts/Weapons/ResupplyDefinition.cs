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
        [Tooltip("채워 넣을 탄 수. 0 이하면 가득 채운다.")]
        public int Rounds;

        [Header("제한")]
        [Tooltip("한 번 쓰고 나서 다시 요청할 수 있을 때까지의 시간(초).\n" +
                 "0이면 제한 없이 몇 번이든 부를 수 있다.")]
        [Min(0f)]
        public float Cooldown = 20f;

        [Tooltip("출격 한 번에 부를 수 있는 횟수. 0 이하면 무제한.")]
        public int UsesPerSortie;
    }
}
