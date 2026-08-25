namespace Adler.Abilities
{
    /// <summary>
    /// 행동이 무기에게 시킬 수 있는 것의 전부.
    /// <para>
    /// 한 가지뿐인 것이 맞다. 쏘는 일은 행동이 아니라 무기고가 직접 한다 — 사격에는
    /// 쿨타임도 지속시간도 출격당 횟수도 없어서, 행동으로 감싸면 그 체계가 하는 일을
    /// 모두 꺼둔 껍데기만 남는다.
    /// </para>
    /// <para>
    /// 남은 하나는 재보급이다. 이쪽은 진짜 행동이다 — 커맨드를 쳐서 부르고, 쿨타임이
    /// 있고, 출격당 횟수가 정해져 있다.
    /// </para>
    /// </summary>
    public interface IWeaponHost
    {
        /// <summary>실려 있는 무기들의 탄을 비율만큼 채운다.</summary>
        void ResupplyAll(float percent);
    }
}
