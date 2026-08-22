using Adler.Flight;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 부스터 연료에 대한 경고음.
    /// <para>
    /// 게이지는 화면 구석에 있고, 부스터를 쓰는 순간은 대개 앞을 봐야 하는 때다.
    /// 눈으로 확인해야만 알 수 있는 정보는 정작 필요한 순간에 읽히지 않는다.
    /// </para>
    /// <para>
    /// 두 가지를 알린다. 바닥나 가고 있다는 것과, 밟았는데 거절당했다는 것.
    /// 앞의 것은 아껴 쓸 시간을 주고, 뒤의 것은 키가 안 먹은 것과 구분해 준다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoostAudio : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("소리")]
        [Tooltip("경고음이 나올 소스. Loop은 끄고 Play On Awake도 끌 것.")]
        [SerializeField] private AudioSource _source;

        [Tooltip("연료가 얼마 안 남았을 때 되풀이되는 소리. 짧은 삑 소리가 맞다.")]
        [SerializeField] private AudioClip _low;

        [Tooltip("밟았는데 못 쓸 때 한 번 나는 소리. 거절당했다는 짧은 신호.")]
        [SerializeField] private AudioClip _denied;

        [Header("경고 기준")]
        [Tooltip("남은 연료가 이 아래로 내려가면 경고한다.\n" +
                 "게이지가 노랗게 변하는 값과 맞춰야 눈과 귀가 같은 이야기를 한다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowThreshold = 0.3f;

        [Tooltip("체크하면 부스터를 밟고 있을 때만 경고한다.")]
        [SerializeField] private bool _onlyWhileBoosting = true;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.5f;

        private BoostFuel _fuel;
        private IFlightModel _model;
        private bool _lowAnnounced;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _fuel = _aircraft != null ? _aircraft.Boost : null;

            if (_fuel == null || _source == null)
            {
                Debug.LogError($"{nameof(BoostAudio)}: 부스터 또는 Audio Source가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _source.loop = false;
            _source.playOnAwake = false;
        }

        private void OnEnable() => _fuel.Denied += OnDenied;

        private void OnDisable() => _fuel.Denied -= OnDenied;

        private void OnDenied(BoostFuel fuel)
        {
            if (_denied != null)
            {
                _source.PlayOneShot(_denied, _volume);
            }
        }

        private void Update()
        {
            if (_low == null)
            {
                return;
            }

            _model ??= _aircraft.Model;

            bool boosting = _model != null && _model.IsBoosting;
            bool warn = _fuel.Normalized <= _lowThreshold
                        && !_fuel.IsLockedOut
                        && (!_onlyWhileBoosting || boosting);

            // 들어서는 순간 한 번만 알린다. 되풀이하면 그 구간 내내 울려서, 알림이
            // 아니라 배경이 되고 정작 다른 소리를 덮는다.
            if (!warn)
            {
                // 벗어나면 다시 알릴 수 있게 풀어둔다. 연료를 채웠다 또 태우면
                // 그때는 새로 알려야 할 일이다.
                _lowAnnounced = false;
                return;
            }

            if (_lowAnnounced)
            {
                return;
            }

            _lowAnnounced = true;
            _source.PlayOneShot(_low, _volume);
        }
    }
}
