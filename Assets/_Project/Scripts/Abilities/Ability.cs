using Adler.Core;

namespace Adler.Abilities
{
    /// <summary>
    /// 행동 하나가 도는 방식. 무엇을 하는지는 물려받는 쪽이 채운다.
    /// <para>
    /// 구간을 넘기는 일은 여기서 한 번만 짜둔다. 행동마다 스스로 시간을 재고 구간을
    /// 옮기면, 행동을 하나 더할 때마다 그 셈을 다시 짜게 되고 그중 하나가 어긋나도
    /// 밖에서는 알 수 없다. 물려받는 쪽은 "이 구간에 들어섰을 때 무엇을 하는가"만
    /// 답하면 된다.
    /// </para>
    /// <para>
    /// 시간은 받은 시계에서만 읽는다. 엔진 시간을 직접 보는 행동이 하나라도 있으면
    /// 그 행동만 시간 배율을 무시하게 되고, 그것이 왜 혼자 빠른지는 겉으로 드러나지 않는다.
    /// </para>
    /// </summary>
    public abstract class Ability
    {
        private readonly StateMachine<AbilityPhase> _phase = new(AbilityPhase.Idle);

        protected Ability(AbilitySpec spec) => Spec = spec;

        /// <summary>이 행동의 수치와 꼬리표.</summary>
        public AbilitySpec Spec { get; }

        /// <summary>지금 어느 구간인지.</summary>
        public AbilityPhase Phase => _phase.Current;

        /// <summary>이 구간에 머문 시간(초).</summary>
        public float Elapsed => _phase.Elapsed;

        /// <summary>돌고 있는 중인지.</summary>
        public bool IsRunning => Phase is not (AbilityPhase.Idle or AbilityPhase.Finished);

        /// <summary>
        /// 지금 시작할 수 있는지. 꼬리표가 요구하는 것을 갖췄는지 여기서 본다.
        /// 더 볼 것이 있으면 물려받는 쪽이 덧붙인다.
        /// </summary>
        public virtual bool CanActivate(in AbilityContext context)
        {
            if (Spec == null)
            {
                return false;
            }

            if (Spec.Has(AbilityTag.NeedsTarget) && context.Targets?.HasTarget != true)
            {
                return false;
            }

            return true;
        }

        /// <summary>실행기가 이 행동을 시작할 때 부른다.</summary>
        public void Begin(in AbilityContext context)
        {
            _phase.Restart(AbilityPhase.Windup);
            OnBegin(in context);
        }

        /// <summary>실행기가 매 프레임 부른다. 구간을 넘기고 그 구간의 일을 시킨다.</summary>
        public void Tick(in AbilityContext context)
        {
            if (!IsRunning)
            {
                return;
            }

            _phase.Advance(context.Delta);
            Advance(in context);

            switch (Phase)
            {
                case AbilityPhase.Windup:
                    OnWindup(in context);
                    break;

                case AbilityPhase.Active:
                    OnActive(in context);
                    break;

                case AbilityPhase.Recovery:
                    OnRecovery(in context);
                    break;
            }
        }

        /// <summary>밖에서 끊거나 스스로 끝날 때 부른다. 정리는 여기 한 번만 모인다.</summary>
        public void End(in AbilityContext context)
        {
            if (Phase == AbilityPhase.Idle)
            {
                return;
            }

            OnEnd(in context);
            _phase.Set(AbilityPhase.Idle);
        }

        /// <summary>이어지는 행동이 스스로 끝을 알릴 때 쓴다. 방아쇠를 놓는 것처럼.</summary>
        protected void Finish() => _phase.Set(AbilityPhase.Finished);

        /// <summary>준비 구간에 들어섰을 때부터 매 프레임.</summary>
        protected virtual void OnWindup(in AbilityContext context) { }

        /// <summary>효력 구간 동안 매 프레임. 이 행동이 실제로 하는 일이 여기 있다.</summary>
        protected abstract void OnActive(in AbilityContext context);

        /// <summary>마무리 구간 동안 매 프레임.</summary>
        protected virtual void OnRecovery(in AbilityContext context) { }

        /// <summary>시작하는 순간 한 번.</summary>
        protected virtual void OnBegin(in AbilityContext context) { }

        /// <summary>끝나는 순간 한 번. 걸어둔 것을 되돌리는 자리다.</summary>
        protected virtual void OnEnd(in AbilityContext context) { }

        /// <summary>
        /// 적어둔 시간에 따라 구간을 넘긴다.
        /// <para>
        /// 이어지는 행동은 효력 구간에 머문다 — 언제 끝날지는 시간이 아니라 손을
        /// 떼는 순간이 정하므로, 시계로 밀어내면 누르고 있는데도 저 혼자 끝난다.
        /// </para>
        /// </summary>
        private void Advance(in AbilityContext context)
        {
            switch (Phase)
            {
                case AbilityPhase.Windup when Elapsed >= Spec.WindupSeconds:
                    _phase.Set(AbilityPhase.Active);
                    break;

                case AbilityPhase.Active when !Spec.Has(AbilityTag.Sustained)
                                              && Elapsed >= Spec.ActiveSeconds:
                    _phase.Set(AbilityPhase.Recovery);
                    break;

                case AbilityPhase.Recovery when Elapsed >= Spec.RecoverySeconds:
                    _phase.Set(AbilityPhase.Finished);
                    break;
            }
        }
    }
}
