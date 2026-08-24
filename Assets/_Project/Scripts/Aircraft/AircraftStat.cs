using System;
using UnityEngine;

namespace Adler.Aircraft
{
    /// <summary>
    /// 부품으로 바뀔 수 있는 기체 수치의 목록.
    /// <para>
    /// 부품이 "어떤 값을 건드리는가"를 데이터로 지정하려면 수치에 이름표가 필요하다.
    /// 필드로만 두면 부품마다 전용 코드를 써야 하지만, 이렇게 열거형으로 두면
    /// 부품 에셋에서 드롭다운으로 고르는 것만으로 새 부품이 만들어진다.
    /// </para>
    /// <para>
    /// 항목을 추가할 때는 <see cref="AircraftStatInfo"/>의 표에도 함께 넣어야 한다.
    /// </para>
    /// </summary>
    public enum AircraftStat
    {
        MinSpeed = 0,
        CruiseSpeed = 1,

        Acceleration = 2,
        Deceleration = 3,

        PitchRate = 4,
        RollRate = 5,

        ControlResponse = 6,
        LowSpeedAgility = 7,
        BankTurnRate = 8,

        BoostCapacity = 9,
        BoostDrain = 10,
        BoostRecharge = 11,
    }

    /// <summary>보정치를 어떻게 얹을지.</summary>
    public enum StatModifierMode
    {
        /// <summary>기본값에 그대로 더한다. 예: 최고 속도 +12m/s</summary>
        Flat = 0,

        /// <summary>비율로 곱한다. 0.15 = +15%.</summary>
        Percent = 1,
    }

    /// <summary>부품 하나가 수치 하나에 가하는 보정.</summary>
    [Serializable]
    public struct StatModifier
    {
        public AircraftStat Stat;
        public StatModifierMode Mode;
        public float Value;

        public StatModifier(AircraftStat stat, StatModifierMode mode, float value)
        {
            Stat = stat;
            Mode = mode;
            Value = value;
        }
    }

    /// <summary>
    /// 수치별 허용 범위. 부품을 겹쳐 끼우다 보면 선회율이 음수가 되거나
    /// 기동성이 1을 넘는 상황이 나오는데, 그런 값은 비행 모델을 망가뜨린다.
    /// </summary>
    public static class AircraftStatInfo
    {
        public const int Count = 12;

        // 기체 길이 1m 기준의 범위다. 속도 상한이 낮아 보이지만 1m 기체에게 50m/s는
        // 실제 전투기의 900m/s에 해당한다 — 조종이 불가능한 속도다.
        private static readonly (float Min, float Max)[] Ranges =
        {
            (1f, 40f),      // MinSpeed
            (1f, 50f),      // CruiseSpeed
            (0.5f, 60f),    // Acceleration
            (0.5f, 60f),    // Deceleration
            (1f, 360f),     // PitchRate
            (1f, 720f),     // RollRate
            (0.1f, 30f),    // ControlResponse
            (0.05f, 1f),    // LowSpeedAgility
            (0f, 180f),     // BankTurnRate
            (10f, 1000f),   // BoostCapacity
            (1f, 200f),     // BoostDrain
            (0f, 200f),     // BoostRecharge
        };

        public static float Clamp(AircraftStat stat, float value)
        {
            (float min, float max) = Ranges[(int)stat];
            return Mathf.Clamp(value, min, max);
        }
    }
}
