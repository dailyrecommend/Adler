using Adler.Flight;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 커맨드가 승인되는 순간 조명탄을 뿌린다.
    /// <para>
    /// 장전해두고 따로 버튼을 누르게 하지 않는다. 그러면 커맨드와 사출 사이에 결정이
    /// 하나 더 끼는데, 정작 조명탄을 쓸 때는 미사일이 날아오는 중이라 그 결정을 내릴
    /// 여유가 없다. 커맨드를 치는 몇 초 자체가 이미 충분한 값이다.
    /// </para>
    /// <para>
    /// 그래서 재보급이나 수리와 같은 부류가 된다 — 승인이 곧 실행이고, 다시 쓰려면
    /// 쿨타임을 기다린다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlareDispenser : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("이 장비를 실은 기체. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("조명탄이 나오는 자리. 비워두면 기체에서 나온다.")]
        [SerializeField] private Transform _dispensePoint;

        private StratagemBay _stratagems;
        private FlareDefinition _firing;

        private float _nextInterval;
        private int _leftInBurst;
        private int _ejected;

        /// <summary>지금 뿌리는 중인지.</summary>
        public bool IsDispensing => _leftInBurst > 0;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _stratagems = _aircraft != null ? _aircraft.Stratagems : null;

            if (_dispensePoint == null)
            {
                _dispensePoint = transform;
            }

            if (_stratagems == null)
            {
                Debug.LogError($"{nameof(FlareDispenser)}: 기체의 스트라타젬을 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable() => _stratagems.Authorized += OnAuthorized;

        private void OnDisable() => _stratagems.Authorized -= OnAuthorized;

        /// <summary>승인이 곧 발사다. 쿨타임은 스트라타젬 쪽이 이미 걸어 두었다.</summary>
        private void OnAuthorized(StratagemDefinition stratagem)
        {
            if (stratagem is not FlareDefinition flare)
            {
                return;
            }

            _firing = flare;
            _leftInBurst = flare.Count;
            _ejected = 0;
            _nextInterval = 0f;
        }

        private void Update()
        {
            if (_leftInBurst <= 0)
            {
                return;
            }

            _nextInterval -= Time.deltaTime;

            if (_nextInterval > 0f)
            {
                return;
            }

            Eject(_ejected);

            _ejected++;
            _leftInBurst--;
            _nextInterval = _firing.Interval;
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
        private void Eject(int index)
        {
            if (_firing.Prefab == null)
            {
                return;
            }

            Transform origin = _dispensePoint;

            // 0, +1, -1, +2, -2 … 순으로 좌우를 번갈아 점점 크게 벌린다.
            // 원 둘레로 돌리면 고르게 퍼지기는 하지만 고리로 보인다 — 조명탄은
            // 날개를 따라 좌우로 갈라져야 뿌린 방향이 읽힌다.
            int step = (index + 1) / 2;
            float side = index % 2 == 0 ? 1f : -1f;
            float spread = _firing.SpreadAngle * side * step / Mathf.Max(1, _firing.Count / 2);
            float lateral = Mathf.Sin(spread * Mathf.Deg2Rad);

            Vector3 offset = origin.right * (lateral * _firing.SpawnRadius);

            Vector3 direction =
                (-origin.forward * _firing.BackwardBias)
                + (-origin.up * _firing.DownwardBias)
                + (origin.right * lateral);

            direction = Quaternion.AngleAxis(
                Random.Range(-_firing.Scatter, _firing.Scatter), origin.forward) * direction;

            direction = direction.normalized;

            GameObject spawned = Instantiate(
                _firing.Prefab, origin.position + offset, Quaternion.LookRotation(direction));

            var flare = spawned.GetComponent<Flare>();

            if (flare == null)
            {
                Debug.LogError($"{nameof(FlareDispenser)}: 조명탄 프리팹에 Flare 컴포넌트가 없습니다.", this);
                Destroy(spawned);
                return;
            }

            flare.Ignite(_firing, direction * _firing.EjectSpeed, _firing.Spin);
        }
    }
}
