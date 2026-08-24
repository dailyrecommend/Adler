namespace Adler.Abilities
{
    /// <summary>
    /// 행동 하나가 거치는 구간.
    /// <para>
    /// 구간을 나누는 이유는 "언제 끊을 수 있는가"를 시간이 아니라 구간으로 말하기
    /// 위해서다. 초 단위로 적어두면 행동의 길이를 손볼 때마다 그 숫자도 함께 고쳐야
    /// 하지만, 구간으로 적어두면 길이가 바뀌어도 규칙은 그대로다.
    /// </para>
    /// </summary>
    public enum AbilityPhase
    {
        /// <summary>아직 시작하지 않았다.</summary>
        Idle,

        /// <summary>준비 구간. 시작은 했지만 아직 효력이 없다.</summary>
        Windup,

        /// <summary>효력 구간. 이 행동이 실제로 무언가를 하는 때다.</summary>
        Active,

        /// <summary>마무리 구간. 효력은 끝났고 다음 행동으로 넘어갈 채비를 한다.</summary>
        Recovery,

        /// <summary>끝났다. 실행기가 이 구간을 보고 치운다.</summary>
        Finished,
    }
}
