using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 무기 한 종류의 공통 성능. 기총과 미사일이 여기서 갈라진다.
    /// <para>
    /// 실시간으로 바꿔가며 쓰는 이상 화면과 탄약, 재보급은 무엇을 들고 있든 같은 방식으로
    /// 다뤄야 한다. 무기마다 다른 이름의 값을 두면 무기를 하나 늘릴 때마다 그것들을
    /// 전부 고치게 된다.
    /// </para>
    /// </summary>
    public abstract class WeaponDefinition : ScriptableObject
    {
        [Header("표시")]
        public string DisplayName = "Unnamed Weapon";

        [Tooltip("화면에 띄울 아이콘.")]
        public Sprite Icon;

        [Header("발사")]
        [Tooltip("분당 발사 수.")]
        [Min(1f)]
        public float RoundsPerMinute = 900f;

        [Tooltip("가득 채웠을 때 실을 수 있는 발수.")]
        [Min(1)]
        public int AmmoCapacity = 600;

        [Tooltip("날아갈 것의 프리팹.")]
        public GameObject Prefab;

        [Tooltip("이만큼 날아가면 사라진다 (m).")]
        [Min(1f)]
        public float Range = 300f;

        [Header("위력")]
        [Tooltip("관문을 통과했을 때 들어가는 피해량.")]
        public float Damage = 12f;

        [Tooltip("관통력. 표적의 장갑 이상이어야 피해가 들어간다.")]
        [Min(0f)]
        public float Penetration = 5f;

        [Tooltip("철거력. 건물이 요구하는 수준 이상이어야 부술 수 있다.")]
        [Min(0f)]
        public float Demolition;

        /// <summary>한 발과 다음 발 사이의 간격(초).</summary>
        public float ShotInterval => 60f / RoundsPerMinute;

        /// <summary>가득 채운 탄을 쉬지 않고 쏟아부었을 때 버티는 시간(초).</summary>
        public float SustainedFireSeconds => AmmoCapacity * ShotInterval;
    }
}
