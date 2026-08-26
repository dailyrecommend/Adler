using System.Collections.Generic;
using Adler.Flight;
using Adler.Weapons;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 실려 있는 보조무기 세 칸을 줄로 보여주고, 고른 칸을 밝힌다.
    /// <para>
    /// 세 칸을 늘 띄워두는 이유는 고르는 일이 곧 판단이기 때문이다. 고른 것만 보이면
    /// 무엇으로 갈아탈 수 있는지 알려고 휠을 돌려봐야 하고, 돌려보는 것 자체가 이미
    /// 갈아탄 것이라 되돌릴 수가 없다.
    /// </para>
    /// <para>
    /// 칸은 만들어 두신 것을 쓴다. 프리팹을 찍어내면 배치가 코드에 매이는데, 이 줄은
    /// 기울어진 판이 겹쳐 놓이는 자리라 손으로 놓는 편이 낫다. 칸이 무기보다 많으면
    /// 남는 칸은 알아서 물러난다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SecondaryDisplay : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("칸")]
        [Tooltip("왼쪽부터 차례로. 실려 있는 보조무기 수만큼만 켜진다.")]
        [SerializeField] private List<SecondarySlot> _slots = new();

        private WeaponBay _bay;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _bay = _aircraft != null ? _aircraft.Weapons : null;

            if (_bay == null)
            {
                Debug.LogError($"{nameof(SecondaryDisplay)}: 기체의 무기를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _slots.RemoveAll(slot => slot == null);
        }

        /// <summary>
        /// 첫 표시는 Start에서 한다. 무기가 자기 Awake에서 탄을 쥐는데, 오브젝트가
        /// 다르면 그 순서가 보장되지 않아 Awake에서 읽으면 아직 없는 것을 잡는다.
        /// </summary>
        private void Start()
        {
            _bay.Rearmed += Rebind;
            _bay.SecondaryChanged += OnSecondaryChanged;

            Rebind();
        }

        private void OnDestroy()
        {
            if (_bay != null)
            {
                _bay.Rearmed -= Rebind;
                _bay.SecondaryChanged -= OnSecondaryChanged;
            }
        }

        /// <summary>
        /// 어느 무기로 바뀌었는지는 쓰지 않는다. 칸 번호가 필요한데 그것은 무기고가
        /// 들고 있으므로, 바뀌었다는 사실만 신호로 받고 번호는 다시 물어본다.
        /// </summary>
        private void OnSecondaryChanged(AircraftWeapon weapon) => Highlight();

        /// <summary>
        /// 실려 있는 것들을 칸에 다시 세운다. 처음 켤 때와 장비를 갈아입었을 때.
        /// </summary>
        private void Rebind()
        {
            IReadOnlyList<AircraftWeapon> loaded = _bay.Secondaries;

            if (_slots.Count < loaded.Count)
            {
                Debug.LogWarning(
                    $"{nameof(SecondaryDisplay)}: 보조무기 {loaded.Count}개에 칸이 {_slots.Count}개뿐이라 " +
                    "일부는 화면에 뜨지 않습니다.", this);
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Bind(i < loaded.Count ? loaded[i] : null);
            }

            Highlight();
        }

        private void Highlight()
        {
            int selected = _bay.SelectedSecondary;

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].SetSelected(i == selected);
            }
        }

        /// <summary>
        /// 눈금은 여기서 한 번에 돌린다. 칸마다 자기 Update를 두면 같은 일이 세 번
        /// 도는 데다, 칸이 늘어날수록 그 수가 그대로 늘어난다.
        /// </summary>
        private void Update()
        {
            foreach (SecondarySlot slot in _slots)
            {
                slot.Refresh();
            }
        }
    }
}
