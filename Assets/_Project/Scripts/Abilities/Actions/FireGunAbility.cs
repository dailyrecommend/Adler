using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 방아쇠를 당기고 있는 동안 손에 든 무기를 쏜다.
    /// <para>
    /// 발사 간격이나 탄약은 무기가 알아서 한다. 이 행동이 하는 일은 "지금 쏘고 있다"를
    /// 이어가는 것뿐이고, 그래서 무기를 바꿔도 이 행동은 그대로다.
    /// </para>
    /// </summary>
    public sealed class FireGunAbility : Ability
    {
        public FireGunAbility(AbilitySpec spec) : base(spec) { }

        /// <summary>
        /// 손을 떼면 끝난다. 이어지는 행동이라 시간으로는 끝나지 않으므로,
        /// 끝내는 일은 이 행동을 붙잡고 있는 쪽이 놓는 것으로 이뤄진다.
        /// </summary>
        protected override void OnActive(in AbilityContext context)
        {
            if (context.Weapons == null)
            {
                Finish();
                return;
            }

            context.Weapons.HoldTrigger(context.Delta);
        }

        /// <summary>
        /// 끝날 때 방아쇠를 놓는다. 여기서 놓지 않으면 다음에 잡았을 때 밀린 간격이
        /// 한꺼번에 쏟아져 무기가 폭주한 것처럼 보인다.
        /// </summary>
        protected override void OnEnd(in AbilityContext context) => context.Weapons?.ReleaseTrigger();
    }
}
