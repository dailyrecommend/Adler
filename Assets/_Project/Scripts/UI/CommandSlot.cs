using System.Collections.Generic;
using Adler.Weapons;
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
