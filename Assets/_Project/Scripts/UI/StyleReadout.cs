using Adler.Combat;
using Adler.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 전투 점수를 화면 요소에 꽂아준다.
    /// <para>
    /// 랭크 글자는 바뀔 때만 다시 쓰고, 게이지는 매 프레임 따라간다 — 글자는 사건이고
    /// 게이지는 흐름이라 박자가 다르다. 랭크가 오르면 글자를 부풀렸다 놓아, 화면을
    /// 안 보고 있어도 곁눈에 걸리게 한다.
    /// </para>
    /// <para>
    /// 모양은 건드리지 않는다. 만들어 둔 텍스트와 이미지를 넣으면 값만 채우고,
    /// 비워둔 칸은 건너뛴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StyleReadout : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private StyleMeter _meter;

        [Header("랭크")]
        [Tooltip("랭크 글자가 들어갈 곳.")]
        [SerializeField] private TMP_Text _rankLabel;

        [Tooltip("다음 랭크까지 차오른 정도. Filled로 두고 Fill Origin은 Bottom.\n" +
                 "이쪽이 진짜 게이지다 — 점수가 오르면 이것이 차오른다.")]
        [SerializeField] private Image _fill;

        [Tooltip("차오른 것의 나머지를 덮는 여백. Filled로 두고 Fill Origin은 Top.\n\n" +
                 "게이지와 반대로 움직인다 — 게이지가 0.3 차면 이것은 0.7 남는다.\n" +
                 "둘이 마주 닿아 틈 없이 한 기둥이 되므로, 비워둔 칸은 건너뛴다.")]
        [SerializeField] private Image _remainder;

        [Header("랭크가 오를 때")]
        [Tooltip("글자가 부풀어 오르는 배율. 1이면 연출하지 않는다.")]
        [Min(1f)]
        [SerializeField] private float _punchScale = 1.4f;

        [Tooltip("부푼 상태에서 제자리로 돌아오는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _punchSeconds = 0.18f;

        private RectTransform _labelRect;
        private Vector3 _labelBaseScale = Vector3.one;
        private float _punchRemaining;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);

            if (_meter == null)
            {
                Debug.LogError($"{nameof(StyleReadout)}: 점수판을 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (_rankLabel != null)
            {
                _labelRect = _rankLabel.rectTransform;
                _labelBaseScale = _labelRect.localScale;
            }
        }

        private void OnEnable()
        {
            _meter.RankChanged += OnRankChanged;
            Show();
        }

        private void OnDisable()
        {
            _meter.RankChanged -= OnRankChanged;
        }

        /// <summary>
        /// 오를 때만 부풀린다. 떨어질 때도 튀면 잘된 일과 잘못된 일이
        /// 같은 신호로 나가서, 곁눈으로는 구별할 수 없게 된다.
        /// </summary>
        private void OnRankChanged(int from, int to)
        {
            Show();

            if (to > from && _labelRect != null && _punchScale > 1f)
            {
                _punchRemaining = _punchSeconds;
            }
        }

        private void Show()
        {
            _rankLabel?.SetText(_meter.RankName);
        }

        private void Update()
        {
            float progress = _meter.RankProgress;

            if (_fill != null)
            {
                _fill.fillAmount = progress;
            }

            if (_remainder != null)
            {
                _remainder.fillAmount = 1f - progress;
            }

            if (_labelRect == null || _punchRemaining <= 0f)
            {
                return;
            }

            _punchRemaining = Mathf.Max(0f, _punchRemaining - _clock.Delta);

            float t = _punchRemaining / _punchSeconds;
            _labelRect.localScale = _labelBaseScale * Mathf.Lerp(1f, _punchScale, t * t);
        }
    }
}
