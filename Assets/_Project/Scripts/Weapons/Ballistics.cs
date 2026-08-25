using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 쏘는 쪽이라면 누구나 쓰는 탄도 계산.
    /// <para>
    /// 적기와 대공포가 같은 셈을 각자 들고 있었다. 같은 셈이 두 벌이면 조준 감각을
    /// 손볼 때 한쪽만 고치게 되고, 그 어긋남은 "대공포만 이상하게 잘 맞힌다"처럼
    /// 값을 만진 곳과 먼 데서 드러난다.
    /// </para>
    /// </summary>
    public static class Ballistics
    {
        /// <summary>
        /// 탄이 도착할 무렵 표적이 있을 자리.
        /// <para>
        /// 도착 시간이 거리에 따라 달라지고 거리는 다시 예측 지점에 따라 달라지므로,
        /// 두 번 반복해 맞춰 간다. 기총 사거리에서는 두 번이면 충분히 수렴한다.
        /// </para>
        /// </summary>
        /// <param name="shooterVelocity">
        /// 쏘는 쪽의 속도. 탄에 기체 속도가 얹혀 나가는 무기는 자기 속도를 넣어야
        /// 표적이 상대 속도로 계산된다. 제자리에 선 포탑은 0을 넣는다.
        /// </param>
        public static Vector3 LeadPoint(
            Vector3 muzzle,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            Vector3 shooterVelocity,
            float muzzleVelocity)
        {
            Vector3 relative = targetVelocity - shooterVelocity;
            Vector3 predicted = targetPosition;

            for (int i = 0; i < 2; i++)
            {
                float flightTime = Vector3.Distance(muzzle, predicted) / muzzleVelocity;
                predicted = targetPosition + (relative * flightTime);
            }

            return predicted;
        }

        /// <summary>
        /// 점사 한 번이 빗나갈 방향과 폭.
        /// <para>
        /// 사격을 시작할 때 한 번만 뽑는다. 매 발 새로 뽑으면 탄이 사방으로 흩어져
        /// 그냥 부정확해 보이고, 한 번만 뽑으면 한 줄기가 통째로 빗나가는 것으로 읽힌다.
        /// </para>
        /// <para>
        /// 사거리가 아니라 지금 거리에 비례한다. 사거리로 재면 코앞을 스쳐 지나가도
        /// 멀리 도는 것과 똑같이 빗나가서, 바싹 붙는 것이 공짜가 된다.
        /// </para>
        /// </summary>
        public static Vector3 BurstScatter(Vector3 from, Vector3 targetPosition, float leadError)
        {
            float distance = Vector3.Distance(from, targetPosition);

            return Random.onUnitSphere * (distance * leadError * Random.value);
        }
    }
}
