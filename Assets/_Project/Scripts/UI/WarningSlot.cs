using TMPro;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 경고 한 줄. 글자만 있다.
    /// <para>
    /// 디버프 칸과 달리 아이콘이 없다. 경고는 오래 떠 있는 것이 아니라 한 번 읽고
    /// 반응하면 끝나는 것이라, 그림을 알아보는 시간조차 아깝다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarningSlot : MonoBehaviour
    {
        [Tooltip("비워두면 이 오브젝트와 그 아래에서 찾는다.")]
        [SerializeField] private TMP_Text _label;

        /// <summary>이 칸이 나타내는 경고.</summary>
        public WarningKind Kind { get; private set; }

        private void Awake()
        {
            if (_label == null)
            {
                _label = GetComponentInChildren<TMP_Text>(includeInactive: true);
            }
        }

        public void Bind(WarningKind kind, string text, Color color)
        {
            Kind = kind;

            if (_label == null)
            {
                return;
            }

            _label.SetText(text);
            _label.color = color;
        }
    }
}
