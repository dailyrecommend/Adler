using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 갈고리를 행동으로 부르는 문패.
    /// <para>
    /// 줄이 어떻게 날아가고 무는지는 <see cref="GrapplingHook"/>이 다 안다. 이 에셋이
    /// 더하는 것은 <b>행동 체계의 자리</b>다 — 입력은 잇는 목록의 한 줄이 되고,
    /// 쿨타임은 실행기의 장부에 오르고, "갈고리 중인가"는 꼬리표로 물을 수 있게 된다.
    /// </para>
    /// <para>
    /// 쿨타임은 던지는 순간부터 흐른다. 실행기의 규칙이 그렇고, 매달려 있는 동안
    /// 이미 줄어들므로 놓은 뒤에 남는 것은 그 나머지다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Grapple", menuName = "Adler/Abilities/Grapple")]
    public sealed class GrappleSpec : AbilitySpec
    {
        /// <inheritdoc />
        public override Ability Create() => new GrappleAbility(this);

        private void OnValidate()
        {
            // 꼬리표를 손으로 맞추게 두면 빠뜨렸을 때 증상이 조용하다 — 조준 없이
            // 던져지거나, 스스로 끝나야 할 것이 시간으로 끝난다. 이 에셋이 무엇인지는
            // 에셋 스스로가 안다.
            Tags |= AbilityTag.Sustained | AbilityTag.Movement | AbilityTag.NeedsTarget;
        }
    }
}
