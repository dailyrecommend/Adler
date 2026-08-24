namespace Adler.Abilities
{
    /// <summary>
    /// 실려 있는 무기를 비율만큼 채운다.
    /// <para>
    /// 한순간에 끝난다. 채우는 데 시간을 들일 이유가 없는 것은, 이 요청의 값이 이미
    /// 커맨드를 치는 몇 초로 치러졌기 때문이다.
    /// </para>
    /// </summary>
    public sealed class ResupplyAbility : Ability
    {
        private readonly float _percent;

        public ResupplyAbility(AbilitySpec spec, float percent) : base(spec) => _percent = percent;

        protected override void OnActive(in AbilityContext context)
        {
            context.Weapons?.ResupplyAll(_percent);
            Finish();
        }
    }
}
