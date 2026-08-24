using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 응급 수리 요청. 정해진 시간에 걸쳐 나눠 채운다.
    /// <para>
    /// 즉시 회복이면 맞기 직전에 눌러두는 것이 최선이 되어 판단이랄 게 없어진다.
    /// 시간을 두고 차오르면 <em>지금 빠져나가서 버틸 수 있는가</em>를 재게 된다 —
    /// 대공포 사거리 안에서 켜봐야 채워지는 속도보다 깎이는 속도가 빠르다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Repair", menuName = "Adler/Weapons/Repair Definition")]
    public sealed class RepairDefinition : StratagemDefinition
    {
        [Header("속도")]
        [Tooltip("초당 채우는 내구도.\n" +
                 "이 값과 Active Seconds를 곱한 것이 이 스킬이 채울 수 있는 최대량이다.")]
        [Min(0.1f)]
        public float RepairRate = 5f;

        /// <summary>
        /// 시간을 다 써도 이만큼밖에 채우지 못한다.
        /// <para>
        /// 길이는 <see cref="AbilitySpec.ActiveSeconds"/>에서 가져온다. 따로 Duration을
        /// 두고 있었는데, 채우는 쪽은 ActiveSeconds로 나누고 총량은 Duration으로 곱해서
        /// 둘이 어긋나는 순간 채워지는 양이 조용히 틀어졌다.
        /// </para>
        /// </summary>
        public float MaxRestored => RepairRate * ActiveSeconds;

        /// <inheritdoc />
        public override Ability Create() => new RepairAbility(this, MaxRestored);
    }
}
