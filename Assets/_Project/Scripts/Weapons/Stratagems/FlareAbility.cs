using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 조명탄을 정해진 수만큼 간격을 두고 내놓는다.
    /// <para>
    /// 한 번에 쏟지 않는다. 동시에 나오면 한 덩어리로 보여서 몇 개인지 읽히지 않고,
    /// 유도탄이 속을 여지도 한 순간에 끝난다. 시간을 두고 뿌려야 그 사이가 도망갈
    /// 틈이 된다.
    /// </para>
    /// </summary>
    public sealed class FlareAbility : Ability
    {
        private readonly FlareDefinition _flare;

        private int _left;
        private int _ejected;
        private float _nextAt;

        public FlareAbility(FlareDefinition flare) : base(flare) => _flare = flare;

        protected override void OnBegin(in AbilityContext context)
        {
            _left = _flare.Count;
            _ejected = 0;
            _nextAt = 0f;
        }

        protected override void OnActive(in AbilityContext context)
        {
            if (_left <= 0)
            {
                Finish();
                return;
            }

            _nextAt -= context.Delta;

            if (_nextAt > 0f)
            {
                return;
            }

            Eject(in context, _ejected);

            _ejected++;
            _left--;
            _nextAt = _flare.Interval;
        }

        /// <summary>
        /// 조명탄 하나를 내놓는다.
        /// <para>
        /// 좌우를 번갈아 점점 크게 벌린다. 한쪽으로만 뿌리면 반대편에서 오는 미사일에
        /// 통하지 않고, 한 점에서 계속 나오면 뒤로 한 줄이 그어질 뿐이라 몇 개인지
        /// 읽히지 않는다.
        /// </para>
        /// <para>
        /// 기체 속도를 더하지 않는다. 사출 속도만으로 나가야 뒤로 처지면서 벌어지고,
        /// 그 벌어지는 거리가 곧 폭발에 휘말리지 않는 여지다.
        /// </para>
        /// </summary>
        private void Eject(in AbilityContext context, int index)
        {
            Transform origin = context.Hardpoint?.Mount;

            if (origin == null || _flare.Prefab == null)
            {
                Finish();
                return;
            }

            // 0, +1, -1, +2, -2 … 순으로 좌우를 번갈아 점점 크게 벌린다.
            int step = (index + 1) / 2;
            float side = index % 2 == 0 ? 1f : -1f;
            float spread = _flare.SpreadAngle * side * step / Mathf.Max(1, _flare.Count / 2);
            float lateral = Mathf.Sin(spread * Mathf.Deg2Rad);

            Vector3 offset = origin.right * (lateral * _flare.SpawnRadius);

            Vector3 direction =
                (-origin.forward * _flare.BackwardBias)
                + (-origin.up * _flare.DownwardBias)
                + (origin.right * lateral);

            direction = Quaternion.AngleAxis(
                Random.Range(-_flare.Scatter, _flare.Scatter), origin.forward) * direction;

            direction = direction.normalized;

            GameObject spawned = Object.Instantiate(
                _flare.Prefab, origin.position + offset, Quaternion.LookRotation(direction));

            if (spawned.TryGetComponent(out Flare flare))
            {
                flare.Ignite(_flare, direction * _flare.EjectSpeed, _flare.Spin);
                return;
            }

            Debug.LogError($"{nameof(FlareAbility)}: 조명탄 프리팹에 {nameof(Flare)}가 없습니다.", spawned);
            Object.Destroy(spawned);
        }
    }
}
