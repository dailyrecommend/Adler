using Adler.Abilities;
using System;
using Adler.Combat;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>기체가 놓일 수 있는 상태 하나.</summary>
    public enum AircraftCondition
    {
        /// <summary>부스터를 쓰는 동안.</summary>
        Boosting,

        /// <summary>방아쇠를 당기고 있는 동안.</summary>
        Firing,

        /// <summary>정해둔 디버프가 걸려 있는 동안.</summary>
        Debuff,

        /// <summary>수리가 도는 동안.</summary>
        Repairing,

        /// <summary>얼어붙어 있는 동안.</summary>
        Frozen,

        /// <summary>시간을 건드리는 것이 도는 동안.</summary>
        TimeWarped,

        /// <summary>갈고리가 무언가에 걸려 있는 동안. 물어 들어간 뒤부터 끌리는 내내.</summary>
        Grappling,

        /// <summary>
        /// 방금 몸으로 들이받았을 때. 부딪힌 뒤 잠깐 참으로 남는다.
        /// <para>
        /// 부딪히는 것은 한 순간이라 이어지는 상태가 없다. 잔상처럼 켜져 있어야 하는
        /// 연출이 붙잡을 것이 필요해서, 들이받기 쪽이 짧은 창을 남겨준다.
        /// </para>
        /// </summary>
        JustRammed,
    }

    /// <summary>
    /// 기체의 상태를 묻는 자리.
    /// <para>
    /// 연출마다 조건을 다시 짜지 않게 한곳에 모아둔다. 화면 효과와 이펙트는 켜는
    /// 방식이 서로 다르지만 <b>언제 켜는가</b>는 같은 질문이라, 그 답을 각자 갖고
    /// 있으면 조건을 하나 더할 때 두 곳을 함께 고쳐야 하고 그중 하나를 빠뜨려도
    /// 겉으로는 드러나지 않는다.
    /// </para>
    /// </summary>
    public static class AircraftConditions
    {
        /// <summary>
        /// 이 조건이 지금 참인지.
        /// <para>
        /// 물어볼 곳이 없으면 거짓으로 본다. 수리가 없는 기체에 수리 연출을 걸어두는
        /// 일은 흔하고, 그때 켜진 채로 남으면 원인을 짚기 어렵다.
        /// </para>
        /// </summary>
        public static bool IsMet(AircraftRig aircraft, AircraftCondition condition, DebuffDefinition debuff)
        {
            if (aircraft == null)
            {
                return false;
            }

            return condition switch
            {
                AircraftCondition.Boosting => aircraft.Model?.IsBoosting == true,
                AircraftCondition.Firing => aircraft.Weapons?.IsFiring == true,
                AircraftCondition.Frozen => aircraft.Model?.IsFrozen == true,
                AircraftCondition.Repairing => aircraft.Abilities?.IsRunning(AbilityTag.Repair) == true,
                AircraftCondition.TimeWarped => aircraft.Abilities?.IsRunning(AbilityTag.TimeWarp) == true,
                AircraftCondition.Grappling => aircraft.Grapple?.IsAttached == true,
                AircraftCondition.JustRammed => aircraft.Ram?.IsRamming == true,
                AircraftCondition.Debuff => debuff != null && aircraft.Debuffs?.IsActive(debuff) == true,
                _ => false,
            };
        }
    }
}
