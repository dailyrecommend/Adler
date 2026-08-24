using System;
using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 피격을 화면 반응으로 옮긴다.
    /// <para>
    /// 한 발마다 반응하지 않는다. 대공포가 분당 300발이면 초당 다섯 번인데, 발마다
    /// 화면이 튀면 진동이 아니라 스트로브가 된다. 짧은 창 동안 받은 피해를 모아
    /// 한 번에 반응하면 세기가 저절로 맞는다 — 스치면 살짝, 쏟아지면 크게.
    /// </para>
    /// <para>
    /// 첫 발은 기다리지 않고 바로 반응한다. 맞기 시작했다는 사실은 늦게 알수록
    /// 쓸모가 줄어들기 때문이다. 창은 그 뒤로 이어지는 발들을 묶는 데만 쓴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageFeedback : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("흔들 화면. 비워두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private HudShake _hudShake;


        [Header("반응")]
        [Tooltip("이어지는 피격을 묶는 시간(초). 이 안에 들어온 피해는 한 번에 반영된다.")]
        [Min(0.05f)]
        [SerializeField] private float _window = 0.2f;

        [Tooltip("내구도를 한 번에 전부 잃었을 때의 흔들림 폭 (픽셀).\n" +
                 "실제 흔들림은 받은 피해가 최대 내구도에서 차지하는 비율에 비례한다.")]
        [Min(0f)]
        [SerializeField] private float _shakeAtFullLoss = 60f;

        [Tooltip("아무리 약하게 맞아도 이만큼은 흔들린다. 스친 것도 알아챌 수 있게.")]
        [Min(0f)]
        [SerializeField] private float _minimumShake = 3f;

        private Health _health;
        private float _pending;
        private Clock _clock;
        private float _nextReactionAt;

        /// <summary>피격 반응이 일어날 때. 0~1로 정규화된 세기. 소리나 효과가 구독한다.</summary>
        public event Action<float> Reacted;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _health = _aircraft != null ? _aircraft.Health : null;

            if (_hudShake == null)
            {
                _hudShake = GetComponent<HudShake>();
            }

            if (_health == null)
            {
                Debug.LogError($"{nameof(DamageFeedback)}: 기체의 내구도를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable() => _health.Damaged += OnDamaged;

        private void OnDisable() => _health.Damaged -= OnDamaged;

        private void OnDamaged(Health health, DamageInfo damage) => _pending += damage.Amount;

        private void Update()
        {
            if (_pending <= 0f || _clock.Now < _nextReactionAt)
            {
                return;
            }

            float fraction = _health.Max > 0f ? Mathf.Clamp01(_pending / _health.Max) : 0f;
            _pending = 0f;
            _nextReactionAt = _clock.Now + _window;

            if (_hudShake != null)
            {
                _hudShake.AddImpulse(Mathf.Max(_minimumShake, _shakeAtFullLoss * fraction));
            }

            // 카메라 흔들림이나 소리는 이 신호를 듣는 쪽이 알아서 한다. 여기서 하나씩
            // 불러주면 반응을 붙일 때마다 이 파일이 그것을 알아야 한다.
            Reacted?.Invoke(fraction);
        }
    }
}
