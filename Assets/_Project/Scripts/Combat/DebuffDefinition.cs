using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 기체가 받고 있는 나쁜 상태 하나. 화면에 무엇으로 보일지를 담는다.
    /// <para>
    /// 효과는 여기 없다. 봉인이 무슨 일을 하는지는 재머가, 화재가 어떻게 번지는지는
    /// 화재가 안다. 이것은 "그 상태에 걸렸다는 사실"에만 이름과 그림을 붙인다.
    /// </para>
    /// <para>
    /// 그래서 새 디버프를 만들 때 화면 쪽은 손대지 않아도 된다. 에셋 하나를 만들고
    /// 그 상태를 아는 쪽에서 켜고 끄기만 하면 목록에 저절로 나타난다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Adler/Debuff", fileName = "Debuff")]
    public sealed class DebuffDefinition : ScriptableObject
    {
        [Tooltip("화면에 뜰 이름. 짧을수록 좋다 — 곁눈질로 읽어야 하는 글자다.")]
        public string DisplayName = "DEBUFF";

        public Sprite Icon;

        [Tooltip("아이콘과 글자에 함께 입힐 색. 급한 것일수록 붉게 둔다.")]
        public Color Tint = new Color(1f, 0.35f, 0.3f, 1f);

        [Tooltip("여러 개가 걸렸을 때의 순서. 낮을수록 위로 간다.\n" +
                 "먼저 처리해야 하는 것을 낮게 둘 것.")]
        public int Priority;
    }
}
