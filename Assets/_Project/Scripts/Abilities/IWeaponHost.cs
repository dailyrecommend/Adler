namespace Adler.Abilities
{
    /// <summary>
    /// 행동이 무기에게 시킬 수 있는 것의 전부.
    /// <para>
    /// 무기고를 통째로 넘기지 않는다. 넘기면 행동이 탄약을 채우거나 무기를 바꿀 수도
    /// 있게 되는데, 방아쇠를 당기는 행동이 할 일이 아니다. 필요한 것만 열어두면
    /// 무엇을 할 수 있는 행동인지가 서명에 그대로 드러난다.
    /// </para>
    /// </summary>
    public interface IWeaponHost
    {
        /// <summary>지금 손에 든 무기가 쏠 수 있는 상태인지.</summary>
        bool CanFire { get; }

        /// <summary>방아쇠를 당기고 있는 동안 매 스텝 부른다.</summary>
        void HoldTrigger(float deltaTime);

        /// <summary>방아쇠를 놓는다.</summary>
        void ReleaseTrigger();

        /// <summary>실려 있는 무기들을 비율만큼 채운다. 재보급이 부른다.</summary>
        void ResupplyAll(float percent);
    }
}
