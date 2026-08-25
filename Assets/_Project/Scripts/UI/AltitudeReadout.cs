using Adler.Flight;
using TMPro;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 기체의 고도를 화면 요소에 꽂아준다.
    /// <para>
    /// 모양은 전혀 건드리지 않는다. 만들어 둔 텍스트를 아래 칸에 넣으면 값만 채워 넣는다.
    /// </para>
    /// <para>
    /// 지면까지 재는 대신 높이를 그대로 읽는다. <see cref="AltitudeFreeze"/>가 얼어붙는
    /// 높이를 그렇게 정하고 있어서, 계기판이 다른 축을 읽으면 둘이 어긋난다 — 지형에
    /// 굴곡이 생기는 순간 화면의 숫자로는 기준선까지 얼마나 남았는지 알 수 없게 된다.
    /// 고도는 게임 안에 하나여야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltitudeReadout : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("고도")]
        [SerializeField] private TMP_Text _altitudeLabel;

        [Tooltip("{0}에 고도가 들어간다. 예: \"{0:0} m\"")]
        [SerializeField] private string _altitudeFormat = "{0:0}";

        [Tooltip("0으로 읽힐 높이. 지면 오브젝트의 y를 넣으면 착지했을 때 0이 찍힌다.\n" +
                 "여기를 건드려도 얼어붙는 높이는 달라지지 않는다 — 이건 표시용 눈금일 뿐이다.")]
        [SerializeField] private float _seaLevel;

        [Tooltip("화면에 띄울 때 실제 높이에 곱하는 값.\n" +
                 "기체가 1m라 실제 수치는 작지만, 계기판에는 전투기다운 숫자가 찍혀야 한다.\n" +
                 "속도계와 같은 값을 써야 둘이 같은 세계의 숫자로 읽힌다.")]
        [SerializeField] private float _altitudeDisplayScale = 10f;

        // 값이 그대로인데 문자열을 다시 만들면 매 프레임 쓰레기가 쌓인다.
        // 표시되는 자릿수가 바뀔 때만 갱신한다.
        private int _lastShown = int.MinValue;

        private void Awake()
        {
            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(AltitudeReadout)}: Aircraft가 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_altitudeLabel == null)
            {
                return;
            }

            // 표시용으로만 부풀린다. 얼어붙는 판정은 언제나 실제 높이로 돌아간다.
            float displayed = (_aircraft.transform.position.y - _seaLevel) * _altitudeDisplayScale;

            int rounded = Mathf.RoundToInt(displayed);
            if (rounded == _lastShown)
            {
                return;
            }

            _lastShown = rounded;
            _altitudeLabel.SetText(string.Format(_altitudeFormat, displayed));
        }
    }
}
