using System;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 판 하나의 기록. 사는 동안 세고, 죽는 순간 확정해 내보낸다.
    /// <para>
    /// 죽고 나서 세면 늦다. 죽음이 모든 상태를 지우게 되어 있고, 점수는 놀면 새는
    /// 물건이라 죽는 순간의 값이 그 판의 값이 아니다 — 최고점은 지나갈 때마다
    /// 적어둬야 남는다. 그래서 이 기록은 판이 도는 내내 곁에서 센다.
    /// </para>
    /// <para>
    /// 화면을 모른다. 확정된 보고서를 이벤트로 내보낼 뿐, 그것을 어디에 어떻게
    /// 그릴지는 화면 쪽의 몫이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunReport : MonoBehaviour
    {
        /// <summary>확정된 판 하나의 기록.</summary>
        public readonly struct Report
        {
            /// <summary>버틴 시간(초).</summary>
            public readonly float Seconds;

            /// <summary>격추 수.</summary>
            public readonly int Kills;

            /// <summary>이번 판에 벌어들인 점수의 합.</summary>
            public readonly float Style;

            /// <summary>지나온 최고 랭크의 글자. 무랭크면 빈 글자다.</summary>
            public readonly string BestRank;

            public Report(float seconds, int kills, string bestRank, float style)
            {
                Seconds = seconds;
                Kills = kills;
                BestRank = bestRank;
                Style = style;
            }
        }

        [Header("참조")]
        [Tooltip("기록할 기체. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("점수판. 최고 랭크를 여기서 받아 적는다. 비워두면 랭크는 빈 글자다.")]
        [SerializeField] private StyleMeter _style;

        private Clock _clock;
        private float _startedAt;
        private int _kills;
        private int _bestRank;
        private string _bestRankName = string.Empty;

        /// <summary>판이 끝났다. 화면이 이 보고서를 받아 그린다.</summary>
        public event Action<Report> Finished;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null || _aircraft.Lifecycle == null)
            {
                Debug.LogError($"{nameof(RunReport)}: 기체 또는 생명주기를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _aircraft.Lifecycle.Respawned += Begin;
            _aircraft.Lifecycle.Destroyed += Finish;
            ImpactChannel.Landed += OnLanded;

            if (_style != null)
            {
                _style.RankChanged += OnRankChanged;
            }
        }

        private void OnDisable()
        {
            _aircraft.Lifecycle.Respawned -= Begin;
            _aircraft.Lifecycle.Destroyed -= Finish;
            ImpactChannel.Landed -= OnLanded;

            if (_style != null)
            {
                _style.RankChanged -= OnRankChanged;
            }
        }

        private void Start() => Begin();

        private void Begin()
        {
            _startedAt = _clock.Now;
            _kills = 0;
            _bestRank = 0;
            _bestRankName = string.Empty;
        }

        private void OnLanded(ImpactWeight weight)
        {
            if (weight == ImpactWeight.Kill)
            {
                _kills++;
            }
        }

        /// <summary>
        /// 최고점은 지나갈 때만 잡을 수 있다. 글자도 그 자리에서 받아 적는다 —
        /// 나중에 번호로 규칙표를 다시 뒤지면 이 기록이 규칙표를 알아야 한다.
        /// </summary>
        private void OnRankChanged(int from, int to)
        {
            if (to > _bestRank)
            {
                _bestRank = to;
                _bestRankName = _style.RankName;
            }
        }

        private void Finish() =>
            Finished?.Invoke(new Report(
                _clock.Now - _startedAt,
                _kills,
                _bestRankName,
                _style != null ? _style.TotalEarned : 0f));
    }
}
