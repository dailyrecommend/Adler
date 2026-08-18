using Adler.Flight;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 남은 부스터 연료를 화면 요소에 꽂아준다.
    /// <para>
    /// 모양은 건드리지 않는다. 만들어 둔 게이지와 텍스트를 넣으면 값만 채우고,
    /// 비워둔 칸은 건너뛴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoostReadout : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private BoostFuel _fuel;

        [Header("게이지")]
        [Tooltip("남은 비율을 채움량으로 넣을 이미지. Image Type을 Filled로 둘 것.")]
        [SerializeField] private Image _fill;

        [Header("숫자")]
        [SerializeField] private TMP_Text _label;

        [Tooltip("{0}에 남은 비율이 0~100으로 들어간다. 예: \"{0:0}%\"")]
        [SerializeField] private string _format = "{0:0}%";

        [Header("색")]
        [SerializeField] private Color _normalColor = new Color(0.5f, 0.8f, 1f, 1f);

        [Tooltip("바닥나서 잠긴 동안의 색. 다시 쓸 수 있게 되면 원래대로 돌아온다.")]
        [SerializeField] private Color _lockedColor = new Color(1f, 0.35f, 0.3f, 1f);

        [Tooltip("연료가 이 비율 아래일 때의 색. 0이면 쓰지 않는다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowThreshold = 0.3f;

        [SerializeField] private Color _lowColor = new Color(1f, 0.75f, 0.25f, 1f);

        [Header("경고")]
        [Tooltip("바닥나서 못 쓰는 동안 켤 요소. 비워둬도 된다.")]
        [SerializeField] private GameObject _lockedWarning;

        private void Awake()
        {
            if (_fuel == null)
            {
                Debug.LogError($"{nameof(BoostReadout)}: Boost Fuel이 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _fuel.Changed += Refresh;
            Refresh(_fuel);
        }

        private void OnDisable() => _fuel.Changed -= Refresh;

        /// <summary>값이 바뀔 때만 부른다. 쓰지도 차지도 않는 동안에는 아무 일도 하지 않는다.</summary>
        private void Refresh(BoostFuel fuel)
        {
            float normalized = fuel.Normalized;

            if (_fill != null)
            {
                _fill.fillAmount = normalized;
            }

            if (_label != null)
            {
                _label.SetText(string.Format(_format, normalized * 100f));
            }

            // 잠김이 잔량 부족보다 우선한다. 못 쓰는 상태를 아는 것이 먼저다.
            Color color = fuel.IsLockedOut
                ? _lockedColor
                : (_lowThreshold > 0f && normalized <= _lowThreshold ? _lowColor : _normalColor);

            if (_fill != null)
            {
                _fill.color = color;
            }

            if (_label != null)
            {
                _label.color = color;
            }

            if (_lockedWarning != null && _lockedWarning.activeSelf != fuel.IsLockedOut)
            {
                _lockedWarning.SetActive(fuel.IsLockedOut);
            }
        }
    }
}
