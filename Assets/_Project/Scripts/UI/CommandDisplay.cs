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
        [SerializeField] private StratagemBay _stratagemBay;

        [Header("만들어 둔 조각")]
        [Tooltip("폭탄 한 칸의 모양. CommandSlot이 붙어 있어야 한다.")]
        [SerializeField] private CommandSlot _slotPrefab;

        [Tooltip("화살표 하나의 모양. 위를 향한 그림으로 두면 방향에 맞게 돌려 쓴다.")]
        [SerializeField] private Image _arrowPrefab;

        [Tooltip("칸들이 늘어설 자리. 비워두면 이 오브젝트 아래에 붙인다.")]
        [SerializeField] private RectTransform _slotRoot;

        [Header("여닫기")]
        [Tooltip("보이고 숨길 대상. 비워두면 칸들이 늘어선 자리를 쓴다.\n" +
                 "오브젝트를 끄지 않고 투명도로 감춘다 — 이 스크립트가 그 안에 있으면 " +
                 "함께 멈춰 다시 열 수 없게 된다.")]
        [SerializeField] private CanvasGroup _panel;

        [Tooltip("여닫히는 속도. 클수록 즉각적이다.")]
        [Min(0.1f)]
        [SerializeField] private float _fadeSpeed = 14f;

        private readonly List<CommandSlot> _slots = new();
        private float _targetAlpha;

        private void Awake()
        {
            if (_stratagemBay == null || _slotPrefab == null)
            {
                Debug.LogError($"{nameof(CommandDisplay)}: Stratagem Bay 또는 Slot Prefab이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_slotRoot == null)
            {
                _slotRoot = transform as RectTransform;
            }

            if (_panel == null && _slotRoot != null)
            {
                _panel = _slotRoot.GetComponent<CanvasGroup>();
                if (_panel == null)
                {
                    _panel = _slotRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            BuildSlots();

            // 닫힌 상태로 시작한다. Tab을 눌러야 열린다.
            _targetAlpha = 0f;
            if (_panel != null)
            {
                _panel.alpha = 0f;
                _panel.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            _stratagemBay.CommandProgressed += OnCommandProgressed;
            _stratagemBay.CommandReset += OnCommandReset;
            _stratagemBay.Authorized += OnAuthorized;
            _stratagemBay.Dropped += OnDropped;
            _stratagemBay.CommandModeChanged += OnCommandModeChanged;
        }

        private void OnDisable()
        {
            _stratagemBay.CommandProgressed -= OnCommandProgressed;
            _stratagemBay.CommandReset -= OnCommandReset;
            _stratagemBay.Authorized -= OnAuthorized;
            _stratagemBay.Dropped -= OnDropped;
            _stratagemBay.CommandModeChanged -= OnCommandModeChanged;
        }

        private void OnCommandModeChanged(bool active)
        {
            _targetAlpha = active ? 1f : 0f;

            if (_panel != null)
            {
                _panel.blocksRaycasts = active;
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
            if (_panel != null && !Mathf.Approximately(_panel.alpha, _targetAlpha))
            {
                _panel.alpha = Mathf.MoveTowards(_panel.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
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
        private void OnAuthorized(StratagemDefinition stratagem)
        {
            bool arms = stratagem is BombDefinition;

            foreach (CommandSlot slot in _slots)
            {
                slot.SetMatchedCount(0);
                slot.SetDimmed(false);
                slot.SetArmed(arms && slot.Stratagem == stratagem);
            }
        }

        private void OnDropped(BombDefinition bomb)
        {
            foreach (CommandSlot slot in _slots)
            {
                slot.SetArmed(false);
            }
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
