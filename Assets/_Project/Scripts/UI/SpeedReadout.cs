using Adler.Flight;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 기체의 속도와 스로틀을 화면 요소에 꽂아준다.
    /// <para>
    /// 모양은 전혀 건드리지 않는다. 만들어 둔 텍스트와 이미지를 아래 칸에 넣으면
    /// 값만 채워 넣는다. 쓰지 않을 칸은 비워두면 그 부분은 건너뛴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeedReadout : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("속도")]
        [SerializeField] private TMP_Text _speedLabel;

        [Tooltip("{0}에 속도가 들어간다. 예: \"{0:0} km/h\"")]
        [SerializeField] private string _speedFormat = "{0:0}";

        [Tooltip("화면에 띄울 때 실제 속도에 곱하는 값.\n" +
                 "기체가 1m라 실제 수치는 작지만, 계기판에는 전투기다운 숫자가 찍혀야 한다.\n" +
                 "10이면 부스터 속도 32가 320으로 표시된다.")]
        [SerializeField] private float _speedDisplayScale = 10f;

        [Header("부스터 강조")]
        [Tooltip("평상시 속도 글자색.")]
        [SerializeField] private Color _speedColor = Color.white;

        [Tooltip("부스터를 쓰는 동안의 속도 글자색.")]
        [SerializeField] private Color _boostSpeedColor = new Color(1f, 0.55f, 0.15f, 1f);

        [Tooltip("속도를 0~1로 환산해 채움량으로 넣을 이미지. Image Type을 Filled로 둘 것.")]
        [SerializeField] private Image _speedFill;

        [Tooltip("채움량이 1이 되는 속도. 0 이하면 기체의 부스터 속도를 따라간다.")]
        [SerializeField] private float _speedFillMax;

        [Header("스로틀")]
        [SerializeField] private TMP_Text _throttleLabel;

        [Tooltip("{0}에 스로틀이 0~100으로 들어간다. 예: \"THR {0:0}%\"")]
        [SerializeField] private string _throttleFormat = "{0:0}%";

        [SerializeField] private Image _throttleFill;

        // 값이 그대로인데 문자열을 다시 만들면 매 프레임 쓰레기가 쌓인다.
        // 표시되는 자릿수가 바뀔 때만 갱신한다.
        private int _lastSpeedShown = int.MinValue;
        private int _lastThrottleShown = int.MinValue;
        private bool _lastBoosting;

        private void Awake()
        {
            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(SpeedReadout)}: Aircraft가 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            IFlightModel model = _aircraft.Model;
            if (model == null)
            {
                return;
            }

            UpdateSpeed(model);
            UpdateThrottle(model);
        }

        private void UpdateSpeed(IFlightModel model)
        {
            float speed = model.Speed;

            if (_speedLabel != null)
            {
                // 표시용으로만 부풀린다. 비행과 사격 계산은 언제나 실제 값으로 돌아간다.
                float displayed = speed * _speedDisplayScale;

                int rounded = Mathf.RoundToInt(displayed);
                if (rounded != _lastSpeedShown)
                {
                    _lastSpeedShown = rounded;
                    _speedLabel.SetText(string.Format(_speedFormat, displayed));
                }

                if (model.IsBoosting != _lastBoosting)
                {
                    _lastBoosting = model.IsBoosting;
                    _speedLabel.color = _lastBoosting ? _boostSpeedColor : _speedColor;
                }
            }

            if (_speedFill != null)
            {
                float max = ResolveSpeedFillMax();
                _speedFill.fillAmount = max > 0f ? Mathf.Clamp01(speed / max) : 0f;
            }
        }

        private void UpdateThrottle(IFlightModel model)
        {
            // 스로틀 레버가 없어져서 이 게이지는 이제 실제 속도를 보여준다. 게이지가
            // 하던 일 — 지금 얼마나 밀어붙이고 있는지 — 은 그대로다.
            float throttle = model.SpeedNormalized;

            if (_throttleLabel != null)
            {
                int percent = Mathf.RoundToInt(throttle * 100f);
                if (percent != _lastThrottleShown)
                {
                    _lastThrottleShown = percent;
                    _throttleLabel.SetText(string.Format(_throttleFormat, percent));
                }
            }

            if (_throttleFill != null)
            {
                _throttleFill.fillAmount = Mathf.Clamp01(throttle);
            }
        }

        private float ResolveSpeedFillMax()
        {
            if (_speedFillMax > 0f)
            {
                return _speedFillMax;
            }

            // 정비로 순항 속도가 바뀌면 눈금도 따라가야 한다.
            return _aircraft.Stats != null ? _aircraft.Stats.TopSpeed : 0f;
        }
    }
}
