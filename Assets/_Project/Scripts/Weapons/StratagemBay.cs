using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Weapons
{
    /// <summary>
    /// 커맨드를 받아 스트라타젬을 승인한다. 폭탄 장전과 투하도 여기서 맡는다.
    /// <para>
    /// 무엇을 요청하든 절차는 같다. 방향키를 맞게 눌러야 하고, 그동안 손이 조종에서
    /// 떠난다. 그래서 판정을 폭탄 전용으로 두지 않고 한곳에 모았다 — 재보급이든
    /// 나중에 붙일 수리든 커맨드 처리를 다시 짤 일이 없다.
    /// </para>
    /// <para>
    /// 조종은 커맨드를 입력하는 동안에도 살아 있다. 대신 그 몇 초가 대가다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StratagemBay : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("폭탄이 떨어져 나오는 자리. 기체 아래쪽에 빈 오브젝트를 두면 된다.")]
        [SerializeField] private Transform _dropPoint;

        [Tooltip("투하 순간의 속도를 물려받을 기체. 비워두면 부모에서 찾는다.")]
        [SerializeField] private Rigidbody _carrier;

        [Header("요청 가능 목록")]
        [Tooltip("각자 다른 커맨드를 가진다. 입력이 맞아떨어진 것이 승인된다.")]
        [SerializeField] private List<StratagemDefinition> _loadout = new();

        [Header("커맨드")]
        [Tooltip("다음 입력이 이 시간 안에 들어오지 않으면 처음부터 다시 입력해야 한다(초).")]
        [Min(0.1f)]
        [SerializeField] private float _inputTimeout = 1.5f;

        [Tooltip("패드 십자키를 누르면 커맨드 창이 저절로 열린다.\n" +
                 "십자키는 커맨드에만 쓰이므로 여는 버튼을 따로 누를 이유가 없다.\n" +
                 "키보드 방향키는 해당하지 않는다 — 그쪽은 Tab으로 연다.")]
        [SerializeField] private bool _gamepadOpensOnInput = true;

        private InputAction _dropAction;
        private InputAction _toggleAction;
        private readonly InputAction[] _directionActions = new InputAction[4];
        private readonly List<CommandDirection> _entered = new();
        private readonly List<StratagemDefinition> _candidates = new();

        private float _lastInputTime;

        // 십자키로 저절로 열린 창인지. 그렇게 열린 것만 저절로 닫는다.
        private bool _autoOpened;

        // 스트라타젬별 재사용 시각과 쓴 횟수. 요청 절차를 여기서 다루므로 제한도 함께 맡는다.
        private readonly Dictionary<StratagemDefinition, float> _readyAt = new();
        private readonly Dictionary<StratagemDefinition, int> _used = new();

        /// <summary>
        /// 커맨드 입력을 받는 중인지. 꺼져 있으면 방향키가 커맨드로 해석되지 않는다.
        /// </summary>
        public bool CommandModeActive { get; private set; }

        /// <summary>입력 모드가 켜지고 꺼질 때. 커맨드 창이 구독한다.</summary>
        public event Action<bool> CommandModeChanged;

        /// <summary>커맨드가 한 칸 진행됐을 때. 화면에 입력 상황을 보여주는 데 쓴다.</summary>
        public event Action<IReadOnlyList<CommandDirection>> CommandProgressed;

        /// <summary>입력이 틀렸거나 시간이 지나 처음으로 돌아갔을 때.</summary>
        public event Action CommandReset;

        /// <summary>커맨드가 완성됐을 때. 재보급처럼 폭탄이 아닌 것들이 여기서 받아 간다.</summary>
        public event Action<StratagemDefinition> Authorized;

        /// <summary>폭탄을 투하했을 때.</summary>
        public event Action<BombDefinition> Dropped;

        /// <summary>
        /// 투하한 폭탄이 터졌을 때. 폭탄은 던져지고 나면 사라지는 물건이라
        /// 화면 표시가 직접 구독할 수 없으므로, 여기서 대신 받아 전달한다.
        /// </summary>
        public event Action<BombDefinition, BlastReport> Detonated;

        /// <summary>장전된 폭탄. 없으면 null.</summary>
        public BombDefinition ArmedBomb { get; private set; }

        public bool IsArmed => ArmedBomb != null;

        /// <summary>지금까지 입력된 커맨드.</summary>
        public IReadOnlyList<CommandDirection> EnteredCommand => _entered;

        /// <summary>요청 가능한 목록. 화면에 커맨드를 늘어놓는 데 쓴다.</summary>
        public IReadOnlyList<StratagemDefinition> Loadout => _loadout;

        /// <summary>커맨드가 맞아도 제한에 걸려 승인되지 않았을 때.</summary>
        public event Action<StratagemDefinition> Refused;

        /// <summary>남은 쿨타임(초). 쓸 수 있으면 0.</summary>
        public float RemainingCooldown(StratagemDefinition stratagem)
        {
            if (stratagem == null || !_readyAt.TryGetValue(stratagem, out float readyAt))
            {
                return 0f;
            }

            return Mathf.Max(0f, readyAt - Time.time);
        }

        /// <summary>쿨타임 진행도. 1이면 방금 썼고 0이면 다 찼다. 게이지에 그대로 넣는다.</summary>
        public float CooldownNormalized(StratagemDefinition stratagem)
        {
            if (stratagem == null || stratagem.Cooldown <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(RemainingCooldown(stratagem) / stratagem.Cooldown);
        }

        /// <summary>출격 횟수 제한까지 다 써버렸는지.</summary>
        public bool IsExhausted(StratagemDefinition stratagem)
        {
            if (stratagem == null || stratagem.UsesPerSortie <= 0)
            {
                return false;
            }

            return _used.TryGetValue(stratagem, out int used) && used >= stratagem.UsesPerSortie;
        }

        /// <summary>지금 부를 수 있는지.</summary>
        public bool IsReady(StratagemDefinition stratagem)
        {
            return stratagem != null
                   && RemainingCooldown(stratagem) <= 0f
                   && !IsExhausted(stratagem);
        }

        /// <summary>출격을 다시 시작할 때 제한을 되돌린다.</summary>
        public void ResetRestrictions()
        {
            _readyAt.Clear();
            _used.Clear();
        }

        private void Awake()
        {
            if (_carrier == null)
            {
                _carrier = GetComponentInParent<Rigidbody>();
            }

            if (_dropPoint == null)
            {
                _dropPoint = transform;
            }
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                Debug.LogError($"{nameof(StratagemBay)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            InputActionMap map = _controls.FindActionMap("Flight", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Up] = map.FindAction("CommandUp", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Down] = map.FindAction("CommandDown", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Left] = map.FindAction("CommandLeft", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Right] = map.FindAction("CommandRight", throwIfNotFound: true);
            _dropAction = map.FindAction("DropBomb", throwIfNotFound: true);
            _toggleAction = map.FindAction("ToggleCommands", throwIfNotFound: true);

            foreach (InputAction action in _directionActions)
            {
                action.Enable();
            }

            _dropAction.Enable();
            _toggleAction.Enable();
        }

        private void OnDisable()
        {
            foreach (InputAction action in _directionActions)
            {
                action?.Disable();
            }

            _dropAction?.Disable();
            _toggleAction?.Disable();
        }

        private void Update()
        {
            if (_toggleAction.WasPressedThisFrame())
            {
                _autoOpened = false;
                SetCommandMode(!CommandModeActive);
            }
            else if (!CommandModeActive && ShouldAutoOpen())
            {
                _autoOpened = true;
                SetCommandMode(true);
            }

            if (CommandModeActive)
            {
                // 같은 프레임에 이어서 읽는다. 창을 연 그 십자키 입력이 커맨드의 첫 칸이 된다.
                ExpireStaleInput();
                ReadCommandInput();
            }

            // 투하는 커맨드 창과 무관하다. 장전해 둔 폭탄은 창을 닫은 뒤에 떨구게 된다.
            if (_dropAction.WasPressedThisFrame())
            {
                TryDrop();
            }
        }

        /// <summary>커맨드 입력 모드를 켜고 끈다.</summary>
        public void SetCommandMode(bool active)
        {
            if (CommandModeActive == active)
            {
                return;
            }

            CommandModeActive = active;

            // 창을 닫으면 치던 커맨드는 버린다. 남겨두면 다시 열었을 때 예전 입력이
            // 이어져서, 몇 번째를 치고 있었는지 알 수 없는 상태로 시작한다.
            if (!active)
            {
                ResetCommand();
            }

            CommandModeChanged?.Invoke(active);
        }

        /// <summary>
        /// 십자키를 눌렀는지 본다. 어느 장치에서 왔는지까지 확인하는 이유는,
        /// 키보드 방향키까지 창을 열면 Tab으로 여는 규칙이 무의미해지기 때문이다.
        /// </summary>
        private bool ShouldAutoOpen()
        {
            if (!_gamepadOpensOnInput)
            {
                return false;
            }

            foreach (InputAction action in _directionActions)
            {
                if (action.WasPressedThisFrame() && action.activeControl?.device is Gamepad)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>입력이 끊긴 채로 시간이 지나면 처음부터 다시 받는다.</summary>
        private void ExpireStaleInput()
        {
            if (_entered.Count == 0 || Time.time - _lastInputTime < _inputTimeout)
            {
                return;
            }

            // 저절로 열린 창은 저절로 닫는다. 십자키에서 손을 뗐는데 창이 남아 있으면
            // 다음에 무심코 누른 십자키가 커맨드로 먹힌다.
            if (_autoOpened)
            {
                CloseAutoOpened();
                return;
            }

            ResetCommand();
        }

        private void CloseAutoOpened()
        {
            _autoOpened = false;
            SetCommandMode(false);
        }

        private void ReadCommandInput()
        {
            for (int i = 0; i < _directionActions.Length; i++)
            {
                if (_directionActions[i].WasPressedThisFrame())
                {
                    Accept((CommandDirection)i);
                    return; // 한 프레임에 한 방향만 받는다
                }
            }
        }

        private void Accept(CommandDirection direction)
        {
            _lastInputTime = Time.time;
            _entered.Add(direction);

            if (!RefreshCandidates())
            {
                // 틀렸다. 다만 방금 누른 것을 새 커맨드의 첫 입력으로 다시 본다.
                // 완전히 버리면 한 번 어긋났을 때 손을 멈췄다 다시 시작해야 한다.
                _entered.Clear();
                _entered.Add(direction);
                CommandReset?.Invoke();

                if (!RefreshCandidates())
                {
                    _entered.Clear();
                    return;
                }
            }

            CommandProgressed?.Invoke(_entered);

            foreach (StratagemDefinition candidate in _candidates)
            {
                if (candidate.Command.Length == _entered.Count)
                {
                    Authorize(candidate);
                    return;
                }
            }
        }

        /// <summary>
        /// 지금까지의 입력으로 아직 가능한 것들을 추린다.
        /// <para>
        /// 쿨타임 중이거나 다 쓴 것은 후보에서 빠진다. 그래서 그 커맨드를 치기 시작하면
        /// 곧바로 어긋난 입력으로 처리되고, 끝까지 다 친 뒤에 거절당하는 일이 없다.
        /// </para>
        /// </summary>
        private bool RefreshCandidates()
        {
            _candidates.Clear();

            foreach (StratagemDefinition stratagem in _loadout)
            {
                if (stratagem == null || stratagem.Command.Length < _entered.Count || !IsReady(stratagem))
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < _entered.Count; i++)
                {
                    if (stratagem.Command[i] != _entered[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    _candidates.Add(stratagem);
                }
            }

            return _candidates.Count > 0;
        }

        private void Authorize(StratagemDefinition stratagem)
        {
            _entered.Clear();
            _candidates.Clear();

            // 커맨드는 맞았지만 아직 부를 수 없는 경우다. 조용히 넘기지 않고 알린다 —
            // 입력이 틀린 것과 쿨타임에 걸린 것은 플레이어에게 다른 이야기다.
            if (!IsReady(stratagem))
            {
                Refused?.Invoke(stratagem);
                return;
            }

            // 폭탄은 여기서 장전만 해 둔다. 쿨타임은 실제로 떨군 뒤에 흐르기 시작한다.
            // 승인 시점부터 재면 장전해 두고 기회를 기다리는 동안 쿨타임이 소진되어,
            // 좋은 진입각을 노리는 것이 손해가 된다.
            if (stratagem is BombDefinition bomb)
            {
                ArmedBomb = bomb;
            }
            else
            {
                // 재보급처럼 승인과 동시에 끝나는 것은 그 자리에서 쓴 것으로 친다.
                MarkUsed(stratagem);
            }

            Authorized?.Invoke(stratagem);

            // 요청이 끝났으니 저절로 열린 창은 물러난다.
            if (_autoOpened)
            {
                CloseAutoOpened();
            }
        }

        /// <summary>쿨타임을 걸고 사용 횟수를 센다. 실제로 쓰인 시점에 부른다.</summary>
        private void MarkUsed(StratagemDefinition stratagem)
        {
            if (stratagem.Cooldown > 0f)
            {
                _readyAt[stratagem] = Time.time + stratagem.Cooldown;
            }

            if (stratagem.UsesPerSortie > 0)
            {
                _used[stratagem] = (_used.TryGetValue(stratagem, out int used) ? used : 0) + 1;
            }
        }

        private void ResetCommand()
        {
            _entered.Clear();
            _candidates.Clear();
            CommandReset?.Invoke();
        }

        private void TryDrop()
        {
            if (!IsArmed)
            {
                return;
            }

            BombDefinition bomb = ArmedBomb;
            if (bomb.Prefab == null)
            {
                Debug.LogError($"{nameof(StratagemBay)}: '{bomb.DisplayName}'에 프리팹이 지정되지 않았습니다.", this);
                return;
            }

            GameObject instance = Instantiate(bomb.Prefab, _dropPoint.position, _dropPoint.rotation);

            // 기체의 속도를 물려받아야 앞으로 던져진다. 그냥 놓으면 제자리에서 떨어져
            // 조준한 곳보다 한참 뒤에 떨어진다.
            if (instance.TryGetComponent(out Rigidbody body) && _carrier != null)
            {
                body.linearVelocity = _carrier.linearVelocity;
            }

            if (instance.TryGetComponent(out Bomb component))
            {
                component.Detonated += report => Detonated?.Invoke(bomb, report);
                component.Arm(bomb, _carrier != null ? _carrier.gameObject : gameObject);
            }
            else
            {
                Debug.LogError($"{nameof(StratagemBay)}: '{bomb.DisplayName}'의 프리팹에 {nameof(Bomb)}이 없습니다.", this);
            }

            ArmedBomb = null;
            MarkUsed(bomb);
            Dropped?.Invoke(bomb);
        }
    }
}
