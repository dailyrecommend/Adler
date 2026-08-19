using Adler.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 디버프 하나를 나타내는 칸. 아이콘과 이름을 갖는다.
    /// <para>
    /// 만드신 프리팹에 이 컴포넌트를 붙이고 아이콘과 글자 자리를 지정하면,
    /// <see cref="DebuffDisplay"/>가 걸린 개수만큼 만들어 채운다.
    /// </para>
    /// <para>
    /// 나타나고 사라지는 것 외에 움직이지 않는다. 걸렸다는 사실만 전하면 되는 표시라,
    /// 움직임을 얹으면 그쪽으로 시선을 뺏겨 정작 비행에서 눈을 떼게 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebuffSlot : MonoBehaviour
    {
        [Header("구성")]
        [SerializeField] private Image _icon;

        [SerializeField] private TMP_Text _label;

        /// <summary>이 칸이 나타내는 디버프.</summary>
        public DebuffDefinition Debuff { get; private set; }

        /// <summary>디버프를 붙인다.</summary>
        public void Bind(DebuffDefinition debuff)
        {
            Debuff = debuff;

            if (_icon != null)
            {
                _icon.sprite = debuff.Icon;
                _icon.enabled = debuff.Icon != null;
                _icon.color = debuff.Tint;
            }

            if (_label != null)
            {
                _label.SetText(debuff.DisplayName);
                _label.color = debuff.Tint;
            }
        }
    }
}
