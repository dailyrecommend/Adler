using System.Collections.Generic;
using Adler.Core;
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

        [Header("봉인")]
        [Tooltip("재머에 걸렸을 때 모든 칸이 함께 쓸 아이콘.\n" +
                 "무엇이 막혔는지가 아니라 전부 막혔다는 사실이 읽혀야 한다.\n" +
                 "비워두면 원래 아이콘을 그대로 쓴다.")]
        [SerializeField] private Sprite _jammedIcon;

        [Tooltip("봉인 중에 이름 자리에 대신 넣을 글자.")]
        [SerializeField] private string _jammedText = "JAMMED";

        [SerializeField] private Color _jammedTextColor = new Color(1f, 0.35f, 0.3f, 1f);

        [Tooltip("봉인 중 아이콘에 곱할 색.")]
        [SerializeField] private Color _jammedIconColor = new Color(1f, 0.45f, 0.4f, 1f);

        [Tooltip("봉인 중 아이콘에 씌울 머티리얼. Adler/UI Glitch 쉐이더를 쓴다.\n" +
                 "칸마다 복제해 다른 씨앗을 넣으므로, 하나를 모두가 나눠 써도 된다.\n" +
                 "비워두면 아이콘만 바뀌고 지지직거리지는 않는다.")]
        [SerializeField] private Material _jammedMaterial;

        [Tooltip("화살표가 떨리는 폭(px). 읽을 수 없을 만큼 흔들면 안 된다.")]
        [Min(0f)]
        [SerializeField] private float _shakePixels = 2.5f;

        [Tooltip("떨리는 빠르기. 높을수록 잘게 떤다.")]
        [Min(0.1f)]
        [SerializeField] private float _shakeSpeed = 26f;

        [Header("칸 흐리기")]
        [Tooltip("지금 입력과 맞지 않는 것의 투명도.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dimmedAlpha = 0.25f;

        [Tooltip("나타나고 사라지는 속도. 클수록 즉각적이다.")]
        [Min(0.1f)]
        [SerializeField] private float _fadeSpeed = 12f;

        private readonly List<Image> _arrows = new();

        // 떨리기 전의 제자리. 여기에 흔들림을 얹지 않으면 흔들린 자리에 또 얹혀 밀려난다.
        private readonly List<Vector2> _arrowHome = new();
        private readonly List<float> _shakeSeeds = new();

        // 초 단위로만 표시하므로 그 자릿수가 바뀔 때만 문자열을 다시 만든다.
        private int _shownSeconds = -1;
        private bool _shownExhausted;

        private bool _dimmed;
        private bool _hidden;
        private float _targetAlpha = 1f;
        private LayoutElement _layout;
        private bool _shownArmed;
        private bool _armed;

        private bool _jammed;
        private bool _shownJammed;
        private bool _homePending;
        private Sprite _normalIcon;
        private Color _iconBaseColor = Color.white;

        private Material _iconBaseMaterial;
        private Material _glitchInstance;

        private static readonly int UvRectId = Shader.PropertyToID("_UVRect");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        /// <summary>이 칸이 나타내는 스트라타젬.</summary>
        public StratagemDefinition Stratagem { get; private set; }

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            if (_icon != null)
            {
                _iconBaseColor = _icon.color;
                _iconBaseMaterial = _icon.material;
            }

            if (_jammedMaterial == null)
            {
                return;
            }

            // 칸마다 복제한다. 하나를 나눠 쓰면 모든 아이콘이 같은 무늬로 같은 순간에
            // 망가져서, 망가진 화면이 아니라 반복되는 무늬로 보인다.
            _glitchInstance = new Material(_jammedMaterial);
            _glitchInstance.SetFloat(SeedId, Random.Range(0f, 100f));
        }

        private void OnDestroy()
        {
            if (_glitchInstance != null)
            {
                Destroy(_glitchInstance);
            }
        }

        /// <summary>스트라타젬을 붙이고 커맨드 길이만큼 화살표를 만든다.</summary>
        public void Bind(StratagemDefinition stratagem, Image arrowPrefab)
        {
            Stratagem = stratagem;
            _normalIcon = stratagem.Icon;
            ApplyIcon();

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

            if (seconds == _shownSeconds && exhausted == _shownExhausted
                && _armed == _shownArmed && _jammed == _shownJammed)
            {
                return;
            }

            _shownSeconds = seconds;
            _shownExhausted = exhausted;
            _shownArmed = _armed;
            _shownJammed = _jammed;

            // 봉인이 모든 것을 덮는다. 장전됐든 쿨타임이 남았든 지금은 쓸 수 없고,
            // 그 사실 하나만 알면 되는 상황이다.
            if (_jammed)
            {
                _label.SetText(_jammedText);
                _label.color = _jammedTextColor;
                return;
            }

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
            _arrowHome.Clear();
            _shakeSeeds.Clear();

            if (_arrowRoot == null || arrowPrefab == null)
            {
                return;
            }

            foreach (CommandDirection direction in stratagem.Command)
            {
                Image arrow = Instantiate(arrowPrefab, _arrowRoot);
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, RotationFor(direction));
                _arrows.Add(arrow);

                // 화살표마다 다른 자리에서 소음을 읽는다. 같은 값을 쓰면 줄 전체가
                // 한 덩어리로 움직여서, 떠는 것이 아니라 미끄러지는 것처럼 보인다.
                _shakeSeeds.Add(Random.Range(0f, 100f));
            }

            if (_jammed)
            {
                _homePending = true;
            }
        }

        /// <summary>
        /// 재머 범위 안이라 아무것도 부를 수 없는 상태를 표시한다.
        /// <para>
        /// 칸을 지우거나 흐리게 만들지 않는다. 목록이 그대로 있어야 무엇을 잃은 것이 아니라
        /// 지금 막혀 있을 뿐임이 드러나고, 재머를 처리한 뒤에 돌아온다는 것도 함께 읽힌다.
        /// </para>
        /// </summary>
        public void SetJammed(bool jammed)
        {
            if (_jammed == jammed)
            {
                return;
            }

            _jammed = jammed;
            ApplyIcon();

            if (jammed)
            {
                // 제자리를 지금 읽으면 안 된다. 레이아웃은 Update 뒤에 계산되므로
                // 만들어진 직후에는 화살표가 아직 전부 원점에 겹쳐 있다.
                _homePending = true;
            }
            else
            {
                RestoreArrows();
            }
        }

        /// <summary>
        /// 봉인 여부에 맞춰 아이콘의 그림과 머티리얼을 갈아 끼운다.
        /// <para>
        /// 지지직거림은 쉐이더가 알아서 계속 돌아간다. 여기서는 씌우고 벗기기만 하면 된다.
        /// </para>
        /// </summary>
        private void ApplyIcon()
        {
            if (_icon == null)
            {
                return;
            }

            Sprite sprite = _jammed && _jammedIcon != null ? _jammedIcon : _normalIcon;

            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
            _icon.color = _jammed ? _jammedIconColor : _iconBaseColor;

            if (_glitchInstance == null)
            {
                return;
            }

            if (_jammed)
            {
                // 그림이 아틀라스 어디에 놓였는지 알려준다. 이게 없으면 줄을 밀 때
                // 옆에 묶인 다른 그림이 딸려 나온다.
                _glitchInstance.SetVector(UvRectId, UvRectOf(sprite));
                _icon.material = _glitchInstance;
            }
            else
            {
                _icon.material = _iconBaseMaterial;
            }
        }

        /// <summary>
        /// 스프라이트가 텍스처 안에서 차지하는 UV 범위. xy가 왼쪽아래, zw가 오른쪽위.
        /// 아틀라스에 묶이지 않은 그림이면 그대로 (0,0,1,1)이 된다.
        /// </summary>
        private static Vector4 UvRectOf(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            Rect rect = sprite.textureRect;
            float width = sprite.texture.width;
            float height = sprite.texture.height;

            return new Vector4(
                rect.xMin / width,
                rect.yMin / height,
                rect.xMax / width,
                rect.yMax / height);
        }

        /// <summary>
        /// 화살표를 잘게 떤다.
        /// <para>
        /// 흔들림은 글자보다 먼저 눈에 들어온다. JAMMED를 읽기 전에 이미 뭔가 잘못됐다는
        /// 것을 알아채고, 커맨드를 치려던 손을 멈추게 된다.
        /// </para>
        /// <para>
        /// 무작위 대신 이어지는 소음을 쓴다. 매 프레임 새 값을 뽑으면 떠는 것이 아니라
        /// 지직거리는 화면처럼 보여서 고장 난 UI로 읽힌다.
        /// </para>
        /// </summary>
        private void ShakeArrows()
        {
            float time = Time.unscaledTime * _shakeSpeed;

            for (int i = 0; i < _arrows.Count; i++)
            {
                float seed = _shakeSeeds[i];

                Vector2 offset = new Vector2(
                    Mathf.PerlinNoise(time, seed) - 0.5f,
                    Mathf.PerlinNoise(seed, time) - 0.5f) * (2f * _shakePixels);

                _arrows[i].rectTransform.anchoredPosition = _arrowHome[i] + offset;
            }
        }

        /// <summary>
        /// 흔들기 전의 제자리를 읽어둔다.
        /// <para>
        /// 읽기 전에 레이아웃을 직접 한 번 돌린다. 줄 세우기는 Update가 모두 끝난 뒤에
        /// 계산되므로, 그냥 읽으면 아직 자리를 잡지 않은 값 — 대개 전부 원점 — 을 제자리로
        /// 기억한다. 그러면 봉인되는 순간 화살표가 한 점에 겹쳐서 떨게 된다.
        /// </para>
        /// </summary>
        private void CaptureHome()
        {
            _homePending = false;
            _arrowHome.Clear();

            if (_arrowRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_arrowRoot);
            }

            foreach (Image arrow in _arrows)
            {
                _arrowHome.Add(arrow.rectTransform.anchoredPosition);
            }
        }

        private void RestoreArrows()
        {
            _homePending = false;

            for (int i = 0; i < _arrows.Count && i < _arrowHome.Count; i++)
            {
                _arrows[i].rectTransform.anchoredPosition = _arrowHome[i];
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

        /// <summary>지금 입력으로는 더 이상 가능하지 않은 것을 흐리게 만든다.</summary>
        public void SetDimmed(bool dimmed)
        {
            _dimmed = dimmed;
            ApplyAlpha();
        }

        /// <summary>승인 직후처럼 이 칸만 남기고 나머지를 치울 때 쓴다.</summary>
        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            ApplyAlpha();
        }

        /// <summary>
        /// 감춤과 흐림을 한 곳에서 합친다. 각자 투명도를 건드리면 나중에 불린 쪽이
        /// 앞의 결정을 지워서, 치워둔 칸이 도로 나타나는 일이 생긴다.
        /// </summary>
        private void ApplyAlpha()
        {
            _targetAlpha = _hidden ? 0f : (_dimmed ? _dimmedAlpha : 1f);

            // 나타날 때는 곧바로 자리를 되찾는다. 다 나타난 뒤에 자리를 잡으면
            // 다른 칸들이 뒤늦게 밀려나면서 목록이 한 번 더 움직인다.
            if (_targetAlpha > 0f)
            {
                SetOccupiesLayout(true);
            }
        }

        /// <summary>
        /// 감춰진 칸이 자리를 비우게 한다.
        /// <para>
        /// 투명하게만 만들면 레이아웃에서는 그대로 남아, 장전된 칸 하나만 보일 때
        /// 위쪽에 빈 자리가 생긴다. 자리를 비우면 순서를 바꾸지 않고도 남은 칸이
        /// 자연스럽게 맨 위로 올라온다.
        /// </para>
        /// </summary>
        private void SetOccupiesLayout(bool occupies)
        {
            if (_layout == null)
            {
                _layout = GetComponent<LayoutElement>();
                if (_layout == null)
                {
                    _layout = gameObject.AddComponent<LayoutElement>();
                }
            }

            _layout.ignoreLayout = !occupies;
        }

        /// <summary>
        /// 칸마다 따로 나타나고 사라진다.
        /// <para>
        /// 창 전체를 한꺼번에 여닫지 않는 이유는, 장전된 칸만 남아야 하기 때문이다.
        /// 담고 있는 쪽의 투명도로 감추면 그 안의 어떤 칸도 남길 수 없다.
        /// </para>
        /// </summary>
        private void Update()
        {
            if (_jammed)
            {
                // 제자리를 읽는 프레임에는 아직 흔들지 않는다. 레이아웃을 방금 돌려놓고
                // 같은 프레임에 밀어버리면 읽어둔 값이 무엇이었는지 알 수 없게 된다.
                if (_homePending)
                {
                    CaptureHome();
                }
                else if (_shakePixels > 0f)
                {
                    ShakeArrows();
                }
            }

            if (_group == null || Mathf.Approximately(_group.alpha, _targetAlpha))
            {
                return;
            }

            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, _fadeSpeed * _clock.Delta);

            // 다 사라진 뒤에 자리를 비운다. 사라지는 도중에 비우면 남은 칸들이
            // 먼저 밀려 올라와서 이 칸이 그 위에 겹쳐 보인다.
            if (_targetAlpha <= 0f && _group.alpha <= 0f)
            {
                SetOccupiesLayout(false);
            }
        }

        /// <summary>기다리지 않고 지금 상태로 맞춘다. 창을 만들 때처럼 처음 그릴 때 쓴다.</summary>
        public void SnapAlpha()
        {
            if (_group != null)
            {
                _group.alpha = _targetAlpha;
            }

            SetOccupiesLayout(_targetAlpha > 0f);
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
