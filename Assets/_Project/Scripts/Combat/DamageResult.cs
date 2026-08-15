namespace Adler.Combat
{
    /// <summary>
    /// 한 발이 실제로 무슨 일을 했는지.
    /// <para>
    /// 쏜 쪽이 결과를 알아야 하기 때문에 존재한다. 표적만 알고 있으면 화면에 무엇을
    /// 띄울지 정할 수가 없다 — 맞혔는데 튕겨 나갔다는 사실은 표적의 사정이 아니라
    /// 플레이어에게 무기를 바꾸라고 알려주는 신호다.
    /// </para>
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary>막혔다면 어느 관문이었는지. <see cref="DamageRejection.None"/>이면 통과했다.</summary>
        public readonly DamageRejection Rejection;

        /// <summary>실제로 깎인 내구도.</summary>
        public readonly float Applied;

        /// <summary>이 한 발로 표적이 쓰러졌는지.</summary>
        public readonly bool Killed;

        public DamageResult(DamageRejection rejection, float applied, bool killed)
        {
            Rejection = rejection;
            Applied = applied;
            Killed = killed;
        }

        /// <summary>맞혔지만 통하지 않았다.</summary>
        public bool Blocked => Rejection != DamageRejection.None;

        /// <summary>피해가 들어갔다.</summary>
        public bool Landed => Rejection == DamageRejection.None;

        /// <summary>이미 쓰러진 표적을 때렸을 때처럼, 아무 일도 없었던 경우.</summary>
        public static DamageResult None => default;
    }
}
