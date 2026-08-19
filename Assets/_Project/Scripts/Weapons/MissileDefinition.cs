using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 미사일 한 종류의 성능.
    /// <para>
    /// 기총과 반대편에 있다. 조준을 붙잡고 있어야 쏠 수 있고 발수도 적지만, 한 번 걸리면
    /// 표적을 쫓아간다. 기총이 지금 겨눈 곳을 때린다면 미사일은 <em>겨누고 있던 시간</em>을
    /// 값으로 치른다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Missile", menuName = "Adler/Weapons/Missile Definition")]
    public sealed class MissileDefinition : WeaponDefinition
    {
        [Header("조준")]
        [Tooltip("표적을 이 각도 안에 두어야 조준이 잡히기 시작한다.")]
        [Range(1f, 90f)]
        public float LockAngle = 20f;

        [Tooltip("이 거리 안의 표적만 잡는다 (m).")]
        [Min(1f)]
        public float LockRange = 400f;

        [Tooltip("조준이 완성되기까지 붙잡고 있어야 하는 시간(초).\n" +
                 "이 시간이 곧 미사일의 값이다. 짧으면 기총과 다를 바 없어진다.")]
        [Min(0f)]
        public float LockSeconds = 1.2f;

        [Tooltip("조준이 벗어나도 이만큼은 버틴다(초). 잠깐 시야에서 놓쳐도 풀리지 않게.")]
        [Min(0f)]
        public float LockGraceSeconds = 0.5f;

        [Header("비행")]
        [Tooltip("발사 직후 속도 (m/s). 여기에 기체 속도가 더해진다.")]
        [Min(1f)]
        public float LaunchSpeed = 40f;

        [Tooltip("최대 속도 (m/s).")]
        [Min(1f)]
        public float MaxSpeed = 160f;

        [Tooltip("최대 속도까지 붙는 가속 (m/s²).")]
        [Min(1f)]
        public float Acceleration = 120f;

        [Tooltip("방향을 꺾을 수 있는 속도 (도/초).\n" +
                 "낮으면 급기동하는 표적을 놓친다 — 미사일을 피할 여지가 여기서 나온다.")]
        [Min(1f)]
        public float TurnRate = 90f;

        [Tooltip("발사 후 이만큼은 곧장 나아간다(초). 기체에서 떨어져 나오는 시간이다.")]
        [Min(0f)]
        public float StraightFlightSeconds = 0.25f;

        [Header("폭발")]
        [Tooltip("이 거리 안쪽은 피해량이 그대로 들어간다 (m).")]
        [Min(0f)]
        public float InnerRadius = 2f;

        [Tooltip("폭발이 닿는 최대 거리 (m).")]
        [Min(0f)]
        public float BlastRadius = 6f;

        [Tooltip("표적에 이만큼까지 다가가면 터진다 (m). 스치듯 지나가도 터지게 한다.")]
        [Min(0f)]
        public float ProximityRadius = 1.5f;

        private void OnValidate()
        {
            if (InnerRadius > BlastRadius)
            {
                InnerRadius = BlastRadius;
            }

            if (LaunchSpeed > MaxSpeed)
            {
                LaunchSpeed = MaxSpeed;
            }
        }
    }
}
