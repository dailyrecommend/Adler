using Adler.Flight;
using TMPro;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 기체의 속도를 화면 요소에 꽂아준다.
    /// <para>
    /// 모양은 전혀 건드리지 않는다. 만들어 둔 텍스트를 아래 칸에 넣으면 값만 채워 넣는다.
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

        // 값이 그대로인데 문자열을 다시 만들면 매 프레임 쓰레기가 쌓인다.
        // 표시되는 자릿수가 바뀔 때만 갱신한다.
        private int _lastSpeedShown = int.MinValue;

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
            if (model == null || _speedLabel == null)
            {
                return;
            }

            // 표시용으로만 부풀린다. 비행과 사격 계산은 언제나 실제 값으로 돌아간다.
            float displayed = model.Speed * _speedDisplayScale;

            int rounded = Mathf.RoundToInt(displayed);
            if (rounded == _lastSpeedShown)
            {
                return;
            }

            _lastSpeedShown = rounded;
            _speedLabel.SetText(string.Format(_speedFormat, displayed));
        }
    }
}
