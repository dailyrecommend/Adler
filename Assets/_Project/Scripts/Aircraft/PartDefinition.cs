using UnityEngine;

namespace Adler.Aircraft
{
    /// <summary>기체에서 부품이 들어가는 자리. 한 자리에 하나만 장착된다.</summary>
    public enum PartSlot
    {
        Engine = 0,
        Wing = 1,
        Airframe = 2,
        Avionics = 3,
    }

    /// <summary>
    /// 정비창에서 장착하는 부품 하나.
    /// <para>
    /// 부품은 스스로 아무 일도 하지 않는다. 어떤 수치를 얼마나 바꾸는지 적어둔
    /// 데이터일 뿐이고, 합산은 <see cref="AircraftStatSheet"/>가 한다.
    /// 새 부품을 만드는 데 코드가 필요 없다는 뜻이다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Part", menuName = "Adler/Aircraft/Part Definition")]
    public sealed class PartDefinition : ScriptableObject
    {
        [Header("표시")]
        public string DisplayName = "Unnamed Part";

        [TextArea(2, 4)]
        public string Description;

        [Header("장착")]
        public PartSlot Slot = PartSlot.Engine;

        [Tooltip("강화 단계. 보정치에 이 배율이 곱해진다.")]
        [Min(1)]
        public int Tier = 1;

        [Header("성능 보정")]
        [Tooltip("장점만 넣지 말 것. 속도를 올리면 선회를 깎는 식으로 상충 관계를 만들어야\n" +
                 "플레이어가 고민할 거리가 생긴다.")]
        public StatModifier[] Modifiers = System.Array.Empty<StatModifier>();

        /// <summary>강화 단계를 반영한 실제 보정치.</summary>
        public float EffectiveValue(in StatModifier modifier)
        {
            return modifier.Value * Tier;
        }
    }
}
