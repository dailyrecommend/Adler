using UnityEngine;

namespace Adler.Aircraft
{
    /// <summary>
    /// 기체 한 종류의 소재 성능. 정비로 바뀌기 전의 값이며, 런타임에 절대 수정되지 않는다.
    /// <para>
    /// 실제 비행에 쓰이는 값은 이 에셋이 아니라 <see cref="AircraftStatSheet"/>가 계산한다.
    /// 여기 값을 직접 고치면 그 기체를 쓰는 모든 개체가 영향을 받고, 플레이 모드에서
    /// 바꾼 값이 그대로 저장되므로 정비 결과를 여기에 쓰면 안 된다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Airframe", menuName = "Adler/Aircraft/Airframe Definition")]
    public sealed class AirframeDefinition : ScriptableObject
    {
        [Header("표시")]
        public string DisplayName = "Unnamed Airframe";

        [Header("속도 (m/s)")]
        // 기본값은 기체 길이 1m 기준이다. 체감 속도는 초당 지나가는 기체 길이 수로
        // 결정되므로, 기체 크기를 바꾸면 이 값들도 같은 비율로 바꿔야 감각이 유지된다.
        [Tooltip("스로틀을 완전히 당겼을 때의 속도. 이 아래로는 떨어지지 않는다.")]
        public float MinSpeed = 6f;

        [Tooltip("기동성이 100%가 되는 기준 속도.")]
        public float CruiseSpeed = 12f;

        [Tooltip("스로틀 최대일 때의 속도.")]
        public float MaxSpeed = 20f;

        [Tooltip("부스터 사용 시 도달하는 속도.")]
        public float BoostSpeed = 32f;

        [Header("가감속")]
        public float Acceleration = 8f;
        public float Deceleration = 6f;

        [Tooltip("스로틀 레버가 0에서 1까지 움직이는 속도.")]
        public float ThrottleResponse = 0.8f;

        [Header("선회 속도 (도/초)")]
        public float PitchRate = 70f;
        public float RollRate = 180f;
        public float YawRate = 40f;

        [Header("조종 감각")]
        [Tooltip("입력이 최대치에 도달하는 속도. 낮을수록 기체가 묵직해진다.")]
        public float ControlResponse = 6f;

        [Tooltip("MinSpeed에서의 기동성 배율. 저속에서 둔해지는 정도.")]
        [Range(0.05f, 1f)]
        public float LowSpeedAgility = 0.7f;

        [Tooltip("기울이면 러더 입력 없이도 그쪽으로 선회하는 정도 (도/초).")]
        public float BankTurnRate = 55f;

        [Header("고정 특성 (부품으로 바뀌지 않음)")]
        [Tooltip("기수가 하늘을 향할 때 속도가 깎이고, 강하할 때 붙는 정도.")]
        [Range(0f, 1f)]
        public float GravityInfluence = 0.15f;

        /// <summary>기본 수치를 스탯 배열에 옮겨 적는다. <see cref="AircraftStatSheet"/>가 호출한다.</summary>
        internal void WriteBaseValues(float[] values)
        {
            values[(int)AircraftStat.MinSpeed] = MinSpeed;
            values[(int)AircraftStat.CruiseSpeed] = CruiseSpeed;
            values[(int)AircraftStat.MaxSpeed] = MaxSpeed;
            values[(int)AircraftStat.BoostSpeed] = BoostSpeed;
            values[(int)AircraftStat.Acceleration] = Acceleration;
            values[(int)AircraftStat.Deceleration] = Deceleration;
            values[(int)AircraftStat.ThrottleResponse] = ThrottleResponse;
            values[(int)AircraftStat.PitchRate] = PitchRate;
            values[(int)AircraftStat.RollRate] = RollRate;
            values[(int)AircraftStat.YawRate] = YawRate;
            values[(int)AircraftStat.ControlResponse] = ControlResponse;
            values[(int)AircraftStat.LowSpeedAgility] = LowSpeedAgility;
            values[(int)AircraftStat.BankTurnRate] = BankTurnRate;
        }
    }
}
