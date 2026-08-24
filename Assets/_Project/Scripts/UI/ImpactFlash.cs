using System;
using System.Collections.Generic;
using Adler.Core;
using Adler.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 타격의 무게에 맞춰 화면을 순간적으로 물들인다.
    /// <para>
    /// 흔들림이나 시간 늦춤과 달리 <b>보고 있지 않아도 들어온다</b>. 화면 전체가 한
    /// 프레임 하얘지는 것은 곁눈으로도 놓칠 수 없어서, 조준점을 보고 있는 동안 일어난
    /// 일을 알리는 데 가장 확실하다. 그만큼 아껴 써야 한다 — 자주 터지면 눈이 피로하고
    /// 그 순간 정작 봐야 할 것이 가려진다.
    /// </para>
    /// <para>
    /// 무게마다 한 줄이다. 들이받기만을 위한 것이 아니라, 나중에 격추나 폭발에도
    /// 붙이고 싶어지면 줄을 더하면 된다.
    /// </para>
    /// <para>
    /// 켤 화면은 만들어 둔 것을 받는다. 색과 모양은 그쪽이 정하고 여기서는 얼마나
    /// 진하게, 얼마나 오래 남길지만 다룬다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactFlash : MonoBehaviour
    {
        [Serializable]
        public struct Burst
        {
            [Tooltip("어떤 타격에 터질지.")]
            public ImpactWeight When;

            [Tooltip("가장 진할 때의 불투명도.\n" +
                     "1이면 화면이 완전히 덮인다 — 아주 짧게 스칠 때만 쓸 만하다.")]
            [Range(0f, 1f)]
            public float Peak;

            [Tooltip("가장 진한 채로 머무는 시간(초).\n" +
                     "0이면 닿자마자 옅어지기 시작한다. 대개 0이 맞다.")]
            [Min(0f)]
            public float HoldSeconds;

            [Tooltip("옅어져 사라지기까지의 시간(초).\n" +
                     "길게 잡으면 번쩍임이 아니라 화면이 뿌옇게 된 것으로 읽힌다.")]
            [Min(0.01f)]
            public float FadeSeconds;

            [Tooltip("이 색으로 물든다. 흰색이면 하얗게 날아간다.")]
            public Color Tint;
        }

        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("타격을 알리는 쪽. 비워두면 기체 아래에서 찾는다.")]
        [SerializeField] private HitImpact _impact;

        [Header("화면")]
        [Tooltip("화면을 덮을 것. 보통 화면 전체를 채운 흰 Image다.\n" +
                 "Raycast Target은 꺼둘 것 — 켜두면 번쩍이는 동안 클릭이 막힌다.")]
        [SerializeField] private Graphic _screen;

        [Header("무게별")]
        [SerializeField] private List<Burst> _bursts = new();

        private Clock _clock;
        private Burst _running;
        private float _elapsed;
        private bool _flashing;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_impact == null && _aircraft != null)
            {
                _impact = _aircraft.GetComponentInChildren<HitImpact>(includeInactive: true);
            }

            if (_impact == null || _screen == null)
            {
                Debug.LogError($"{nameof(ImpactFlash)}: {nameof(HitImpact)} 또는 덮을 화면이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            Clear();
        }

        private void OnEnable() => _impact.Impact += OnImpact;

        private void OnDisable()
        {
            _impact.Impact -= OnImpact;

            // 꺼질 때 덮인 채로 두면 화면이 하얀 채로 남는다.
            Clear();
        }

        /// <summary>
        /// 진행 중이어도 다시 터뜨린다.
        /// <para>
        /// 옅어지는 것을 기다려주면 연달아 들이받았을 때 두 번째가 묻힌다 — 두 번
        /// 맞혔다는 사실이 화면에서 사라지는 셈이라, 처음부터 다시 시작하는 편이 맞다.
        /// </para>
        /// </summary>
        private void OnImpact(ImpactWeight weight)
        {
            if (!TryFind(weight, out Burst burst) || burst.Peak <= 0f)
            {
                return;
            }

            _running = burst;
            _elapsed = 0f;
            _flashing = true;

            Paint(burst.Peak);
        }

        private void Update()
        {
            if (!_flashing)
            {
                return;
            }

            // 늦춰진 시계로 센다. 히트스톱이 걸린 그 순간에 터지는 것이라, 바깥
            // 시간으로 세면 화면이 멈춰 있는 동안 번쩍임만 혼자 끝나버린다.
            _elapsed += _clock.Delta;

            float left = _elapsed - _running.HoldSeconds;

            if (left <= 0f)
            {
                return;
            }

            float fade = 1f - Mathf.Clamp01(left / _running.FadeSeconds);

            if (fade <= 0f)
            {
                Clear();
                return;
            }

            Paint(_running.Peak * fade);
        }

        private bool TryFind(ImpactWeight weight, out Burst burst)
        {
            foreach (Burst candidate in _bursts)
            {
                if (candidate.When == weight)
                {
                    burst = candidate;
                    return true;
                }
            }

            burst = default;
            return false;
        }

        private void Paint(float alpha)
        {
            Color color = _running.Tint;
            color.a = alpha;

            _screen.color = color;

            if (!_screen.enabled)
            {
                _screen.enabled = true;
            }
        }

        private void Clear()
        {
            _flashing = false;

            if (_screen != null)
            {
                // 끄기까지 한다. 투명한 채로 켜두면 화면 전체를 덮는 것이 매 프레임
                // 그려져서, 아무것도 보이지 않는데 채우기 비용만 계속 나간다.
                _screen.enabled = false;
            }
        }
    }
}
