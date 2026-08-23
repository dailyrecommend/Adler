using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 밖에서 기체를 표적 쪽으로 끌어당기는 힘. 그래플이 매 물리 스텝 넣는다.
    /// <para>
    /// 나아가는 것과 겨누는 것을 따로 조절한다. 끌려가는 동안에도 조준은 플레이어
    /// 몫이어야 하기 때문이다 — 기수까지 붙잡으면 조종간이 안 먹는 것처럼 느껴지고,
    /// 화면에서 조준점이 굳어버린다.
    /// </para>
    /// </summary>
    public readonly struct Tether
    {
        /// <summary>표적이 있는 쪽. 정규화되어 있어야 한다.</summary>
        public readonly Vector3 Direction;

        /// <summary>
        /// 나아가는 방향이 그쪽으로 휘는 정도.
        /// 1이면 조종간과 무관하게 표적으로 끌려간다.
        /// </summary>
        public readonly float PathBend;

        /// <summary>
        /// 기수가 표적을 따라가는 정도. 거들어주는 몫이라 작게 둔다.
        /// 0.5를 넘기면 조종간을 빼앗긴 것처럼 느껴진다.
        /// </summary>
        public readonly float AimAssist;

        /// <summary>
        /// 끌려가는 동안 최소한 이만큼은 낸다 (m/s).
        /// <para>
        /// 배율이 아니라 절대값인 이유는, 배율은 <b>내</b> 속도를 기준으로 하기 때문이다.
        /// 상대가 부스터를 켜면 나도 같이 빨라지지 않아 그대로 놓친다. 잡아둔 상대보다
        /// 빠르다는 것이 보장돼야 거리가 계속 좁혀진다.
        /// </para>
        /// </summary>
        public readonly float SpeedFloor;

        /// <summary>
        /// 기수를 끌어당길 수 있는 최대 속도 (도/초).
        /// <para>
        /// 기체 자신의 선회율과 무관하다. 줄에 매달린 것이므로 혼자서는 낼 수 없는
        /// 속도로 홱 돌아가는 것이 맞고, 그러지 않으면 상대가 급기동하는 순간
        /// 기수가 뒤처져 따라갈 수가 없다.
        /// </para>
        /// </summary>
        public readonly float TurnRate;

        public Tether(
            Vector3 direction, float pathBend, float aimAssist, float speedFloor, float turnRate)
        {
            Direction = direction;
            PathBend = Mathf.Clamp01(pathBend);
            AimAssist = Mathf.Clamp01(aimAssist);
            SpeedFloor = Mathf.Max(0f, speedFloor);
            TurnRate = Mathf.Max(0f, turnRate);
        }

        /// <summary>아무것도 걸리지 않은 상태.</summary>
        public static Tether None => new(Vector3.zero, 0f, 0f, 0f, 0f);

        public bool IsActive => Direction.sqrMagnitude > 0.0001f;

        public bool BendsPath => IsActive && PathBend > 0f;

        public bool AssistsAim => IsActive && AimAssist > 0f;
    }
}
