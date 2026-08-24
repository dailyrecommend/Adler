using Adler.Core;
using Adler.Flight;
using Adler.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 남은 탄을 화면 요소에 꽂아준다.
    /// <para>
    /// 모양은 건드리지 않는다. 만들어 둔 텍스트와 이미지를 넣으면 값만 채우고,
    /// 비워둔 칸은 건너뛴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmmoReadout : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("손에 든 무기의 아이콘을 넣을 곳. 비워둬도 된다.")]
        [SerializeField] private Image _weaponIcon;

        [Tooltip("손에 든 무기의 이름을 넣을 곳. 비워둬도 된다.")]
        [SerializeField] private TMP_Text _weaponLabel;

        private WeaponBay _bay;
        private AircraftWeapon _weapon;

        [Header("숫자")]
        [SerializeField] private TMP_Text _label;

        [Tooltip("{0}에 남은 탄, {1}에 최대 탄이 들어간다. 예: \"{0} / {1}\"")]
        [SerializeField] private string _format = "{0}";

        [Header("게이지")]
        [Tooltip("남은 비율을 채움량으로 넣을 이미지. Image Type을 Filled로 둘 것.")]
        [SerializeField] private Image _fill;

        [Header("경고")]
        [Tooltip("남은 탄이 이 비율 아래로 떨어지면 색을 바꾼다. 0이면 쓰지 않는다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowThreshold = 0.2f;

        [SerializeField] private Color _normalColor = Color.white;

        [SerializeField] private Color _lowColor = new Color(1f, 0.4f, 0.3f, 1f);

        [Tooltip("탄이 완전히 떨어졌을 때 켤 요소. 재보급을 부르라는 신호. 비워둬도 된다.")]
        [SerializeField] private GameObject _emptyWarning;

        [Header("숫자 변화 연출")]
        [Tooltip("숫자가 바뀔 때 부풀어 오르는 배율. 1이면 연출하지 않는다.")]
        [Min(1f)]
        [SerializeField] private float _punchScale = 1.12f;

        [Tooltip("탄이 한 번에 크게 늘었을 때의 배율. 재보급처럼 한 발과는 다른 사건에 쓴다.")]
        [Min(1f)]
        [SerializeField] private float _resupplyPunchScale = 1.4f;

        [Tooltip("부푼 상태에서 제자리로 돌아오는 데 걸리는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _punchDuration = 0.12f;

        private bool _wasLow;
        private bool _initialized;
        private int _shownRemaining;

        private RectTransform _labelRect;
        private Vector3 _labelBaseScale = Vector3.one;
        private float _punchRemaining;
        private float _punchAmount = 1f;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _bay = _aircraft != null ? _aircraft.Weapons : null;

            if (_bay == null)
            {
                Debug.LogError($"{nameof(AmmoReadout)}: 기체의 무기를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (_label != null)
            {
                _labelRect = _label.rectTransform;
                _labelBaseScale = _labelRect.localScale;
            }
        }

        private void OnEnable() => _bay.WeaponChanged += OnWeaponChanged;

        private void OnDisable()
        {
            _bay.WeaponChanged -= OnWeaponChanged;
            Unsubscribe();
        }

        /// <summary>
        /// 첫 표시는 Start에서 한다. 무기가 자기 Awake에서 탄을 채우는데, 오브젝트가
        /// 다르면 그 순서가 보장되지 않아 OnEnable에서 읽으면 0을 가져올 수 있다.
        /// </summary>
        private void Start() => OnWeaponChanged(_bay.Active);

        /// <summary>
        /// 손에 든 무기를 따라간다. 표시가 무기 하나에 붙어 있으면 교체한 뒤에도
        /// 이전 무기의 잔탄을 보여주게 된다.
        /// </summary>
        private void OnWeaponChanged(AircraftWeapon weapon)
        {
            Unsubscribe();
            _weapon = weapon;

            if (_weapon == null)
            {
                return;
            }

            _weapon.AmmoChanged += Refresh;

            if (_weaponIcon != null)
            {
                _weaponIcon.sprite = _weapon.Definition != null ? _weapon.Definition.Icon : null;
                _weaponIcon.enabled = _weaponIcon.sprite != null;
            }

            if (_weaponLabel != null)
            {
                _weaponLabel.SetText(_weapon.Definition != null ? _weapon.Definition.DisplayName : string.Empty);
            }

            // 교체는 사건이 아니므로 숫자가 튀는 연출을 붙이지 않는다.
            _initialized = false;
            Refresh(_weapon);
            _initialized = true;
        }

        private void Unsubscribe()
        {
            if (_weapon != null)
            {
                _weapon.AmmoChanged -= Refresh;
                _weapon = null;
            }
        }

        /// <summary>
        /// 매 프레임이 아니라 값이 바뀔 때만 갱신한다. 초당 스물다섯 발이 나가지만
        /// 그래도 프레임 수보다는 적고, 쏘지 않는 동안에는 아무 일도 하지 않는다.
        /// </summary>
        private void Refresh(AircraftWeapon ammo)
        {
            if (_label != null)
            {
                _label.SetText(string.Format(_format, ammo.Remaining, ammo.Capacity));
            }

            if (_fill != null)
            {
                _fill.fillAmount = ammo.AmmoNormalized;
            }

            bool low = _lowThreshold > 0f && ammo.AmmoNormalized <= _lowThreshold;
            if (low != _wasLow)
            {
                _wasLow = low;

                if (_label != null)
                {
                    _label.color = low ? _lowColor : _normalColor;
                }

                if (_fill != null)
                {
                    _fill.color = low ? _lowColor : _normalColor;
                }
            }

            if (_emptyWarning != null && _emptyWarning.activeSelf != ammo.IsEmpty)
            {
                _emptyWarning.SetActive(ammo.IsEmpty);
            }

            // 한 발 쏜 것과 재보급으로 확 늘어난 것은 다른 사건이다. 무엇이 일어났는지
            // 따로 듣지 않고 잔탄이 늘었는지로 가른다 — 늘어나는 길은 보급뿐이다.
            if (_initialized)
            {
                Punch(ammo.Remaining > _shownRemaining ? _resupplyPunchScale : _punchScale);
            }

            _shownRemaining = ammo.Remaining;
        }

        /// <summary>
        /// 부풀린 상태를 다시 채워 넣는다. 가라앉는 중에 또 바뀌면 다시 최고점으로 간다.
        /// <para>
        /// 매번 튀었다 가라앉게 하면 초당 스물다섯 번 바뀌는 동안 숫자가 떨리는 것처럼 보인다.
        /// 이렇게 두면 쏘는 동안에는 부푼 채로 있다가 방아쇠를 놓으면 가라앉는다.
        /// </para>
        /// </summary>
        private void Punch(float amount)
        {
            if (_labelRect == null || amount <= 1f)
            {
                return;
            }

            // 재보급처럼 큰 연출이 진행 중이면 한 발짜리 연출로 덮어쓰지 않는다.
            _punchAmount = _punchRemaining > 0f ? Mathf.Max(_punchAmount, amount) : amount;
            _punchRemaining = _punchDuration;
        }

        private void Update()
        {
            if (_labelRect == null || _punchRemaining <= 0f)
            {
                return;
            }

            _punchRemaining -= _clock.Delta;

            if (_punchRemaining <= 0f)
            {
                _punchRemaining = 0f;
                _punchAmount = 1f;
                _labelRect.localScale = _labelBaseScale;
                return;
            }

            float t = _punchRemaining / _punchDuration;
            _labelRect.localScale = _labelBaseScale * Mathf.Lerp(1f, _punchAmount, t * t);
        }
    }
}
