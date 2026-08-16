using System.Collections.Generic;
using Adler.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 탑재 가능한 폭탄들과 각자의 커맨드를 화면에 늘어놓고, 입력이 진행되는 대로 갱신한다.
    /// <para>
    /// 커맨드를 외우게 만들면 진입 장벽이 되고, 매번 화면을 뚫어져라 봐야 해도 비행을 못 한다.
    /// 눌린 만큼 화살표가 채워지고 가망 없는 폭탄이 흐려지면, 곁눈질만으로 지금 어디까지
    /// 왔는지 알 수 있다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandDisplay : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private BombBay _bombBay;

        [Header("만들어 둔 조각")]
        [Tooltip("폭탄 한 칸의 모양. CommandSlot이 붙어 있어야 한다.")]
        [SerializeField] private CommandSlot _slotPrefab;

        [Tooltip("화살표 하나의 모양. 위를 향한 그림으로 두면 방향에 맞게 돌려 쓴다.")]
        [SerializeField] private Image _arrowPrefab;

        [Tooltip("칸들이 늘어설 자리. 비워두면 이 오브젝트 아래에 붙인다.")]
        [SerializeField] private RectTransform _slotRoot;

        private readonly List<CommandSlot> _slots = new();

        private void Awake()
        {
            if (_bombBay == null || _slotPrefab == null)
            {
                Debug.LogError($"{nameof(CommandDisplay)}: Bomb Bay 또는 Slot Prefab이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_slotRoot == null)
            {
                _slotRoot = transform as RectTransform;
            }

            BuildSlots();
        }

        private void OnEnable()
        {
            _bombBay.CommandProgressed += OnCommandProgressed;
            _bombBay.CommandReset += OnCommandReset;
            _bombBay.Authorized += OnAuthorized;
            _bombBay.Dropped += OnDropped;
        }

        private void OnDisable()
        {
            _bombBay.CommandProgressed -= OnCommandProgressed;
            _bombBay.CommandReset -= OnCommandReset;
            _bombBay.Authorized -= OnAuthorized;
            _bombBay.Dropped -= OnDropped;
        }

        private void BuildSlots()
        {
            foreach (BombDefinition bomb in _bombBay.Loadout)
            {
                if (bomb == null)
                {
                    continue;
                }

                CommandSlot slot = Instantiate(_slotPrefab, _slotRoot);
                slot.Bind(bomb, _arrowPrefab);
                _slots.Add(slot);
            }
        }

        private void OnCommandProgressed(IReadOnlyList<CommandDirection> entered)
        {
            foreach (CommandSlot slot in _slots)
            {
                bool matches = Matches(slot.Bomb, entered);

                // 맞는 폭탄만 진행 상황을 보여준다. 어긋난 폭탄까지 화살표를 채우면
                // 지금 어느 커맨드를 입력하고 있는지 알아볼 수 없다.
                slot.SetMatchedCount(matches ? entered.Count : 0);
                slot.SetDimmed(!matches);
            }
        }

        private void OnCommandReset()
        {
            foreach (CommandSlot slot in _slots)
            {
                slot.SetMatchedCount(0);
                slot.SetDimmed(false);
            }
        }

        private void OnAuthorized(BombDefinition bomb)
        {
            foreach (CommandSlot slot in _slots)
            {
                slot.SetMatchedCount(0);
                slot.SetDimmed(false);
                slot.SetArmed(slot.Bomb == bomb);
            }
        }

        private void OnDropped(BombDefinition bomb)
        {
            foreach (CommandSlot slot in _slots)
            {
                slot.SetArmed(false);
            }
        }

        private static bool Matches(BombDefinition bomb, IReadOnlyList<CommandDirection> entered)
        {
            if (bomb == null || bomb.Command.Length < entered.Count)
            {
                return false;
            }

            for (int i = 0; i < entered.Count; i++)
            {
                if (bomb.Command[i] != entered[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
