using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 응급 수리 요청. 정해진 시간 동안 목표치까지 계속 채운다.
    /// <para>
    /// 즉시 회복이면 맞기 직전에 눌러두는 것이 최선이 되어 판단이랄 게 없어진다.
    /// 시간을 두고 차오르면 <em>지금 빠져나가서 버틸 수 있는가</em>를 재게 된다 —
    /// 대공포 사거리 안에서 켜봐야 채워지는 속도보다 깎이는 속도가 빠르다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Repair", menuName = "Adler/Weapons/Repair Definition")]
    public sealed class RepairDefinition : StratagemDefinition
    {
        [Header("목표")]
        [Tooltip("이 비율을 넘지 않을 만큼만 채운다(%).\n" +
                 "닿았다고 끝나지는 않는다 — 지속 시간이 남아 있으면 다시 깎였을 때 이어서 채운다.")]
        [Range(0f, 100f)]
        public float TargetHealthPercent = 60f;

        [Header("속도")]
        [Tooltip("초당 채우는 내구도.\n" +
                 "이 값과 지속 시간을 곱한 것이 이 스킬이 채울 수 있는 최대량이다.")]
        [Min(0.1f)]
        public float RepairRate = 5f;

        [Header("지속")]
        [Tooltip("이 시간 동안 회복이 이어진다(초).\n" +
                 "그 사이 맞아서 깎인 것도 같은 창 안에서 다시 채워진다.")]
        [Min(0.1f)]
        public float Duration = 10f;

        [Tooltip("체크하면 수리 중에 맞는 순간 중단된다.\n" +
                 "켜두면 안전한 곳까지 물러나야 하므로 이탈 판단이 생기고, " +
                 "끄면 맞으면서 버티는 선택이 가능해진다.")]
        public bool CancelOnDamage;

        /// <summary>주어진 최대 내구도에서 이번 수리가 목표로 삼는 값.</summary>
        public float TargetFor(float maxHealth) => maxHealth * (TargetHealthPercent / 100f);

        /// <summary>시간을 다 써도 이만큼밖에 채우지 못한다.</summary>
        public float MaxRestored => RepairRate * Duration;

        /// <inheritdoc />
        public override Ability Create() => new RepairAbility(this, MaxRestored);
    }
}
