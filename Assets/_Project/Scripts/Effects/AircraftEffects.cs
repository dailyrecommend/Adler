using System;
using System.Collections.Generic;
using Adler.Combat;
using Adler.Flight;
using UnityEngine;
using UnityEngine.VFX;

namespace Adler.Effects
{
    /// <summary>
    /// 기체의 상태에 맞춰 붙어 있는 이펙트를 켜고 끈다.
    /// <para>
    /// 총구 화염이든 부스터 불꽃이든 하는 일은 하나 ─ "조건이 참이면 재생하고 아니면
    /// 멈춘다" ─ 이고 다른 것은 무엇을 보느냐뿐이다. 이펙트마다 컴포넌트를 두면 켜고
    /// 끄는 방식을 고칠 때 그 수만큼 고쳐야 하고, 붙일 이펙트가 늘수록 인스펙터가
    /// 같은 모양의 칸으로 채워진다.
    /// </para>
    /// <para>
    /// 조건은 화면 효과 쪽과 같은 것을 쓴다. 켜는 방식은 달라도 <b>언제 켜는가</b>는
    /// 같은 질문이라, 답을 각자 갖고 있으면 조건을 더할 때 두 곳이 어긋난다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftEffects : MonoBehaviour
    {
        [Serializable]
        public struct Emitter
        {
            [Tooltip("켜고 끌 이펙트. 이 오브젝트 아래의 파티클과 VFX를 모두 다룬다.\n" +
                     "Play On Awake는 꺼둘 것 — 여기서 켜기 전에 이미 돌고 있게 된다.")]
            public GameObject Effect;

            [Tooltip("무엇을 보고 켤지.")]
            public AircraftCondition When;

            [Tooltip("Debuff를 고른 경우에만 쓴다. 어느 디버프인지.")]
            public DebuffDefinition Debuff;
        }

        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("이펙트")]
        [SerializeField] private List<Emitter> _emitters = new();

        // 켜고 끌 것들을 미리 모아둔다. 매 프레임 계층을 뒤지면 이펙트 수만큼 비용이 쌓인다.
        private readonly List<ParticleSystem[]> _particles = new();
        private readonly List<VisualEffect[]> _visuals = new();
        private readonly List<bool> _playing = new();

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(AircraftEffects)}: 기체를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            foreach (Emitter emitter in _emitters)
            {
                GameObject target = emitter.Effect;

                _particles.Add(target != null
                    ? target.GetComponentsInChildren<ParticleSystem>(includeInactive: true)
                    : Array.Empty<ParticleSystem>());

                _visuals.Add(target != null
                    ? target.GetComponentsInChildren<VisualEffect>(includeInactive: true)
                    : Array.Empty<VisualEffect>());

                // 시작할 때 꺼둔다. 에디터에서 돌려보다 켜둔 채 저장하면, 조건이
                // 참이 되기 전부터 나오는 이유를 알 수 없다.
                _playing.Add(true);
            }

            for (int i = 0; i < _emitters.Count; i++)
            {
                Set(i, false);
            }
        }

        private void Update()
        {
            for (int i = 0; i < _emitters.Count; i++)
            {
                Emitter emitter = _emitters[i];

                Set(i, AircraftConditions.IsMet(_aircraft, emitter.When, emitter.Debuff));
            }
        }

        /// <summary>
        /// 켜고 끈다. 이미 그 상태면 건드리지 않는다.
        /// <para>
        /// 매 프레임 <c>Play</c>를 다시 부르면 이펙트가 처음부터 되감겨서, 이어져야 할
        /// 불꽃이 한 프레임짜리 조각들로 끊긴다.
        /// </para>
        /// </summary>
        private void Set(int index, bool on)
        {
            if (_playing[index] == on)
            {
                return;
            }

            _playing[index] = on;

            foreach (ParticleSystem particle in _particles[index])
            {
                if (on)
                {
                    particle.Play(withChildren: false);
                }
                else
                {
                    // 이미 나온 알갱이는 살려둔다. 즉시 지우면 총구 화염이 손을 떼는
                    // 순간 툭 사라져서, 멎은 것이 아니라 지워진 것으로 보인다.
                    particle.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            foreach (VisualEffect visual in _visuals[index])
            {
                if (on)
                {
                    visual.Play();
                }
                else
                {
                    visual.Stop();
                }
            }
        }
    }
}
