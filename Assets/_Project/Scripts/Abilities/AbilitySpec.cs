using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 행동 하나의 수치와 성격. 무엇을 하는지가 아니라 <b>어떤 것인지</b>를 적는다.
    /// <para>
    /// 행동을 클래스 하나로만 두면 수치를 손볼 때마다 컴파일을 기다려야 하고, 스무 개가
    /// 넘어가는 순간 어느 것이 무엇인지 코드를 열어야만 알 수 있다. 도는 방식만 코드에
    /// 두고 나머지를 에셋으로 빼면, 새 행동을 만드는 일이 대개 <b>에셋 하나 만들기</b>가 된다.
    /// </para>
    /// <para>
    /// 도는 방식까지 새로 짜야 하는 행동만 <see cref="Ability"/>를 물려받고, 그 종류를
    /// 여기에 지정한다. 같은 방식에 수치만 다른 행동은 에셋만 늘리면 된다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "Adler/Abilities/Ability Spec")]
    public class AbilitySpec : ScriptableObject
    {
        [Header("이름")]
        public string DisplayName = "Unnamed Ability";

        [TextArea]
        public string Description;

        [Header("성격")]
        [Tooltip("이 행동이 어떤 것인지. 전이 규칙이 이름 대신 이것을 보고 판단한다.")]
        public AbilityTag Tags = AbilityTag.Instant;

        [Header("구간 (초)")]
        [Tooltip("시작한 뒤 효력이 생기기까지. 0이면 곧바로 듣는다.")]
        [Min(0f)]
        public float WindupSeconds;

        [Tooltip("효력이 이어지는 시간.\n" +
                 "Sustained 꼬리표가 붙어 있으면 무시된다 — 그쪽은 손을 뗄 때 끝난다.")]
        [Min(0f)]
        public float ActiveSeconds = 0.1f;

        [Tooltip("효력이 끝난 뒤 다음 행동으로 넘어가기까지.\n" +
                 "0으로 두면 다음 행동이 곧바로 이어져 무게가 느껴지지 않는다.")]
        [Min(0f)]
        public float RecoverySeconds;

        [Header("재사용")]
        [Tooltip("끝난 뒤 다시 쓸 수 있을 때까지의 시간(초).")]
        [Min(0f)]
        public float Cooldown;

        /// <summary>이 꼬리표가 붙어 있는지.</summary>
        public bool Has(AbilityTag tag) => (Tags & tag) != 0;

        /// <summary>
        /// 이 수치로 도는 행동 하나를 만든다.
        /// <para>
        /// 만드는 일을 데이터 쪽에 둔 이유는, 그러지 않으면 실행기가 종류마다 분기를
        /// 갖게 되기 때문이다. 여기서 만들면 새 종류가 생겨도 실행기는 그대로다.
        /// </para>
        /// </summary>
        public virtual Ability Create() => null;
    }
}
