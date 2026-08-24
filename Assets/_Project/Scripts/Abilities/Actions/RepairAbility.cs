using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 정해진 시간에 걸쳐 내구도를 채운다.
    /// <para>
    /// 재보급과 달리 지속된다. 그래서 채우는 동안 격추될 수도 있고, 그 사이에 다시
    /// 요청하면 앞의 것을 대신한다 — 남은 분량을 이어 붙이면 언제 끝나는지 알 수
    /// 없게 되고, 화면에 띄울 진행도도 정의할 수 없다.
    /// </para>
    /// </summary>
    public sealed class RepairAbility : Ability
    {
        private readonly float _amount;

        public RepairAbility(AbilitySpec spec, float amount) : base(spec) => _amount = amount;

        /// <summary>이 수리가 얼마나 진행됐는지. 0에서 1까지. 화면 표시가 읽어 간다.</summary>
        public float Progress => Spec.ActiveSeconds > 0f
            ? Mathf.Clamp01(Elapsed / Spec.ActiveSeconds)
            : 1f;

        /// <summary>가득 찬 기체는 고칠 것이 없다. 쿨타임만 버리게 된다.</summary>
        public override bool CanActivate(in AbilityContext context)
            => base.CanActivate(in context) && context.Durability?.IsFull == false;

        /// <summary>
        /// 흐른 만큼 나눠 채운다. 한 번에 채우면 지속되는 뜻이 없고, 채우는 동안
        /// 맞으면 그만큼 다시 깎이는 줄다리기가 이 스트라타젬의 성격이다.
        /// </summary>
        protected override void OnActive(in AbilityContext context)
        {
            if (context.Durability == null || Spec.ActiveSeconds <= 0f)
            {
                context.Durability?.Restore(_amount);
                Finish();
                return;
            }

            context.Durability.Restore(_amount * (context.Delta / Spec.ActiveSeconds));
        }
    }
}
