using System;
using System.Collections.Generic;
using Adler.Flight;
using Adler.UI;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 타격의 무게에 맞춰 한 번짜리 소리를 낸다.
    /// <para>
    /// <see cref="AircraftSounds"/>와 나뉘어 있는 것은 보고 있는 것이 다르기 때문이다.
    /// 그쪽은 이어지는 상태를 보고 켜고 끄지만, 이쪽이 듣는 것은 일어난 사건이라
    /// 끄는 일 자체가 없다.
    /// </para>
    /// <para>
    /// 여기서 내는 것은 <b>때린 쪽이 듣는 소리</b>다. 맞은 쪽이 내는 소리는 그쪽의
    /// 내구도에 붙어 따로 난다 — 같은 한 방이라도 때린 손맛과 맞은 비명은 다른
    /// 이야기라, 한곳에서 내면 둘 중 하나의 사정에 다른 하나가 끌려간다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactAudio : MonoBehaviour
    {
        /// <summary>한 번의 타격 안에서 언제 낼지.</summary>
        public enum Moment
        {
            /// <summary>부딪히는 그 순간. 시간이 멎기 시작하는 때이기도 하다.</summary>
            OnHit,

            /// <summary>
            /// 멎어 있던 시간이 다시 흐르기 시작할 때.
            /// <para>
            /// 시간을 늦추지 않는 가벼운 명중에는 이 순간이 없다. 그런 줄은 영영 나지 않는다.
            /// </para>
            /// </summary>
            OnRelease,
        }

        [Serializable]
        public struct Cue
        {
            [Tooltip("어떤 타격에 낼지. 같은 값을 여러 줄에 두면 전부 난다.")]
            public ImpactWeight When;

            [Tooltip("그 타격의 어느 순간에 낼지.\n\n" +
                     "멎는 순간과 풀리는 순간에 각각 하나씩 두면, 멎어 있던 그 짧은\n" +
                     "시간이 뜸이 된다 — 들이켰다가 내리치는 셈이다.")]
            public Moment At;

            [Tooltip("낼 소리.")]
            public AudioClip Clip;

            [Range(0f, 1f)]
            [Tooltip("이 줄의 크기.")]
            public float Volume;

            [Tooltip("음높이가 흔들리는 폭. 0이면 매번 똑같이 들려 기계처럼 된다.\n" +
                     "연달아 나올 수 있는 소리일수록 조금 줘야 한 소리가 겹친 것으로 들리지 않는다.")]
            [Range(0f, 0.5f)]
            public float PitchJitter;
        }

        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("타격을 알리는 쪽. 비워두면 기체 아래에서 찾는다.")]
        [SerializeField] private HitImpact _impact;

        [Header("소리")]
        [Tooltip("소리가 나올 소스. Loop은 끄고 Play On Awake도 끌 것.")]
        [SerializeField] private AudioSource _source;

        [SerializeField] private List<Cue> _cues = new();

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_impact == null && _aircraft != null)
            {
                _impact = _aircraft.GetComponentInChildren<HitImpact>(includeInactive: true);
            }

            if (_impact == null || _source == null)
            {
                Debug.LogError($"{nameof(ImpactAudio)}: {nameof(HitImpact)} 또는 Audio Source가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _source.loop = false;
            _source.playOnAwake = false;
        }

        private void OnEnable()
        {
            _impact.Impact += OnImpact;
            _impact.Released += OnReleased;
        }

        private void OnDisable()
        {
            _impact.Impact -= OnImpact;
            _impact.Released -= OnReleased;
        }

        private void OnImpact(ImpactWeight weight) => Play(weight, Moment.OnHit);

        private void OnReleased(ImpactWeight weight) => Play(weight, Moment.OnRelease);

        /// <summary>
        /// 맞는 줄을 <b>전부</b> 낸다. 하나를 찾고 멈추지 않는다.
        /// <para>
        /// 한 순간이 여러 소리로 이루어지는 일이 흔하다 — 부딪히는 소리와 부서지는
        /// 소리는 다른 클립이고, 겹쳐 들려야 한 방으로 읽힌다. 한 줄만 내면 둘 중
        /// 하나를 고르는 셈이 된다.
        /// </para>
        /// </summary>
        private void Play(ImpactWeight weight, Moment moment)
        {
            foreach (Cue cue in _cues)
            {
                if (cue.When != weight || cue.At != moment || cue.Clip == null)
                {
                    continue;
                }

                // 음높이는 소스에 걸린다. PlayOneShot에는 음높이 인자가 없어서, 이 줄을
                // 낼 때마다 소스 쪽을 흔들어 준다 — 이미 나가고 있는 소리는 자기가
                // 시작할 때의 값을 들고 가므로 뒤엣것이 앞엣것을 비틀지 않는다.
                _source.pitch = 1f + UnityEngine.Random.Range(-cue.PitchJitter, cue.PitchJitter);
                _source.PlayOneShot(cue.Clip, cue.Volume);
            }
        }
    }
}
