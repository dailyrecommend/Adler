using System.Collections.Generic;
using Adler.Combat;
using Adler.Flight;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 지금 받고 있는 나쁜 상태들을 화면에 늘어놓는다.
    /// <para>
    /// 걸린 것만 만들고 풀리면 지운다. 전부 미리 만들어 두고 껐다 켜는 방법도 있지만,
    /// 그러면 디버프를 하나 늘릴 때마다 이쪽에도 목록을 적어야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebuffDisplay : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("만들어 둔 조각")]
        [Tooltip("디버프 한 칸의 모양. DebuffSlot이 붙어 있어야 한다.")]
        [SerializeField] private DebuffSlot _slotPrefab;

        [Tooltip("칸들이 늘어설 자리. 비워두면 이 오브젝트 아래에 붙인다.\n" +
                 "Vertical Layout Group을 붙여두면 알아서 쌓인다.")]
        [SerializeField] private RectTransform _slotRoot;

        private AircraftDebuffs _debuffs;

        // 지금 화면에 있는 칸들. 어느 디버프의 것인지로 찾는다.
        private readonly Dictionary<DebuffDefinition, DebuffSlot> _slots = new();
        private readonly List<DebuffDefinition> _removed = new();

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _debuffs = _aircraft != null ? _aircraft.Debuffs : null;

            if (_debuffs == null || _slotPrefab == null)
            {
                Debug.LogError($"{nameof(DebuffDisplay)}: 기체의 디버프 목록 또는 Slot Prefab이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_slotRoot == null)
            {
                _slotRoot = transform as RectTransform;
            }
        }

        private void OnEnable()
        {
            _debuffs.Changed += Rebuild;

            // 이미 걸린 채로 켜졌을 수 있다. 신호는 바뀔 때만 오므로 한 번 맞춰둔다.
            Rebuild();
        }

        private void OnDisable() => _debuffs.Changed -= Rebuild;

        /// <summary>
        /// 목록에 맞춰 칸을 만들고 지운다.
        /// <para>
        /// 살아 있는 칸은 건드리지 않는다. 매번 전부 지웠다 다시 만들면 하나가 풀릴 때
        /// 남은 것들까지 등장 연출을 다시 해서, 방금 걸린 것이 무엇인지 알 수 없게 된다.
        /// </para>
        /// </summary>
        private void Rebuild()
        {
            foreach (DebuffDefinition debuff in _debuffs.Active)
            {
                if (debuff == null || _slots.ContainsKey(debuff))
                {
                    continue;
                }

                DebuffSlot slot = Instantiate(_slotPrefab, _slotRoot);
                slot.Bind(debuff);
                _slots.Add(debuff, slot);
            }

            // 돌면서 지우면 순회가 깨지므로 먼저 모아둔다.
            _removed.Clear();

            foreach (KeyValuePair<DebuffDefinition, DebuffSlot> pair in _slots)
            {
                if (!_debuffs.IsActive(pair.Key))
                {
                    _removed.Add(pair.Key);
                }
            }

            foreach (DebuffDefinition debuff in _removed)
            {
                Destroy(_slots[debuff].gameObject);
                _slots.Remove(debuff);
            }

            ApplyOrder();
        }

        /// <summary>
        /// 정해진 우선순위대로 줄을 세운다.
        /// <para>
        /// 만든 차례대로 두면 걸린 순서에 따라 자리가 바뀐다. 같은 상황인데 볼 때마다
        /// 다른 줄에 있으면 눈이 위치로 기억하지 못하고 매번 글자를 읽어야 한다.
        /// </para>
        /// </summary>
        private void ApplyOrder()
        {
            int index = 0;

            foreach (DebuffDefinition debuff in _debuffs.Active)
            {
                if (debuff != null && _slots.TryGetValue(debuff, out DebuffSlot slot))
                {
                    slot.transform.SetSiblingIndex(index++);
                }
            }
        }
    }
}
