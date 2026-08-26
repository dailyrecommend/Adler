using System;
using System.Collections.Generic;
using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 전투 점수의 규칙표. 무엇이 몇 점이고, 어디서 랭크가 갈리고, 얼마나 빨리 새는지.
    /// <para>
    /// 숫자는 전부 여기 있고 도는 방식은 <see cref="StyleMeter"/>에 있다. 점수 조율은
    /// 굴려보면서 하는 일이라, 값을 바꿀 때마다 코드를 열게 두면 조율 자체를 안 하게 된다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Style", menuName = "Adler/Combat/Style Definition")]
    public sealed class StyleDefinition : ScriptableObject
    {
        /// <summary>랭크 한 칸. 문턱을 넘으면 이 랭크가 된다.</summary>
        [Serializable]
        public struct Tier
        {
            [Tooltip("화면에 띄울 글자. D, C, B… 첫 칸은 빈 글자로 두면 무랭크가 된다.")]
            public string Name;

            [Tooltip("이 점수를 넘으면 이 랭크다. 첫 칸은 0이어야 한다.")]
            [Min(0f)]
            public float Threshold;

            [Tooltip("이 랭크에 있는 동안 초당 새는 점수.\n" +
                     "위로 갈수록 크게 잡아야 높은 랭크가 유지할 가치가 있는 것이 된다.")]
            [Min(0f)]
            public float DrainPerSecond;
        }

        /// <summary>명중 한 종류의 값.</summary>
        [Serializable]
        public struct Reward
        {
            public ImpactWeight When;

            [Min(0f)]
            public float Points;
        }

        [Header("랭크")]
        [Tooltip("문턱이 낮은 것부터 높은 순서로. 첫 칸은 문턱 0의 무랭크여야 한다.")]
        public Tier[] Tiers = Array.Empty<Tier>();

        [Tooltip("점수가 이 위로는 쌓이지 않는다.\n\n" +
                 "꼭대기 문턱보다 넉넉히 높게 둔다. 문턱에서 딱 자르면 최고 랭크에 닿는\n" +
                 "순간부터 한 방울만 새도 떨어져서, 최고 랭크가 스치는 순간이 된다.")]
        [Min(1f)]
        public float Ceiling = 1200f;

        [Header("점수")]
        [Tooltip("명중의 무게마다 몇 점인지. 여기 없는 무게는 0점이다.")]
        public List<Reward> Points = new();

        [Header("반복")]
        [Tooltip("같은 종류로 또 맞힐 때마다 그 종류의 값이 이만큼으로 줄어든다.\n" +
                 "0.65면 세 번째부터는 절반도 안 된다 — 섞어 쓰는 것이 점수의 길이 된다.")]
        [Range(0.1f, 1f)]
        public float RepeatFactor = 0.65f;

        [Tooltip("깎인 값이 제자리로 돌아오는 데 걸리는 시간(초).")]
        [Min(0.1f)]
        public float RepeatRecoverySeconds = 4f;

        [Tooltip("반복해도 이 아래로는 안 깎인다. 0이면 같은 짓만 하는 사람은 결국 0점이다.")]
        [Range(0f, 1f)]
        public float RepeatFloor = 0.2f;

        [Header("흐름")]
        [Tooltip("마지막 득점 뒤 이만큼은 점수가 새지 않는다(초).")]
        [Min(0f)]
        public float GraceSeconds = 2f;

        [Tooltip("내구도를 전부 잃는 피격 기준으로 깎이는 점수.\n" +
                 "절반을 잃으면 이 절반이 깎인다. 피하는 것이 점수의 일부가 되는 자리다.")]
        [Min(0f)]
        public float HitPenalty = 400f;

        /// <summary>이 무게의 점수. 목록에 없으면 0.</summary>
        public float PointsFor(ImpactWeight weight)
        {
            foreach (Reward reward in Points)
            {
                if (reward.When == weight)
                {
                    return reward.Points;
                }
            }

            return 0f;
        }

        /// <summary>에셋을 처음 만들 때의 기본값. 빈 표에서 시작하지 않게 한다.</summary>
        private void Reset()
        {
            Tiers = new[]
            {
                new Tier { Name = string.Empty, Threshold = 0f, DrainPerSecond = 0f },
                new Tier { Name = "D", Threshold = 50f, DrainPerSecond = 6f },
                new Tier { Name = "C", Threshold = 150f, DrainPerSecond = 10f },
                new Tier { Name = "B", Threshold = 300f, DrainPerSecond = 16f },
                new Tier { Name = "A", Threshold = 500f, DrainPerSecond = 24f },
                new Tier { Name = "S", Threshold = 700f, DrainPerSecond = 34f },
                new Tier { Name = "SS", Threshold = 850f, DrainPerSecond = 46f },
                new Tier { Name = "SSS", Threshold = 1000f, DrainPerSecond = 60f },
            };

            Points = new List<Reward>
            {
                new() { When = ImpactWeight.Light, Points = 6f },
                new() { When = ImpactWeight.Blast, Points = 40f },
                new() { When = ImpactWeight.Kill, Points = 120f },
                new() { When = ImpactWeight.Ram, Points = 90f },
                new() { When = ImpactWeight.Grapple, Points = 30f },
            };
        }
    }
}
