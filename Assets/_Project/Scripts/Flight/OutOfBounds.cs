using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 경계 밖에 머무는 플레이어를 유예 뒤에 격추한다.
    /// <para>
    /// 적기는 경계를 넘지 못하게 조향을 꺾으면 되지만, 플레이어의 조종간을 꺾는 것은
    /// 조종을 빼앗는 일이다. 대신 값을 붙인다 — 나가는 것은 되지만 머무는 것은 안 된다.
    /// 서리 지대와 같은 문법이라, 배운 규칙이 하나 더 늘지 않는다.
    /// </para>
    /// <para>
    /// 판정만 한다. 경고를 어떻게 띄울지는 화면의 몫이고, 여기는 밖인지와 얼마나
    /// 남았는지를 내줄 뿐이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OutOfBounds : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("유예")]
        [Tooltip("경계 밖에서 버틸 수 있는 시간(초). 다 쓰면 격추된다.\n" +
                 "돌아오면 즉시 처음부터 다시 센다 — 절반쯤 쓴 유예가 남아 있으면\n" +
                 "다음에 스칠 때 영문 모를 즉사가 된다.")]
        [Min(0.5f)]
        [SerializeField] private float _graceSeconds = 5f;

        private Clock _clock;
        private float _outsideFor;

        /// <summary>지금 경계 밖인지. 경고 화면이 읽는다.</summary>
        public bool IsOutside { get; private set; }

        /// <summary>격추까지 남은 시간(초). 안에 있으면 유예 전체다.</summary>
        public float RemainingSeconds => Mathf.Max(0f, _graceSeconds - _outsideFor);

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null || _aircraft.Health == null)
            {
                Debug.LogError($"{nameof(OutOfBounds)}: 기체 또는 내구도를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Battlefield field = Battlefield.Active;
            Health health = _aircraft.Health;

            // 경계가 없거나 이미 죽었으면 셀 것이 없다. 죽은 뒤에도 세고 있으면
            // 되살아나는 순간 남은 유예로 한 번 더 죽는다.
            if (field == null || !health.IsAlive)
            {
                Clear();
                return;
            }

            IsOutside = !field.Contains(_aircraft.transform.position);

            if (!IsOutside)
            {
                Clear();
                return;
            }

            _outsideFor += _clock.Delta;

            if (_outsideFor < _graceSeconds)
            {
                return;
            }

            // 남은 내구도가 얼마든 한 번에 보낸다. 관통은 장갑을 무엇으로 두든
            // 뚫리도록 크게 준다 — 경계는 무기가 아니라 규칙이라 막을 수 없어야 한다.
            health.TakeDamage(new DamageInfo(
                float.MaxValue,
                float.MaxValue,
                0f,
                _aircraft.transform.position,
                Vector3.up,
                gameObject));

            Clear();
        }

        private void Clear()
        {
            IsOutside = false;
            _outsideFor = 0f;
        }
    }
}
