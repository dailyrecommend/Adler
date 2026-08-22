using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 비례항법유도. 시선각이 도는 속도에 비례해 방향을 꺾는다.
    /// <para>
    /// 두 물체가 서로를 보는 각도가 변하지 않으면 반드시 충돌한다. 배가 서로를 피하는 데
    /// 쓰는 것과 같은 성질이다. 그래서 시선각이 도는 것은 곧 빗나가고 있다는 뜻이고,
    /// 도는 만큼 반대로 꺾어주면 충돌 경로로 돌아온다.
    /// </para>
    /// <para>
    /// 표적을 향하지 않는다는 것이 요점이다. 표적이 있는 곳이 아니라 만나게 될 곳으로
    /// 가므로 리드가 저절로 잡히고, 마지막 순간에 몰아서 꺾을 일이 없다. 표적을 그대로
    /// 쫓으면 거리가 0에 가까워질수록 필요한 선회량이 무한히 커지는데, 그것이 미사일이
    /// 빗나가는 진짜 이유였다.
    /// </para>
    /// <para>
    /// 미사일이 아니라 숫자만 받는다. 그래야 씬을 띄우지 않고도 그대로 시험할 수 있고,
    /// 나중에 적기가 회피 기동을 계산할 때도 같은 식을 쓸 수 있다.
    /// </para>
    /// </summary>
    public static class ProportionalNavigation
    {
        /// <summary>지금 향해야 할 방향. 정규화해서 돌려준다.</summary>
        /// <param name="navigationConstant">
        /// 시선각이 도는 만큼의 몇 배로 꺾을지. 3보다 낮으면 리드가 모자라 뒤를 쫓고,
        /// 5를 넘으면 표적이 조금만 움직여도 크게 반응해 경로가 출렁인다.
        /// </param>
        public static Vector3 Heading(
            Vector3 position,
            Vector3 velocity,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float navigationConstant,
            float deltaTime)
        {
            Vector3 toTarget = targetPosition - position;
            float rangeSquared = toTarget.sqrMagnitude;

            if (rangeSquared < 0.0001f)
            {
                return velocity.normalized;
            }

            Vector3 relativeVelocity = targetVelocity - velocity;

            // 시선이 도는 속도. 표적이 정확히 다가오거나 멀어지기만 하면 0이 되고,
            // 그때는 이미 충돌 경로라 꺾을 이유가 없다.
            Vector3 lineOfSightRate = Vector3.Cross(toTarget, relativeVelocity) / rangeSquared;

            // 시선이 도는 축을 기준으로 진행 방향을 밀어낸다. 결과는 진행 방향에
            // 수직이라 속도를 잃지 않고 방향만 바뀐다.
            Vector3 acceleration = Vector3.Cross(lineOfSightRate, velocity) * navigationConstant;

            return (velocity + (acceleration * deltaTime)).normalized;
        }
    }
}
