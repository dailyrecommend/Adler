using Adler.Combat;
using Adler.Flight;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 기체의 남은 내구도를 화면 요소에 꽂아준다.
    /// <para>
    /// 피격 순간에 요란하게 반응하지 않는다. 맞았다는 사실은 화면 흔들림이 이미
    /// 알려주고, 이쪽이 답해야 할 것은 <em>얼마나 남았는가</em>다. 그건 상태이므로
    /// 조용히 떠 있다가 눈길이 갈 때 읽히면 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthReadout : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("게이지")]
        [Tooltip("남은 비율을 채움량으로 넣을 이미지. Image Type을 Filled로 둘 것.")]
        [SerializeField] private Image _fill;

        [Header("숫자")]
        [SerializeField] private TMP_Text _label;

        [Tooltip("{0}에 남은 값, {1}에 최대치, {2}에 백분율이 들어간다. 예: \"{2:0}%\"")]
        [SerializeField] private string _format = "{2:0}%";

        [Header("색")]
        [SerializeField] private Color _normalColor = Color.white;

        [Tooltip("이 비율 아래로 떨어지면 색이 바뀐다. 0이면 쓰지 않는다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowThreshold = 0.35f;

        [SerializeField] private Color _lowColor = new Color(1f, 0.4f, 0.3f, 1f);

        [Tooltip("위험할 때 켤 요소. 비워둬도 된다.")]
        [SerializeField] private GameObject _lowWarning;

        private Health _health;
        private bool _wasLow;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _health = _aircraft != null ? _aircraft.Health : null;

            if (_health == null)
            {
                Debug.LogError($"{nameof(HealthReadout)}: 기체의 내구도를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnChanged;
            _health.Revived += OnRevived;
            _health.Healed += OnHealed;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnChanged;
            _health.Revived -= OnRevived;
            _health.Healed -= OnHealed;
        }

        /// <summary>
        /// 첫 표시는 Start에서 한다.
        /// <para>
        /// Health는 자기 Awake에서 내구도를 채우는데, 오브젝트가 다르면 Awake와 OnEnable의
        /// 순서가 보장되지 않는다. OnEnable에서 읽으면 아직 0인 값을 가져올 수 있고,
        /// 그 뒤로는 피격 전까지 다시 읽지 않으니 0인 채로 굳어버린다.
        /// </para>
        /// </summary>
        private void Start() => Refresh();

        private void OnDamaged(Health health, DamageInfo damage, DamageResult result) => Refresh();

        private void OnChanged(Health health, DamageInfo damage) => Refresh();

        private void OnRevived(Health health) => Refresh();

        private void OnHealed(Health health, float amount) => Refresh();

        /// <summary>
        /// 값이 바뀔 때만 부른다. 되살아났을 때는 신호가 없으므로 밖에서 부를 수 있게 열어둔다.
        /// </summary>
        public void Refresh()
        {
            float normalized = _health.Normalized;

            if (_fill != null)
            {
                _fill.fillAmount = normalized;
            }

            if (_label != null)
            {
                _label.SetText(string.Format(
                    _format, _health.Current, _health.Max, normalized * 100f));
            }

            bool low = _lowThreshold > 0f && normalized <= _lowThreshold;
            if (low != _wasLow)
            {
                _wasLow = low;
                ApplyColor(low ? _lowColor : _normalColor);

                if (_lowWarning != null)
                {
                    _lowWarning.SetActive(low);
                }
            }
        }

        private void ApplyColor(Color color)
        {
            if (_fill != null)
            {
                _fill.color = color;
            }

            if (_label != null)
            {
                _label.color = color;
            }
        }
    }
}
