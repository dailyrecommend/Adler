using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 미사일 한 종류의 성능.
    /// <para>
    /// 기총과 반대편에 있다. 발수가 적고 사거리 안에 들어야 쏠 수 있지만, 한 번 나가면
    /// 표적을 쫓아간다. 기총이 지금 겨눈 곳을 때린다면 미사일은 <em>거리를 좁히는 일</em>을
    /// 값으로 치른다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Missile", menuName = "Adler/Weapons/Missile Definition")]
    public sealed class MissileDefinition : WeaponDefinition
    {
        [Header("교전")]
        [Tooltip("이 거리 안의 표적에만 쏠 수 있다 (m).\n\n" +
                 "표적을 잡는 일은 조준 쪽이 맡고 화면에 보이는 한 훨씬 멀리까지 잡히므로,\n" +
                 "여기가 이 무기가 닿는 곳을 정하는 유일한 자리다.")]
        [Min(1f)]
        public float LockRange = 400f;

        [Header("유도")]
        [Tooltip("항법상수(N). 시선각이 도는 만큼의 몇 배로 꺾을지.\n" +
                 "3보다 낮으면 리드가 모자라 뒤를 쫓고, 5를 넘으면 표적이 조금만 움직여도\n" +
                 "크게 반응해 경로가 출렁이면서 오히려 속도를 잃는다.")]
        [Range(1f, 8f)]
        public float NavigationConstant = 4f;

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

        [Tooltip("이 거리 안으로 들어온 뒤 표적을 스쳐 지나가면 놓친 것으로 보고 유도를 끈다 (m).\n" +
                 "0이면 포기하지 않고 끝까지 쫓는다.\n\n" +
                 "이것이 없으면 급선회로 지나치게 만들어도 크게 돌아 다시 온다. 사거리가 다할\n" +
                 "때까지 반복되므로, 잘 피한 것이 시간을 버는 것에 그치고 결국 같은 결말이 된다.\n" +
                 "피할 수 있으려면 피했다는 판정이 있어야 한다.")]
        [Min(0f)]
        public float MissRange;

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
