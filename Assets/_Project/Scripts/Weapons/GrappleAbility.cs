using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 갈고리를 행동 체계 위에서 굴린다.
    /// <para>
    /// 얇은 것이 맞다. 줄의 물리와 단계는 <see cref="GrapplingHook"/>의 것이고, 여기는
    /// 행동의 언어 — 시작·지속·끝 — 를 그쪽의 언어 — 던짐·매달림·놓음 — 로 옮길
    /// 뿐이다. 여기에 줄의 사정이 스며들면 같은 규칙이 두 파일로 갈라진다.
    /// </para>
    /// <para>
    /// 줄은 스스로 끊어지기도 한다. 다 감기거나, 너무 멀어지거나, 시간이 다 되거나.
    /// 그래서 이쪽이 끝내는 길과 저쪽이 끝나는 길이 둘 다 있고, 어느 쪽이든 결과가
    /// 같아야 한다 — 매 틱 줄이 살아 있는지 보고, 죽었으면 행동도 끝낸다.
    /// </para>
    /// </summary>
    public sealed class GrappleAbility : Ability
    {
        private GrapplingHook _hook;

        public GrappleAbility(GrappleSpec spec) : base(spec) { }

        protected override void OnBegin(in AbilityContext context)
        {
            // 만들어질 때가 아니라 시작할 때 찾는다. 스펙 에셋은 씬을 모르므로,
            // 씬의 부품은 문맥이 가리키는 기체에서 그때그때 얻는 수밖에 없다.
            if (_hook == null && context.Owner != null)
            {
                _hook = context.Owner.GetComponentInChildren<GrapplingHook>(includeInactive: true);
            }

            // 조준은 문턱에서 확인했지만 그 사이에 표적이 사라졌을 수 있다.
            // 던져지지 않았으면 행동도 없던 일이다 — 잡고 있으면 줄 없는 갈고리가
            // 영영 돌게 된다.
            if (_hook == null || !_hook.Fire())
            {
                Finish();
            }
        }

        /// <summary>줄이 스스로 끊어졌으면 행동도 따라 끝난다.</summary>
        protected override void OnActive(in AbilityContext context)
        {
            if (_hook == null || _hook.Phase == GrapplePhase.Idle)
            {
                Finish();
            }
        }

        /// <summary>
        /// 어느 길로 끝나든 줄은 놓는다. 이미 놓였으면 저쪽이 알아서 무시한다.
        /// </summary>
        protected override void OnEnd(in AbilityContext context) => _hook?.Release();
    }
}
