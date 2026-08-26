using System;
using System.Collections.Generic;
using Adler.Core;
using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 싸움을 점수로 매긴다. 잘 섞어 때리면 오르고, 놀거나 맞으면 내린다.
    /// <para>
    /// 손맛 통로를 구독할 뿐, 무기도 기체도 모른다. 때린 것의 종류와 무게는
    /// <see cref="ImpactChannel"/>이 이미 나르고 있어서, 점수를 매기는 데 필요한
    /// 신호가 새 배선 하나 없이 전부 온다 — 새 무기가 생겨도 여기는 그대로다.
    /// </para>
    /// <para>
    /// 같은 종류만 반복하면 값이 마른다. 종류마다 신선도를 두고 쓸 때마다 그것만
    /// 깎으므로, 기총만 긁는 것보다 기총→갈고리→박치기로 섞는 쪽이 언제나 낫다.
    /// 무엇을 섞을 수 있는지가 곧 이 게임의 손패라서, 점수가 손패를 꺼내게 만든다.
    /// </para>
    /// <para>
    /// 늦춰진 시계를 쓴다. 히트스톱으로 화면이 멎은 동안 점수만 새고 있으면,
    /// 잘 맞힌 값으로 받은 멈춤이 오히려 손해가 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StyleMeter : MonoBehaviour
    {
        [Tooltip("점수의 규칙표. 랭크·점수·감소가 전부 이 에셋에 있다.")]
        [SerializeField] private StyleDefinition _definition;

        // 종류별 신선도(0~1). 쓸수록 마르고 쉬면 돌아온다.
        private readonly Dictionary<ImpactWeight, float> _freshness = new();

        private float _score;
        private float _graceUntil;
        private int _rank;
        private Clock _clock;

        /// <summary>랭크가 바뀔 때. (떠난 칸, 들어선 칸). 화면이 글자를 튀길 자리다.</summary>
        public event Action<int, int> RankChanged;

        /// <summary>지금 점수.</summary>
        public float Score => _score;

        /// <summary>
        /// 이번 판에 벌어들인 점수의 합. 새거나 맞아서 잃은 것은 빼지 않는다.
        /// <para>
        /// 천장에 막힌 몫도 센다. 게이지가 가득한 채로 잘 싸운 것도 벌어들인 것이고,
        /// 여기서 안 세면 최고 랭크를 유지한 시간이 기록에서 가장 초라한 시간이 된다.
        /// </para>
        /// </summary>
        public float TotalEarned { get; private set; }

        /// <summary>지금 랭크 칸 번호. 0은 무랭크다.</summary>
        public int RankIndex => _rank;

        /// <summary>지금 랭크의 글자. 무랭크면 빈 글자일 수 있다.</summary>
        public string RankName =>
            _definition != null && _rank < _definition.Tiers.Length
                ? _definition.Tiers[_rank].Name
                : string.Empty;

        /// <summary>
        /// 다음 랭크까지 차오른 정도(0~1). 꼭대기에서는 1이다.
        /// 게이지가 매 프레임 읽는다 — 알림은 랭크가 바뀔 때만 나간다.
        /// </summary>
        public float RankProgress
        {
            get
            {
                if (_definition == null || _rank >= _definition.Tiers.Length - 1)
                {
                    return 1f;
                }

                float floor = _definition.Tiers[_rank].Threshold;
                float next = _definition.Tiers[_rank + 1].Threshold;

                return Mathf.InverseLerp(floor, next, _score);
            }
        }

        private void Awake()
        {
            _clock = TimeScale.For(this);

            if (_definition == null || _definition.Tiers.Length == 0)
            {
                Debug.LogError($"{nameof(StyleMeter)}: 규칙표가 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            ImpactChannel.Landed += OnLanded;
            ImpactChannel.Suffered += OnSuffered;
        }

        private void OnDisable()
        {
            ImpactChannel.Landed -= OnLanded;
            ImpactChannel.Suffered -= OnSuffered;
        }

        /// <summary>
        /// 출격을 다시 시작할 때 비운다. 지난 싸움의 점수를 안고 시작하면
        /// 첫 랭크가 실력이 아니라 이월이 된다.
        /// </summary>
        public void ResetScore()
        {
            _score = 0f;
            TotalEarned = 0f;
            _freshness.Clear();
            _graceUntil = 0f;
            RefreshRank();
        }

        private void OnLanded(ImpactWeight weight)
        {
            float points = _definition.PointsFor(weight);

            if (points <= 0f)
            {
                return;
            }

            float freshness = _freshness.TryGetValue(weight, out float value) ? value : 1f;
            float earned = points * freshness;

            _score = Mathf.Min(_score + earned, _definition.Ceiling);
            TotalEarned += earned;

            // 쓴 종류만 마른다. 전부 같이 깎으면 섞어 쓴 사람과 한 우물만 판
            // 사람이 같은 값을 받아서, 섞으라는 뜻이 사라진다.
            _freshness[weight] = Mathf.Max(_definition.RepeatFloor, freshness * _definition.RepeatFactor);

            _graceUntil = _clock.Now + _definition.GraceSeconds;
            RefreshRank();
        }

        /// <summary>
        /// 맞으면 깎인다. 유예도 지운다 — 맞고도 점수가 편히 쉬고 있으면
        /// 피하는 것이 점수와 무관한 일이 된다.
        /// </summary>
        private void OnSuffered(float fraction)
        {
            _score = Mathf.Max(0f, _score - (_definition.HitPenalty * fraction));
            _graceUntil = 0f;
            RefreshRank();
        }

        private void Update()
        {
            float delta = _clock.Delta;

            if (delta <= 0f)
            {
                return;
            }

            Recover(delta);
            Drain(delta);
        }

        /// <summary>마른 신선도가 천천히 돌아온다. 쉬었다 다시 꺼내면 제값을 한다.</summary>
        private void Recover(float delta)
        {
            if (_freshness.Count == 0)
            {
                return;
            }

            float step = delta / _definition.RepeatRecoverySeconds;

            // 딕셔너리는 돌면서 못 고친다. 키를 먼저 떠놓는 대신, 종류가 다섯뿐이라
            // 스택에 잠깐 담는다.
            Span<ImpactWeight> keys = stackalloc ImpactWeight[8];
            int count = 0;

            foreach (KeyValuePair<ImpactWeight, float> entry in _freshness)
            {
                if (entry.Value < 1f && count < keys.Length)
                {
                    keys[count++] = entry.Key;
                }
            }

            for (int i = 0; i < count; i++)
            {
                _freshness[keys[i]] = Mathf.Min(1f, _freshness[keys[i]] + step);
            }
        }

        /// <summary>유예가 끝났으면 지금 랭크의 속도로 샌다. 높이 올라올수록 빨리 샌다.</summary>
        private void Drain(float delta)
        {
            if (_score <= 0f || _clock.Now < _graceUntil)
            {
                return;
            }

            _score = Mathf.Max(0f, _score - (_definition.Tiers[_rank].DrainPerSecond * delta));
            RefreshRank();
        }

        /// <summary>
        /// 점수에서 랭크를 다시 센다. 문턱은 낮은 것부터 순서대로라 뒤에서부터 본다.
        /// </summary>
        private void RefreshRank()
        {
            int rank = 0;

            for (int i = _definition.Tiers.Length - 1; i >= 0; i--)
            {
                if (_score >= _definition.Tiers[i].Threshold)
                {
                    rank = i;
                    break;
                }
            }

            if (rank == _rank)
            {
                return;
            }

            int previous = _rank;
            _rank = rank;
            RankChanged?.Invoke(previous, rank);
        }
    }
}
