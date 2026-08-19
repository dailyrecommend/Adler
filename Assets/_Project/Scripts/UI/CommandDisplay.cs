using Adler.Flight;
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
        [SerializeField] private AircraftRig _aircraft;

        private StratagemBay _stratagemBay;

        [Header("만들어 둔 조각")]
        [Tooltip("폭탄 한 칸의 모양. CommandSlot이 붙어 있어야 한다.")]
        [SerializeField] private CommandSlot _slotPrefab;

        [Tooltip("화살표 하나의 모양. 위를 향한 그림으로 두면 방향에 맞게 돌려 쓴다.")]
        [SerializeField] private Image _arrowPrefab;

        [Tooltip("칸들이 늘어설 자리. 비워두면 이 오브젝트 아래에 붙인다.")]
        [SerializeField] private RectTransform _slotRoot;

        [Header("여닫기")]
        [Tooltip("승인된 뒤 그것만 남겨 보여주는 시간(초).\n" +
                 "장전되는 것이 아니라면 이 시간이 지나고 사라진다.")]
        [Min(0f)]
        [SerializeField] private float _confirmSeconds = 0.6f;

        private readonly List<CommandSlot> _slots = new();

        // 창이 닫혀도 남아 있어야 하는 칸. 장전된 폭탄은 쓸 때까지 화면에 붙어 있는다.
        private StratagemDefinition _armed;

        // 승인 직후 잠시 혼자 남는 칸. 장전되지 않는 것도 확인할 시간은 준다.
        private StratagemDefinition _confirming;

        private float _confirmRemaining;
        private bool _commandModeActive;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _stratagemBay = _aircraft != null ? _aircraft.Stratagems : null;

            if (_stratagemBay == null || _slotPrefab == null)
            {
                Debug.LogError($"{nameof(CommandDisplay)}: 기체의 스트라타젬 또는 Slot Prefab이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_slotRoot == null)
            {
                _slotRoot = transform as RectTransform;
            }

            BuildSlots();

            // 이미 재머 범위 안에서 시작할 수도 있다. 신호는 상태가 바뀔 때만 오므로
            // 여기서 한 번 맞춰두지 않으면 범위를 벗어났다 다시 들어와야 표시가 붙는다.
            OnJammedChanged(_stratagemBay.IsJammed);

            // 닫힌 상태로 시작한다. Tab을 눌러야 열린다.
            RefreshVisibility();

            foreach (CommandSlot slot in _slots)
            {
                slot.SnapAlpha();
            }
        }

        private void OnEnable()
        {
            _stratagemBay.CommandProgressed += OnCommandProgressed;
            _stratagemBay.CommandReset += OnCommandReset;
            _stratagemBay.Authorized += OnAuthorized;
            _stratagemBay.Dropped += OnDropped;
            _stratagemBay.Disarmed += OnDropped;
            _stratagemBay.CommandModeChanged += OnCommandModeChanged;
            _stratagemBay.JammedChanged += OnJammedChanged;
        }

        private void OnDisable()
        {
            _stratagemBay.CommandProgressed -= OnCommandProgressed;
            _stratagemBay.CommandReset -= OnCommandReset;
            _stratagemBay.Authorized -= OnAuthorized;
            _stratagemBay.Dropped -= OnDropped;
            _stratagemBay.Disarmed -= OnDropped;
            _stratagemBay.CommandModeChanged -= OnCommandModeChanged;
            _stratagemBay.JammedChanged -= OnJammedChanged;
        }

        /// <summary>
        /// 봉인이 걸리고 풀리는 것을 모든 칸에 알린다.
        /// <para>
        /// 창을 닫지 않는다. 열어서 전부 JAMMED로 바뀐 목록을 보여주는 것이, 열리지 않는
        /// 것보다 무엇이 벌어졌는지를 분명히 알려준다.
        /// </para>
        /// </summary>
        private void OnJammedChanged(bool jammed)
        {
            foreach (CommandSlot slot in _slots)
            {
                slot.SetJammed(jammed);

                // 치던 커맨드는 봉인과 함께 버려졌다. 화살표가 채워진 채로 떨면
                // 아직 이어서 칠 수 있는 것처럼 보인다.
                slot.SetMatchedCount(0);
                slot.SetDimmed(false);
            }
        }

        private void OnCommandModeChanged(bool active)
        {
            _commandModeActive = active;

            if (active)
            {
                _confirming = null;
                _confirmRemaining = 0f;

                foreach (CommandSlot slot in _slots)
                {
                    slot.SetDimmed(false);
                }
            }

            RefreshVisibility();
        }

        /// <summary>
        /// 어느 칸이 보일지 한 곳에서 정한다.
        /// <para>
        /// 창이 열려 있으면 전부, 승인 직후에는 그것만, 그 뒤로는 장전된 것만 남는다.
        /// 이 세 경우를 각자 처리하면 한쪽에서 치운 칸을 다른 쪽이 되살리게 된다.
        /// </para>
        /// </summary>
        private void RefreshVisibility()
        {
            foreach (CommandSlot slot in _slots)
            {
                bool visible = _commandModeActive
                               || (_confirming != null && slot.Stratagem == _confirming)
                               || (_armed != null && slot.Stratagem == _armed);

                slot.SetHidden(!visible);
            }
        }

        private void BuildSlots()
        {
            foreach (StratagemDefinition stratagem in _stratagemBay.Loadout)
            {
                if (stratagem == null)
                {
                    continue;
                }

                CommandSlot slot = Instantiate(_slotPrefab, _slotRoot);
                slot.Bind(stratagem, _arrowPrefab);
                _slots.Add(slot);
            }
        }

        /// <summary>
        /// 쿨타임은 계속 흐르므로 매 프레임 갱신한다. 슬롯 쪽에서 표시되는 초가 바뀔 때만
        /// 글자를 다시 만들기 때문에, 여기서 매번 물어봐도 부담이 되지 않는다.
        /// </summary>
        private void Update()
        {
            if (_confirmRemaining > 0f)
            {
                _confirmRemaining -= Time.deltaTime;

                if (_confirmRemaining <= 0f)
                {
                    _confirmRemaining = 0f;
                    _confirming = null;
                    RefreshVisibility();
                }
            }

            foreach (CommandSlot slot in _slots)
            {
                StratagemDefinition stratagem = slot.Stratagem;

                slot.SetCooldown(
                    _stratagemBay.RemainingCooldown(stratagem),
                    _stratagemBay.CooldownNormalized(stratagem),
                    _stratagemBay.IsExhausted(stratagem));
            }
        }

        private void OnCommandProgressed(IReadOnlyList<CommandDirection> entered)
        {
            foreach (CommandSlot slot in _slots)
            {
                bool matches = Matches(slot.Stratagem, entered);

                // 맞는 것만 진행 상황을 보여준다. 어긋난 것까지 화살표를 채우면
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

        /// <summary>
        /// 폭탄만 장전 상태로 남는다. 재보급처럼 즉시 처리되는 것은 승인과 동시에 끝나므로
        /// 켜둘 표시가 없다.
        /// </summary>
        /// <summary>
        /// 장전되는 것은 쓸 때까지 화면에 남고, 즉시 끝나는 것은 잠깐 보였다 사라진다.
        /// </summary>
        private void OnAuthorized(StratagemDefinition stratagem)
        {
            bool arms = stratagem is BombDefinition;
            _armed = arms ? stratagem : null;
            _confirming = stratagem;
            _confirmRemaining = _confirmSeconds;

            foreach (CommandSlot slot in _slots)
            {
                slot.SetMatchedCount(0);
                slot.SetDimmed(false);
                slot.SetArmed(arms && slot.Stratagem == stratagem);
            }

            RefreshVisibility();
        }

        /// <summary>
        /// 장전이 끝났을 때. 떨궈서든 봉인에 풀려서든 표시는 똑같이 내린다 —
        /// 어느 쪽이든 이제 쓸 수 없다는 사실은 하나다.
        /// </summary>
        private void OnDropped(BombDefinition bomb)
        {
            _armed = null;

            foreach (CommandSlot slot in _slots)
            {
                slot.SetArmed(false);
            }

            RefreshVisibility();
        }

        private static bool Matches(StratagemDefinition stratagem, IReadOnlyList<CommandDirection> entered)
        {
            if (stratagem == null || stratagem.Command.Length < entered.Count)
            {
                return false;
            }

            for (int i = 0; i < entered.Count; i++)
            {
                if (stratagem.Command[i] != entered[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
