using Adler.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Controls
{
    /// <summary>
    /// 조종석의 입력을 한곳에서 읽어 "무엇을 하려는가"로 옮긴다.
    /// <para>
    /// 기능마다 직접 키를 읽으면 같은 키를 두 기능이 쓰게 되는 순간 서로를 알아야 한다.
    /// 실제로 그랬다 — WASD가 조종과 커맨드를 겹쳐 쓰게 되자 조종이 스트라타젬을
    /// 들여다봐야 했다. 누른 것을 뜻으로 옮기는 층이 하나 있으면, 그 조정은 여기서
    /// 끝나고 기능들은 서로를 모른 채로 남는다.
    /// </para>
    /// <para>
    /// 여기서 정하지 않는 것도 있다. 커맨드 창이 열렸는지는 재밍 여부와 자동 열림까지
    /// 얽힌 판단이라 그것을 아는 쪽의 몫이고, 이쪽은 <see cref="IControlSuppressor"/>로
    /// "누가 키보드를 가져갔는가"만 묻는다.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PilotInput : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("읽어올 액션 맵의 이름.")]
        [SerializeField] private string _mapName = "Flight";

        private InputActionMap _map;

        private InputAction _pitch;
        private InputAction _roll;
        // 조작 이름표로 찾는다. 이름별 속성은 읽기 좋으라고 그 위에 얹어둔 것이다.
        //
        // 칸 수는 열거형의 가장 큰 번호를 따른다. 번호를 손으로 다시 세게 두면
        // 조작을 더하거나 뺄 때 이 숫자를 함께 고쳐야 한다는 사실이 어디에도 없다.
        // 빠진 번호는 칸만 빌 뿐 문제가 없다.
        private readonly InputAction[] _actions = new InputAction[SlotCountOf<PilotAction>()];
        private readonly InputAction[] _commands = new InputAction[SlotCountOf<CommandDirection>()];

        private IControlSuppressor[] _suppressors;

        /// <summary>열거형의 가장 큰 번호까지 담을 수 있는 칸 수.</summary>
        private static int SlotCountOf<T>() where T : System.Enum
        {
            int max = 0;

            foreach (int value in System.Enum.GetValues(typeof(T)))
            {
                max = Mathf.Max(max, value);
            }

            return max + 1;
        }

        /// <summary>기수를 올리고 내리는 정도. 키보드를 빼앗겼으면 0이다.</summary>
        public float Pitch => Stick(_pitch);

        /// <summary>기체를 굴리는 정도. 키보드를 빼앗겼으면 0이다.</summary>
        public float Roll => Stick(_roll);

        /// <summary>부스터를 누르고 있는가.</summary>
        public bool Boost => IsHeld(PilotAction.Boost);

        /// <summary>방아쇠를 당기고 있는가.</summary>
        public bool Fire => IsHeld(PilotAction.Fire);

        public bool SwitchWeaponPressed => WasPressed(PilotAction.SwitchWeapon);

        public bool SwitchTargetPressed => WasPressed(PilotAction.SwitchTarget);

        public bool GrapplePressed => WasPressed(PilotAction.Grapple);

        public bool RespawnPressed => WasPressed(PilotAction.Respawn);


        public bool ToggleCommandsPressed => WasPressed(PilotAction.ToggleCommands);

        /// <summary>이번 프레임에 이 방향이 눌렸는가.</summary>
        public bool CommandPressed(CommandDirection direction) => Pressed(_commands[(int)direction]);

        /// <summary>
        /// 이번 프레임에 <b>패드 십자키로</b> 커맨드 방향이 눌렸는가.
        /// <para>
        /// 어느 장치로 눌렸는지는 원시 입력의 사정이라 여기서 답한다. 십자키는
        /// 조종면과 겹치지 않으므로 그것만으로 커맨드 창을 열어도 되지만,
        /// 키보드는 WASD가 조종과 겹쳐서 그럴 수 없다.
        /// </para>
        /// </summary>
        public bool AnyCommandOnGamepad
        {
            get
            {
                foreach (InputAction action in _commands)
                {
                    if (Pressed(action) && action.activeControl?.device is Gamepad)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Awake()
        {
            if (_controls == null)
            {
                Debug.LogError($"{nameof(PilotInput)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            // 키보드를 가져갈 수 있는 것들을 한 번만 찾아둔다. 새로 생겨도 인터페이스만
            // 구현해 이 기체에 붙으면 되고, 여기는 그대로다.
            _suppressors = GetComponentsInChildren<IControlSuppressor>(includeInactive: true);
        }

        private void OnEnable()
        {
            _map = _controls.FindActionMap(_mapName, throwIfNotFound: true);

            _pitch = _map.FindAction("Pitch", throwIfNotFound: true);
            _roll = _map.FindAction("Roll", throwIfNotFound: true);
            _actions[(int)PilotAction.Boost] = _map.FindAction("Boost", throwIfNotFound: true);
            _actions[(int)PilotAction.Fire] = _map.FindAction("Fire", throwIfNotFound: true);
            _actions[(int)PilotAction.SwitchWeapon] = _map.FindAction("SwitchWeapon", throwIfNotFound: true);
            _actions[(int)PilotAction.SwitchTarget] = _map.FindAction("SwitchTarget", throwIfNotFound: true);
            _actions[(int)PilotAction.Grapple] = _map.FindAction("Grapple", throwIfNotFound: true);
            _actions[(int)PilotAction.Respawn] = _map.FindAction("Respawn", throwIfNotFound: true);
            _actions[(int)PilotAction.ToggleCommands] = _map.FindAction("ToggleCommands", throwIfNotFound: true);

            _commands[(int)CommandDirection.Up] = _map.FindAction("CommandUp", throwIfNotFound: true);
            _commands[(int)CommandDirection.Down] = _map.FindAction("CommandDown", throwIfNotFound: true);
            _commands[(int)CommandDirection.Left] = _map.FindAction("CommandLeft", throwIfNotFound: true);
            _commands[(int)CommandDirection.Right] = _map.FindAction("CommandRight", throwIfNotFound: true);

            _map.Enable();
        }

        private void OnDisable() => _map?.Disable();

        /// <summary>이 조작을 누르고 있는가.</summary>
        public bool IsHeld(PilotAction action) => _actions[(int)action]?.IsPressed() == true;

        /// <summary>이번 프레임에 이 조작이 눌렸는가.</summary>
        public bool WasPressed(PilotAction action) => Pressed(_actions[(int)action]);

        private static bool Pressed(InputAction action)
            => action != null && action.WasPressedThisFrame();

        /// <summary>
        /// 조종면 입력. 키보드를 가져간 것이 있으면 키보드 쪽만 버린다.
        /// <para>
        /// 패드는 그대로 둔다. 그쪽은 커맨드가 십자키라 스틱과 겹치지 않으므로, 함께
        /// 막으면 겪지도 않는 문제 때문에 조종을 빼앗기는 셈이 된다.
        /// </para>
        /// </summary>
        private float Stick(InputAction action)
        {
            if (action == null)
            {
                return 0f;
            }

            float value = action.ReadValue<float>();

            if (value == 0f || !KeyboardTaken)
            {
                return value;
            }

            return action.activeControl?.device is Keyboard ? 0f : value;
        }

        private bool KeyboardTaken
        {
            get
            {
                if (_suppressors == null)
                {
                    return false;
                }

                foreach (IControlSuppressor suppressor in _suppressors)
                {
                    if (suppressor != null && suppressor.SuppressesKeyboard)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
