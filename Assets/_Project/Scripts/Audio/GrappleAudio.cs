using Adler.Core;
using Adler.Flight;
using Adler.Weapons;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 그래플링의 소리. 줄 한 번이 거치는 여섯 순간을 각각 맡는다.
    /// <para>
    /// 던지고 · 날아가고 · 물리고 · 채이고 · 끌려가고 · 끊어진다. 이 중 넷은 한
    /// 순간이고 둘은 이어지는 소리인데, 한 순간짜리는 신호를 듣고 울리고 이어지는
    /// 것은 상태를 보고 맞춘다.
    /// </para>
    /// <para>
    /// 이렇게 나누는 이유는 그래플을 거는 순간이 대개 적을 보고 있는 때이기
    /// 때문이다. 화면 구석을 확인할 겨를이 없으므로, 지금 어디까지 갔는지가
    /// 귀만으로 따라와야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrappleAudio : MonoBehaviour
    {
        /// <summary>지금 이어지고 있어야 할 소리.</summary>
        private enum Sustain
        {
            None,
            Flight,
            Reel,
        }

        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("소리를 낼 그래플링. 비워두면 기체 아래에서 찾는다.")]
        [SerializeField] private GrapplingHook _hook;

        [Header("한 번씩 울리는 소리")]
        [Tooltip("소스. Loop은 끄고 Play On Awake도 끌 것.")]
        [SerializeField] private AudioSource _source;

        [Tooltip("① 던질 때. 짧게 치고 나가는 소리.")]
        [SerializeField] private AudioClip _fire;

        [Tooltip("③ 갈고리가 표적에 닿아 물릴 때. 딱 걸리는 소리.")]
        [SerializeField] private AudioClip _arrive;

        [Tooltip("④ 줄이 팽팽해져 끌기 시작할 때. 확 채이는 소리.\n" +
                 "물리는 소리와 달라야 걸린 것과 끌려가는 것이 구분된다.")]
        [SerializeField] private AudioClip _pull;

        [Tooltip("⑥ 줄이 끊어질 때.")]
        [SerializeField] private AudioClip _release;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.7f;

        [Header("이어지는 소리")]
        [Tooltip("아래 두 소리가 번갈아 나올 소스.\n" +
                 "Play On Awake는 끌 것. Loop과 클립은 코드가 넣는다.")]
        [SerializeField] private AudioSource _sustained;

        [Tooltip("② 날아가는 동안. 줄이 풀려 나가는 소리.\n" +
                 "멀리 던질수록 오래 들리므로, 이 소리의 길이가 곧 거리가 된다.")]
        [SerializeField] private AudioClip _flightLoop;

        [Range(0f, 1f)]
        [SerializeField] private float _flightVolume = 0.5f;

        [Tooltip("⑤ 끌려가는 동안. 줄이 감기는 소리.")]
        [SerializeField] private AudioClip _reelLoop;

        [Range(0f, 1f)]
        [SerializeField] private float _reelVolume = 0.6f;

        [Tooltip("끊어질 때 소리가 잦아드는 시간(초).\n" +
                 "뚝 끊으면 끊어지는 소리보다 그 자리의 정적이 먼저 들린다.")]
        [Min(0f)]
        [SerializeField] private float _fadeOut = 0.15f;

        private Sustain _playing;

        // 실제로 붙어 있는 갈고리. 인스펙터 칸과 달리 장비 교체를 따라간다.
        private GrapplingHook _bound;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            // 갈고리는 여기서 찾지 않는다. 장비라서 이 Awake보다 늦게 태어나고,
            // 안 실었을 수도 있다 — 어느 쪽이든 잘못이 아니다.
            if (_source == null)
            {
                Debug.LogError($"{nameof(GrappleAudio)}: Audio Source가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _source.loop = false;
            _source.playOnAwake = false;

            if (_sustained != null)
            {
                _sustained.loop = true;
                _sustained.playOnAwake = false;
                _sustained.volume = 0f;
            }
        }

        private void OnEnable()
        {
            WeaponBay bay = _aircraft != null ? _aircraft.Weapons : null;
            if (bay != null)
            {
                bay.Rearmed += OnRearmed;
            }

            Bind();
        }

        private void OnDisable()
        {
            WeaponBay bay = _aircraft != null ? _aircraft.Weapons : null;
            if (bay != null)
            {
                bay.Rearmed -= OnRearmed;
            }

            Unbind();
            Silence();
        }

        /// <summary>
        /// 장비를 갈아입었다. 갈고리가 실렸는지부터 다시 본다 —
        /// 내렸을 수도, 방금 실렸을 수도 있다.
        /// </summary>
        private void OnRearmed()
        {
            Unbind();
            Bind();
            Silence();
        }

        /// <summary>
        /// 갈고리를 찾아 붙는다. 안 실려 있으면 조용히 빈손으로 있는다 —
        /// 장비를 내린 것은 잘못이 아니므로 오류를 뱉지 않는다.
        /// </summary>
        private void Bind()
        {
            _bound = _hook != null ? _hook : (_aircraft != null ? _aircraft.Grapple : null);

            if (_bound != null)
            {
                _bound.PhaseChanged += OnPhaseChanged;
            }
        }

        /// <summary>
        /// 참조 비교로 끊는다. 지워진 갈고리는 null인 척을 해서, 보통 비교로는
        /// 끊을 기회 자체가 안 온다.
        /// </summary>
        private void Unbind()
        {
            if (!ReferenceEquals(_bound, null))
            {
                _bound.PhaseChanged -= OnPhaseChanged;
            }

            _bound = null;
        }

        /// <summary>이어지던 소리를 그 자리에서 멈춘다.</summary>
        private void Silence()
        {
            if (_sustained != null)
            {
                _sustained.Stop();
                _sustained.volume = 0f;
            }

            _playing = Sustain.None;
        }

        /// <summary>
        /// 단계가 바뀌는 순간 그 자리에 맞는 소리를 낸다.
        /// <para>
        /// 신호를 넷 받아 각각 대응하지 않고 단계 하나만 본다. 신호가 늘 때마다 잇는
        /// 줄이 늘고, 어느 신호가 어느 순서로 오는지는 여기서 알 수 없어서 소리가
        /// 겹치거나 빠지는 것을 짐작으로만 막게 된다. 단계는 하나뿐이라 그럴 자리가 없다.
        /// </para>
        /// </summary>
        private void OnPhaseChanged(GrapplePhase from, GrapplePhase to)
        {
            switch (to)
            {
                case GrapplePhase.Flying:
                    Play(_fire);
                    break;

                case GrapplePhase.Biting:
                    Play(_arrive);
                    break;

                case GrapplePhase.Pulling:
                    Play(_pull);
                    break;

                case GrapplePhase.Idle:
                    Play(_release);
                    break;
            }
        }

        private void Play(AudioClip clip)
        {
            if (clip != null)
            {
                _source.PlayOneShot(clip, _volume);
            }
        }

        /// <summary>
        /// 이어지는 소리는 신호를 듣지 않고 상태를 본다.
        /// <para>
        /// 걸리고 끊어지는 신호마다 재생을 켜고 끄면, 한 번이라도 어긋났을 때 소리만
        /// 남아 계속 울린다. 지금 어떤 상태인지를 매 프레임 확인해 맞추면 어긋날
        /// 자리가 없다.
        /// </para>
        /// <para>
        /// 물려서 버티는 동안은 어느 쪽도 울리지 않는다. 줄이 늘어져 있는 짧은
        /// 사이라 소리가 비는 것이 맞고, 그 정적이 있어야 뒤이어 채이는 소리가 산다.
        /// </para>
        /// </summary>
        private void Update()
        {
            if (_sustained == null)
            {
                return;
            }

            Sustain wanted = _bound == null ? Sustain.None
                : _bound.Phase == GrapplePhase.Pulling ? Sustain.Reel
                : _bound.Phase == GrapplePhase.Flying ? Sustain.Flight
                : Sustain.None;

            if (wanted != _playing)
            {
                Switch(wanted);
            }

            if (_playing != Sustain.None)
            {
                _sustained.volume = VolumeFor(_playing);
                return;
            }

            FadeOut();
        }

        private void Switch(Sustain wanted)
        {
            _playing = wanted;

            if (wanted == Sustain.None)
            {
                return;
            }

            AudioClip clip = wanted == Sustain.Reel ? _reelLoop : _flightLoop;

            if (clip == null)
            {
                _sustained.Stop();
                return;
            }

            // 처음부터 다시 재생한다. 이어서 틀면 지난번에 끊긴 자리부터 나와
            // 걸 때마다 다른 소리로 시작한다.
            _sustained.clip = clip;
            _sustained.volume = VolumeFor(wanted);
            _sustained.time = 0f;
            _sustained.Play();
        }

        private void FadeOut()
        {
            if (!_sustained.isPlaying)
            {
                return;
            }

            _sustained.volume = _fadeOut > 0f
                ? Mathf.MoveTowards(_sustained.volume, 0f, _clock.Delta / _fadeOut)
                : 0f;

            if (_sustained.volume <= 0f)
            {
                _sustained.Stop();
            }
        }

        private float VolumeFor(Sustain sustain)
            => sustain == Sustain.Reel ? _reelVolume : _flightVolume;
    }
}
