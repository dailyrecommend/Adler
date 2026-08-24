using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 잠깐 적들의 시간을 세운다. 쓴 사람은 그대로 움직인다.
    /// <para>
    /// 멈추는 시간은 <see cref="AbilitySpec.ActiveSeconds"/>로 정한다. 여기 따로 두지
    /// 않는 이유는 그것이 곧 이 행동이 효력을 갖는 구간이기 때문이다 — 두 곳에 적어두면
    /// 언젠가 서로 다른 값이 된다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "TimeStop", menuName = "Adler/Stratagems/Time Stop Definition")]
    public sealed class TimeStopDefinition : StratagemDefinition
    {
        [Header("정지")]
        [Tooltip("붙잡힌 것들이 흐르는 속도.\n\n" +
                 "0이면 완전히 선다. 세상 시계는 그대로 두고 대상만 늦추므로 0이 안전하다.\n\n" +
                 "0.05쯤 남겨두면 아주 느리게 기어가는 것이 보인다. 완전히 세우는 것보다\n" +
                 "읽기 쉬울 때가 있다 — 멎어 있으면 화면이 멈춘 것인지 게임이 멈춘 것인지\n" +
                 "구분되지 않는다.")]
        [Range(0f, 0.5f)]
        public float StopScale;

        /// <inheritdoc />
        public override Ability Create() => new TimeStopAbility(this);

        private void OnValidate()
        {
            // 꼬리표를 손으로 맞추게 두면 빠뜨렸을 때 증상이 "화면 효과가 안 켜진다"라
            // 원인을 엉뚱한 데서 찾게 된다. 이 자산이 무엇인지는 자산 스스로가 안다.
            Tags |= AbilityTag.Stratagem | AbilityTag.TimeWarp;
        }
    }
}
