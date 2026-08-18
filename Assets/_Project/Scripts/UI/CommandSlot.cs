using System.Collections.Generic;
using Adler.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 폭탄 하나를 화면에 나타내는 칸. 아이콘과 그 옆의 화살표들을 갖는다.
    /// <para>
    /// 만드신 프리팹에 이 컴포넌트를 붙이고 아이콘과 화살표가 들어갈 자리를 지정하면,
    /// <see cref="CommandDisplay"/>가 폭탄 수만큼 복제해 채운다. 커맨드 길이가 폭탄마다
    /// 달라서 화살표 개수를 미리 만들어 둘 수 없기 때문이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandSlot : MonoBehaviour
    {
        [Header("구성")]
        [SerializeField] private Image _icon;

        [Tooltip("화살표가 들어갈 자리. Horizontal Layout Group을 붙여두면 알아서 늘어선다.")]
        [SerializeField] private RectTransform _arrowRoot;

        [Tooltip("칸 전체를 흐리게 만들 때 쓴다.")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("장전됐을 때 켤 요소. 테두리나 발광 같은 것. 비워둬도 된다.")]
        [SerializeField] private GameObject _armedHighlight;

        [Header("이름 / 쿨타임")]
        [Tooltip("평소에는 이름을, 쿨타임 중에는 남은 시간을 보여주는 글자.")]
        [SerializeField] private TMP_Text _label;

        [Tooltip("{0}에 분, {1}에 초가 들어간다. 초는 두 자리로 채워야 4:9처럼 보이지 않는다.")]
        [SerializeField] private string _cooldownFormat = "Cooldown {0}:{1:00}";

        [Tooltip("장전되어 쓸 수 있을 때 보여줄 글자.")]
        [SerializeField] private string _armedText = "READY";

        [Tooltip("출격 횟수를 다 썼을 때 보여줄 글자.")]
        [SerializeField] private string _exhaustedText = "Expended";

        [Tooltip("쿨타임 진행을 보여줄 이미지. Image Type을 Filled로 둘 것.")]
        [SerializeField] private Image _cooldownFill;

        [Tooltip("체크하면 쿨타임이 끝날수록 채워진다. 끄면 다 찬 상태에서 줄어든다.")]
        [SerializeField] private bool _fillAsItRecovers;

        [Tooltip("쿨타임 중일 때의 글자색.")]
        [SerializeField] private Color _cooldownTextColor = new Color(1f, 0.6f, 0.35f, 1f);

        [Tooltip("장전됐을 때의 글자색.")]
        [SerializeField] private Color _armedTextColor = new Color(0.4f, 1f, 0.5f, 1f);

        [SerializeField] private Color _readyTextColor = Color.white;

        [Header("화살표 색")]
        [Tooltip("아직 누르지 않은 화살표.")]
        [SerializeField] private Color _pendingColor = new Color(1f, 1f, 1f, 0.35f);

        [Tooltip("이미 누른 화살표.")]
        [SerializeField] private Color _enteredColor = Color.white;

        [Header("칸 흐리기")]
        [Tooltip("지금 입력과 맞지 않는 폭탄의 투명도.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dimmedAlpha = 0.25f;

        private readonly List<Image> _arrows = new();

        // 초 단위로만 표시하므로 그 자릿수가 바뀔 때만 문자열을 다시 만든다.
        private int _shownSeconds = -1;
        private bool _shownExhausted;
        private bool _shownArmed;
        private bool _armed;

        /// <summary>이 칸이 나타내는 스트라타젬.</summary>
        public StratagemDefinition Stratagem { get; private set; }

        /// <summary>스트라타젬을 붙이고 커맨드 길이만큼 화살표를 만든다.</summary>
        public void Bind(StratagemDefinition stratagem, Image arrowPrefab)
        {
            Stratagem = stratagem;

            if (_icon != null)
            {
                _icon.sprite = stratagem.Icon;
                _icon.enabled = stratagem.Icon != null;
            }

            BuildArrows(stratagem, arrowPrefab);
            SetMatchedCount(0);
            SetDimmed(false);
            SetArmed(false);

            _shownSeconds = -1;
            _shownExhausted = false;
            SetCooldown(0f, 0f, exhausted: false);
        }

        /// <summary>
        /// 한 줄이 네 가지를 번갈아 보여준다. 장전됨 → 소진 → 쿨타임 → 이름 순으로 우선한다.
        /// <para>
        /// 같은 자리를 쓰는 이유는 그때그때 알아야 할 것이 다르기 때문이다. 쿨타임이 도는
        /// 동안 궁금한 것은 이름이 아니라 언제 되는지고, 장전된 뒤에는 쓸 수 있다는 사실이다.
        /// </para>
        /// </summary>
        public void SetCooldown(float remainingSeconds, float normalized, bool exhausted)
        {
            if (_cooldownFill != null)
            {
                _cooldownFill.fillAmount = _fillAsItRecovers ? 1f - normalized : normalized;
            }

            if (_label == null)
            {
                return;
            }

            // 올림해서 보여준다. 0.4초 남았는데 0으로 뜨면 눌러도 안 되는 순간이 생긴다.
            int seconds = Mathf.CeilToInt(remainingSeconds);

            if (seconds == _shownSeconds && exhausted == _shownExhausted && _armed == _shownArmed)
            {
                return;
            }

            _shownSeconds = seconds;
            _shownExhausted = exhausted;
            _shownArmed = _armed;

            if (_armed)
            {
                _label.SetText(_armedText);
                _label.color = _armedTextColor;
                return;
            }

            if (exhausted)
            {
                _label.SetText(_exhaustedText);
                _label.color = _cooldownTextColor;
                return;
            }

            if (seconds <= 0)
            {
                _label.SetText(Stratagem != null ? Stratagem.DisplayName : string.Empty);
                _label.color = _readyTextColor;
                return;
            }

            _label.SetText(string.Format(_cooldownFormat, seconds / 60, seconds % 60));
            _label.color = _cooldownTextColor;
        }

        private void BuildArrows(StratagemDefinition stratagem, Image arrowPrefab)
        {
            foreach (Image arrow in _arrows)
            {
                Destroy(arrow.gameObject);
            }

            _arrows.Clear();

            if (_arrowRoot == null || arrowPrefab == null)
            {
                return;
            }

            foreach (CommandDirection direction in stratagem.Command)
            {
                Image arrow = Instantiate(arrowPrefab, _arrowRoot);
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, RotationFor(direction));
                _arrows.Add(arrow);
            }
        }

        /// <summary>앞에서부터 몇 개까지 입력됐는지 표시한다.</summary>
        public void SetMatchedCount(int count)
        {
            for (int i = 0; i < _arrows.Count; i++)
            {
                _arrows[i].color = i < count ? _enteredColor : _pendingColor;
            }
        }

        /// <summary>지금 입력으로는 더 이상 가능하지 않은 폭탄을 흐리게 만든다.</summary>
        public void SetDimmed(bool dimmed)
        {
            if (_group != null)
            {
                _group.alpha = dimmed ? _dimmedAlpha : 1f;
            }
        }

        public void SetArmed(bool armed)
        {
            _armed = armed;

            if (_armedHighlight != null)
            {
                _armedHighlight.SetActive(armed);
            }
        }

        /// <summary>
        /// 화살표 그림이 위를 향한다고 보고 돌린다.
        /// UI에서 Z 회전은 반시계 방향이라 오른쪽이 음수다.
        /// </summary>
        private static float RotationFor(CommandDirection direction)
        {
            return direction switch
            {
                CommandDirection.Up => 0f,
                CommandDirection.Left => 90f,
                CommandDirection.Down => 180f,
                CommandDirection.Right => -90f,
                _ => 0f,
            };
        }
    }
}
