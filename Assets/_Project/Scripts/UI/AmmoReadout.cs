using Adler.Flight;
using Adler.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 한 자리에 걸린 무기를 화면 요소에 꽂아준다.
    /// <para>
    /// 자리마다 하나씩 붙인다. 주무기와 보조무기가 늘 함께 살아 있으므로 한 표시가
    /// 둘을 번갈아 보여줄 이유가 없고, 번갈아 두면 지금 무엇을 보고 있는지를 읽는
    /// 사람이 매번 확인해야 한다.
    /// </para>
    /// <para>
    /// 보조무기 자리는 휠을 돌릴 때마다 걸린 것이 바뀐다. 그때만 다시 꽂고, 그 사이에는
    /// 탄 눈금 하나만 돈다 — 이름과 설명과 그림은 무기가 바뀌지 않는 한 그대로다.
    /// </para>
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

        [Tooltip("어느 자리의 무기를 보여줄지.")]
        [SerializeField] private WeaponSlot _slot = WeaponSlot.Primary;

        [Header("무기")]
        [Tooltip("무기 아이콘. 그 무기가 어느 갈래인지 나타내는 글리프다.")]
        [SerializeField] private Image _icon;

        [Tooltip("무기 이름.")]
        [SerializeField] private TMP_Text _nameLabel;

        [Tooltip("무기 설명.")]
        [SerializeField] private TMP_Text _descriptionLabel;

        [Tooltip("남은 탄을 채움량으로 넣을 이미지. Image Type을 Filled로 둘 것.\n\n" +
                 "차오르는 몫까지 함께 센다 — 발수만 세면 눈금이 뚝뚝 끊겨서\n" +
                 "다음 발이 언제 오는지 화면으로 알 수 없다.")]
        [SerializeField] private Image _fill;

        private WeaponBay _bay;
        private AmmoStock _stock;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _bay = _aircraft != null ? _aircraft.Weapons : null;

            if (_bay == null)
            {
                Debug.LogError($"{nameof(AmmoReadout)}: 기체의 무기를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 첫 표시는 Start에서 한다. 무기가 자기 Awake에서 탄을 쥐는데, 오브젝트가
        /// 다르면 그 순서가 보장되지 않아 Awake에서 읽으면 아직 없는 것을 잡는다.
        /// </summary>
        private void Start()
        {
            // 장비를 갈아입으면 이 자리의 무기도 바뀐다. 다시 꽂는다.
            _bay.Rearmed += OnRearmed;

            if (_slot == WeaponSlot.Secondary)
            {
                _bay.SecondaryChanged += Bind;
            }

            Bind(_bay[_slot]);
        }

        private void OnDestroy()
        {
            if (_bay != null)
            {
                _bay.Rearmed -= OnRearmed;

                if (_slot == WeaponSlot.Secondary)
                {
                    _bay.SecondaryChanged -= Bind;
                }
            }
        }

        private void OnRearmed() => Bind(_bay[_slot]);

        /// <summary>
        /// 이 자리에 걸린 무기를 꽂는다. 빈 자리면 표시째로 물러난다.
        /// <para>
        /// 조용히 물러나는 이유는 빈 자리가 잘못이 아니기 때문이다. 보조무기를 둘만
        /// 달고 나가는 것은 흔한 일이고, 그때마다 오류를 뱉으면 콘솔이 배선 실수와
        /// 설계를 구별하지 못하게 된다.
        /// </para>
        /// </summary>
        private void Bind(AircraftWeapon weapon)
        {
            bool filled = weapon != null && weapon.Definition != null;

            if (gameObject.activeSelf != filled)
            {
                gameObject.SetActive(filled);
            }

            if (!filled)
            {
                _stock = null;
                return;
            }

            WeaponDefinition definition = weapon.Definition;
            _stock = weapon.Ammo;

            Show(_icon, definition.Icon);

            _nameLabel?.SetText(definition.DisplayName);
            _descriptionLabel?.SetText(definition.Description);
        }

        /// <summary>
        /// 탄 눈금만 따라간다. 차오르는 정도는 알림으로 오지 않으므로,
        /// 매끄럽게 채우려면 직접 읽는 수밖에 없다.
        /// </summary>
        private void Update()
        {
            if (_fill != null && _stock != null)
            {
                _fill.fillAmount = _stock.Normalized;
            }
        }

        /// <summary>그림이 없으면 자리를 비운다. 빈 사각형이 남는 것보다 낫다.</summary>
        private static void Show(Image target, Sprite sprite)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = sprite;
            target.enabled = sprite != null;
        }
    }
}
