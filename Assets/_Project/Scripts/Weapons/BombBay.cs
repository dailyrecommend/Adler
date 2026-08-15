using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Weapons
{
    /// <summary>
    /// 폭탄 승인과 투하를 맡는다.
    /// <para>
    /// 폭탄은 기체에 실려 있는 것이 아니라 매번 요청해서 허가받는 물건이다. 방향키로
    /// 커맨드를 맞게 입력하면 한 발이 장전되고, 쓰고 나면 다시 입력해야 한다.
    /// </para>
    /// <para>
    /// 조종은 커맨드를 입력하는 동안에도 그대로 살아 있다. 대신 손이 방향키에 가 있는
    /// 몇 초가 대가다 — 그 사이에는 기총을 쏠 수 없고 지형도 봐야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BombBay : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("폭탄이 떨어져 나오는 자리. 기체 아래쪽에 빈 오브젝트를 두면 된다.")]
        [SerializeField] private Transform _dropPoint;

        [Tooltip("투하 순간의 속도를 물려받을 기체. 비워두면 부모에서 찾는다.")]
        [SerializeField] private Rigidbody _carrier;

        [Header("탑재 가능 폭탄")]
        [Tooltip("각자 다른 커맨드를 가진다. 입력이 맞아떨어진 폭탄이 장전된다.")]
        [SerializeField] private List<BombDefinition> _loadout = new();

        [Header("커맨드")]
        [Tooltip("다음 입력이 이 시간 안에 들어오지 않으면 처음부터 다시 입력해야 한다(초).")]
        [Min(0.1f)]
        [SerializeField] private float _inputTimeout = 1.5f;

        private InputAction _dropAction;
        private readonly InputAction[] _directionActions = new InputAction[4];
        private readonly List<CommandDirection> _entered = new();
        private readonly List<BombDefinition> _candidates = new();

        private float _lastInputTime;

        /// <summary>커맨드가 한 칸 진행됐을 때. 화면에 입력 상황을 보여주는 데 쓴다.</summary>
        public event Action<IReadOnlyList<CommandDirection>> CommandProgressed;

        /// <summary>입력이 틀렸거나 시간이 지나 처음으로 돌아갔을 때.</summary>
        public event Action CommandReset;

        /// <summary>커맨드가 완성돼 한 발이 장전됐을 때.</summary>
        public event Action<BombDefinition> Authorized;

        /// <summary>투하했을 때.</summary>
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
                Debug.LogError($"{nameof(BombBay)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            InputActionMap map = _controls.FindActionMap("Flight", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Up] = map.FindAction("CommandUp", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Down] = map.FindAction("CommandDown", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Left] = map.FindAction("CommandLeft", throwIfNotFound: true);
            _directionActions[(int)CommandDirection.Right] = map.FindAction("CommandRight", throwIfNotFound: true);
            _dropAction = map.FindAction("DropBomb", throwIfNotFound: true);

            foreach (InputAction action in _directionActions)
            {
                action.Enable();
            }

            _dropAction.Enable();
        }

        private void OnDisable()
        {
            foreach (InputAction action in _directionActions)
            {
                action?.Disable();
            }

            _dropAction?.Disable();
        }

        private void Update()
        {
            ExpireStaleInput();
            ReadCommandInput();

            if (_dropAction.WasPressedThisFrame())
            {
                TryDrop();
            }
        }

        /// <summary>입력이 끊긴 채로 시간이 지나면 처음부터 다시 받는다.</summary>
        private void ExpireStaleInput()
        {
            if (_entered.Count == 0 || Time.time - _lastInputTime < _inputTimeout)
            {
                return;
            }

            ResetCommand();
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

            foreach (BombDefinition candidate in _candidates)
            {
                if (candidate.Command.Length == _entered.Count)
                {
                    Authorize(candidate);
                    return;
                }
            }
        }

        /// <summary>지금까지의 입력으로 아직 가능한 폭탄들을 추린다.</summary>
        private bool RefreshCandidates()
        {
            _candidates.Clear();

            foreach (BombDefinition bomb in _loadout)
            {
                if (bomb == null || bomb.Command.Length < _entered.Count)
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < _entered.Count; i++)
                {
                    if (bomb.Command[i] != _entered[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    _candidates.Add(bomb);
                }
            }

            return _candidates.Count > 0;
        }

        private void Authorize(BombDefinition bomb)
        {
            ArmedBomb = bomb;
            _entered.Clear();
            _candidates.Clear();
            Authorized?.Invoke(bomb);
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
                Debug.LogError($"{nameof(BombBay)}: '{bomb.DisplayName}'에 프리팹이 지정되지 않았습니다.", this);
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
                Debug.LogError($"{nameof(BombBay)}: '{bomb.DisplayName}'의 프리팹에 {nameof(Bomb)}이 없습니다.", this);
            }

            ArmedBomb = null;
            Dropped?.Invoke(bomb);
        }
    }
}
