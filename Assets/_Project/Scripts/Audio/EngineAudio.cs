using Adler.Core;
using Adler.Flight;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 엔진 소리. 순항음과 부스터음을 겹쳐 두고 속도에 맞춰 섞는다.
    /// <para>
    /// 소리를 갈아 끼우지 않고 두 겹을 함께 틀어둔 채 크기만 바꾼다. 갈아 끼우면
    /// 부스터를 켜고 끌 때마다 파형이 끊겨 뚝뚝 소리가 나는데, 부스터는 자주 쓰는
    /// 것이라 그 끊김이 계속 들린다.
    /// </para>
    /// <para>
    /// 속도로 음높이를 움직이는 것이 핵심이다. 크기만 바꾸면 소리가 커질 뿐 빨라지지
    /// 않아서, 화면은 빠른데 귀는 그대로인 어긋남이 생긴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EngineAudio : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("소리")]
        [Tooltip("늘 도는 엔진음. Loop을 켜둘 것.")]
        [SerializeField] private AudioSource _cruise;

        [Tooltip("부스터를 쓸 때 겹쳐지는 소리. Loop을 켜고 Volume은 0으로 둘 것.")]
        [SerializeField] private AudioSource _boost;

        [Tooltip("부스터를 밟는 순간 한 번 울릴 소리. 비워둬도 된다.")]
        [SerializeField] private AudioSource _boostIgnition;

        [Header("음높이")]
        [Tooltip("가장 느릴 때의 음높이.")]
        [Range(0.1f, 3f)]
        [SerializeField] private float _minPitch = 0.75f;

        [Tooltip("가장 빠를 때의 음높이. 2를 넘기면 대개 우스워진다.")]
        [Range(0.1f, 3f)]
        [SerializeField] private float _maxPitch = 1.5f;

        [Header("크기")]
        [Range(0f, 1f)]
        [SerializeField] private float _cruiseVolume = 0.5f;

        [Range(0f, 1f)]
        [SerializeField] private float _boostVolume = 0.8f;

        [Header("전환")]
        [Tooltip("부스터 소리가 올라오는 속도. 밟는 것은 즉발이므로 빠른 편이 맞다.")]
        [Min(0.1f)]
        [SerializeField] private float _boostFadeIn = 12f;

        [Tooltip("부스터 소리가 빠지는 속도. 느리게 두면 여운이 남는다.")]
        [Min(0.1f)]
        [SerializeField] private float _boostFadeOut = 3f;

        private float _boostBlend;
        private bool _wasBoosting;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null || _cruise == null)
            {
                Debug.LogError($"{nameof(EngineAudio)}: Aircraft 또는 Cruise 소스가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            Prepare(_cruise);
            Prepare(_boost);

            if (_boost != null)
            {
                _boost.volume = 0f;
            }
        }

        /// <summary>
        /// 끊기지 않게 미리 틀어둔다.
        /// <para>
        /// 계속 돌려두고 크기만 여닫는다. 필요할 때마다 Play와 Stop을 부르면 파형이
        /// 그 자리에서 잘려 딸깍하는 소리가 나는데, 부스터는 자주 쓰는 것이라 그
        /// 끊김이 내내 들린다.
        /// </para>
        /// <para>
        /// 처음부터 다시 듣고 싶을 때는 멈췄다 트는 대신 재생 위치만 되돌린다.
        /// </para>
        /// </summary>
        private static void Prepare(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.loop = true;
            source.playOnAwake = false;

            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void Update()
        {
            IFlightModel model = _aircraft.Model;

            if (model == null)
            {
                return;
            }

            UpdateBoostBlend(model.IsBoosting);

            // 얼어붙으면 엔진이 죽은 것이라 소리도 함께 잦아든다.
            float power = model.IsFrozen ? 0f : 1f;
            float pitch = Mathf.Lerp(_minPitch, _maxPitch, model.SpeedNormalized);

            _cruise.pitch = pitch;
            _cruise.volume = _cruiseVolume * power;

            if (_boost == null)
            {
                return;
            }

            _boost.pitch = pitch;
            _boost.volume = _boostVolume * _boostBlend * power;

            // 들려야 하는데 멈춰 있으면 다시 튼다. Awake에서 클립이 아직 없었거나
            // 무언가가 중간에 멈췄어도 여기서 되살아난다 — 소리가 안 나는 원인 중
            // 가장 찾기 어려운 것이 "틀어놨다고 생각했는데 안 돌고 있던" 경우다.
            if (_boost.volume > 0.001f && !_boost.isPlaying && _boost.clip != null)
            {
                _boost.Play();
            }
        }

        private void UpdateBoostBlend(bool boosting)
        {
            if (boosting && !_wasBoosting)
            {
                // 재생 위치를 처음으로 되돌린다. 계속 돌던 자리에서 볼륨만 열면 분사가
                // 이미 한창인 상태로 드러나서, 밟은 순간과 소리의 시작점이 어긋난다.
                //
                // Play()를 다시 부르지 않는 이유는 이미 돌고 있기 때문이다. 위치만
                // 옮기면 파형이 끊기지 않아 딸깍하는 소리가 나지 않는다.
                if (_boost != null)
                {
                    _boost.time = 0f;
                }

                if (_boostIgnition != null)
                {
                    _boostIgnition.Play();
                }
            }

            _wasBoosting = boosting;

            float speed = boosting ? _boostFadeIn : _boostFadeOut;
            _boostBlend = Mathf.Lerp(
                _boostBlend, boosting ? 1f : 0f, 1f - Mathf.Exp(-speed * _clock.Delta));
        }
    }
}
