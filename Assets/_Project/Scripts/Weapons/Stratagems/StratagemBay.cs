using Adler.Abilities;
using Adler.Combat;
using Adler.Controls;
using Adler.Core;
using Adler.Flight;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 커맨드를 받아 스트라타젬을 승인한다.
    /// <para>
    /// 무엇을 요청하든 절차는 같다. 방향키를 맞게 눌러야 하고, 그동안 손이 조종에서
    /// 떠난다. 승인까지가 이곳의 일이고, 무엇이 일어나는지는 행동 쪽이 맡는다 —
    /// 그래서 스트라타젬이 늘어도 여기는 그대로다.
    /// </para>
    /// <para>
    /// 조종은 커맨드를 입력하는 동안에도 살아 있다. 대신 그 몇 초가 대가다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StratagemBay : MonoBehaviour, IDebuffSource, IControlSuppressor
    {
        [Header("참조")]
        [Tooltip("입력을 읽어오는 곳. 비워두면 이 기체에서 찾는다.")]
        [SerializeField] private PilotInput _input;

        [Tooltip("이 장비를 실은 기체. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("봉인당했을 때 디버프 목록에 올릴 것. JAMMED로 만들어 둔 에셋.\n" +
                 "비워두면 봉인은 그대로 걸리되 목록에는 뜨지 않는다.")]
        [SerializeField] private DebuffDefinition _jammedDebuff;

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

        [Tooltip("Tab으로 창을 연 직후 이 시간 동안 커맨드를 받지 않는다(초).\n" +
                 "WASD가 조종과 커맨드를 겸하므로, 창을 여는 순간 손은 이미 그 위에 있다.\n" +
                 "누르고 있던 조종 입력이 커맨드 첫 칸으로 먹히는 것을 막는다.")]
        [Min(0f)]
        [SerializeField] private float _openGuardSeconds = 0.1f;


        // 어느 커맨드를 치고 있는지 알아내는 일은 이쪽이 맡는다. 여기는 입력을 읽어
        // 넘기고 결과를 알릴 뿐이다.
        private AbilityRunner _abilities;
        private CommandRecognizer _recognizer;

        private float _lastInputTime;

        // 십자키로 저절로 열린 창인지. 그렇게 열린 것만 저절로 닫는다.
        private bool _autoOpened;

        // 이 시각까지는 커맨드를 받지 않는다. 창을 연 그 손가락이 첫 칸이 되지 않게 한다.
        private Clock _clock;
        private float _inputGuardUntil;

        /// <summary>
        /// 커맨드 입력을 받는 중인지. 꺼져 있으면 방향키가 커맨드로 해석되지 않는다.
        /// </summary>
        public bool CommandModeActive { get; private set; }

        /// <summary>
        /// 커맨드 창이 열려 있는 동안 키보드 조종을 멈춘다.
        /// <para>
        /// WASD가 조종과 커맨드를 함께 맡으므로, 창이 열린 채로 커맨드를 치면 기수가
        /// 같이 움직인다. 커맨드는 몇 초를 잡아먹는 일이라 그동안 기체가 제멋대로
        /// 꺾이면, 입력을 마치고 났을 때 어디를 향하고 있을지 알 수 없다.
        /// </para>
        /// </summary>
        public bool SuppressesKeyboard => CommandModeActive;

        /// <summary>입력 모드가 켜지고 꺼질 때. 커맨드 창이 구독한다.</summary>
        public event Action<bool> CommandModeChanged;

        /// <summary>커맨드가 한 칸 진행됐을 때. 화면에 입력 상황을 보여주는 데 쓴다.</summary>
        public event Action<IReadOnlyList<CommandDirection>> CommandProgressed;

        /// <summary>입력이 틀렸거나 시간이 지나 처음으로 돌아갔을 때.</summary>
        public event Action CommandReset;

        /// <summary>커맨드가 완성돼 승인됐을 때. 화면 표시가 받아 간다.</summary>
        public event Action<StratagemDefinition> Authorized;

        /// <summary>지금까지 입력된 커맨드.</summary>
        public IReadOnlyList<CommandDirection> EnteredCommand => _recognizer.Entered;

        /// <summary>요청 가능한 목록. 화면에 커맨드를 늘어놓는 데 쓴다.</summary>
        public IReadOnlyList<StratagemDefinition> Loadout => _loadout;

        /// <summary>커맨드가 맞아도 제한에 걸려 승인되지 않았을 때.</summary>
        public event Action<StratagemDefinition> Refused;

        /// <summary>봉인 상태가 바뀔 때. 화면 표시가 구독한다.</summary>
        public event Action<bool> JammedChanged;

        /// <summary>
        /// 재머 범위 안이라 스트라타젬을 쓸 수 없는 상태인지.
        /// </summary>
        public bool IsJammed { get; private set; }

        /// <summary>봉인하고 있는 재머. 없으면 null. 어느 쪽을 처리해야 하는지 알려준다.</summary>
        public StratagemJammer Jammer { get; private set; }

        /// <summary>
        /// 봉인을 디버프 목록에 내놓는다.
        /// <para>
        /// 명시적으로 구현해 겉으로 드러내지 않는다. 이건 화면에 줄 하나를 띄우기 위한
        /// 것이지, 다른 코드가 봉인 여부를 물어볼 창구가 아니다 — 그건 <see cref="IsJammed"/>다.
        /// </para>
        /// </summary>
        void IDebuffSource.CollectDebuffs(List<DebuffDefinition> into)
        {
            if (IsJammed && _jammedDebuff != null)
            {
                into.Add(_jammedDebuff);
            }
        }

        // 쿨타임과 횟수의 주인은 실행기다. 여기는 화면이 묻기 좋게 창구만 내준다 —
        // 화면이 실행기를 직접 알게 하면, 커맨드 화면이 스트라타젬 아닌 행동까지
        // 뒤질 수 있는 문이 열린다.

        /// <summary>남은 쿨타임(초). 쓸 수 있으면 0.</summary>
        public float RemainingCooldown(StratagemDefinition stratagem)
            => _abilities != null ? _abilities.RemainingCooldown(stratagem) : 0f;

        /// <summary>쿨타임 진행도. 1이면 방금 썼고 0이면 다 찼다. 게이지에 그대로 넣는다.</summary>
        public float CooldownNormalized(StratagemDefinition stratagem)
            => _abilities != null ? _abilities.CooldownNormalized(stratagem) : 0f;

        /// <summary>출격 횟수 제한까지 다 써버렸는지.</summary>
        public bool IsExhausted(StratagemDefinition stratagem)
            => _abilities != null && _abilities.IsExhausted(stratagem);

        /// <summary>
        /// 지금 부를 수 있는지. 제한만 본다.
        /// <para>
        /// 행동 스스로의 조건은 묻지 않는다. 그것까지 물으면 가득 찬 기체의 수리
        /// 커맨드가 아예 안 쳐지는 것처럼 보이는데, 그때 알려야 할 것은 "입력이
        /// 틀렸다"가 아니라 "맞았지만 거절됐다"다.
        /// </para>
        /// </summary>
        public bool IsReady(StratagemDefinition stratagem)
            => stratagem != null && (_abilities == null || !_abilities.IsRestricted(stratagem));

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _input = _input != null ? _input : _aircraft?.Input;
            // 기체 전체에서 찾는다. 부모 방향만 보면 실행기가 형제 오브젝트에 있을 때
            // 놓치는데, 컴포넌트를 무리 지어 배치하면 형제로 놓이는 것이 자연스럽다.
            _abilities = _aircraft != null ? _aircraft.Abilities : null;

            if (_input == null)
            {
                Debug.LogError($"{nameof(StratagemBay)}: {nameof(PilotInput)}을 찾지 못했습니다.", this);
                enabled = false;
            }

            // 제한은 실행기가 세고, 인식기는 그 결과만 물어본다.
            _recognizer = new CommandRecognizer(_loadout, IsReady);
        }

        private void Update()
        {
            UpdateJamming();

            // 봉인 중에도 창은 열린다. 열리지 않으면 눌러도 아무 일이 없는 것과 같아서
            // 봉인당한 것인지 키가 안 먹는 것인지 구분이 안 된다. 열어서 봉인당한 목록을
            // 보여주는 편이 무엇이 벌어졌는지 한 번에 알려준다.
            if (_input.ToggleCommandsPressed)
            {
                _autoOpened = false;
                SetCommandMode(!CommandModeActive);

                // 십자키로 저절로 열린 창에는 걸지 않는다. 그쪽은 창을 연 그 입력이
                // 커맨드의 첫 칸이 되는 것이 규칙이고, 겹치는 키도 없다.
                if (CommandModeActive)
                {
                    _inputGuardUntil = _clock.Now + _openGuardSeconds;
                }
            }
            else if (!IsJammed && !CommandModeActive && ShouldAutoOpen())
            {
                _autoOpened = true;
                SetCommandMode(true);
            }

            // 다만 커맨드는 한 칸도 들어가지 않는다. 화살표가 채워지지 않는 것 자체가
            // 지금 통하지 않는다는 답이 된다.
            if (CommandModeActive && !IsJammed && _clock.Now >= _inputGuardUntil)
            {
                // 같은 프레임에 이어서 읽는다. 창을 연 그 십자키 입력이 커맨드의 첫 칸이 된다.
                ExpireStaleInput();
                ReadCommandInput();
            }

        }

        /// <summary>
        /// 재머 범위에 들고 나는 것을 살핀다.
        /// <para>
        /// 봉인에 걸리면 치던 커맨드를 버린다. 창은 닫지 않는다 — 열려 있던 것이 저절로
        /// 닫히면 무엇 때문에 닫혔는지 알 수 없고, 봉인당한 목록을 보여줄 자리도 사라진다.
        /// </para>
        /// </summary>
        private void UpdateJamming()
        {
            StratagemJammer jammer = StratagemJammer.NearestJammer(transform.position);
            bool jammed = jammer != null;

            Jammer = jammer;

            if (jammed == IsJammed)
            {
                return;
            }

            IsJammed = jammed;

            if (jammed)
            {
                _autoOpened = false;
                ResetCommand();
            }

            JammedChanged?.Invoke(jammed);
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

            return _input.AnyCommandOnGamepad;
        }

        /// <summary>입력이 끊긴 채로 시간이 지나면 처음부터 다시 받는다.</summary>
        private void ExpireStaleInput()
        {
            if (_recognizer.Entered.Count == 0 || _clock.Now - _lastInputTime < _inputTimeout)
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
            for (int i = 0; i < 4; i++)
            {
                if (_input.CommandPressed((CommandDirection)i))
                {
                    Accept((CommandDirection)i);
                    return; // 한 프레임에 한 방향만 받는다
                }
            }
        }

        /// <summary>
        /// 방향 하나를 인식기에 넘기고, 그 결과를 화면에 알린다.
        /// <para>
        /// 무엇이 맞았는지 판단하는 일은 이제 인식기의 몫이다. 여기 남은 것은 언제
        /// 입력이 들어왔는지 기록하고 결과에 맞는 신호를 보내는 것뿐이다.
        /// </para>
        /// </summary>
        private void Accept(CommandDirection direction)
        {
            _lastInputTime = _clock.Now;

            switch (_recognizer.Accept(direction))
            {
                case CommandInput.Rejected:
                    return;

                case CommandInput.Restarted:
                    // 어긋났다는 것과 새로 시작했다는 것을 함께 알린다. 앞의 것만
                    // 보내면 화면이 빈 상태로 남아 첫 칸이 들어간 것이 안 보인다.
                    CommandReset?.Invoke();
                    CommandProgressed?.Invoke(_recognizer.Entered);
                    return;

                case CommandInput.Progressed:
                    CommandProgressed?.Invoke(_recognizer.Entered);
                    return;

                case CommandInput.Accepted:
                    // 마지막 칸도 채워진 것은 채워진 것이다. 승인만 알리고 넘어가면
                    // 그 한 번의 입력에는 소리도 표시도 없어서, 다 쳤는데 마지막
                    // 화살표만 반응하지 않은 것처럼 보인다.
                    CommandProgressed?.Invoke(_recognizer.Entered);
                    Authorize(_recognizer.Completed);
                    return;
            }
        }

        private void Authorize(StratagemDefinition stratagem)
        {
            _recognizer.Reset();

            // 커맨드는 맞았지만 아직 부를 수 없는 경우다. 조용히 넘기지 않고 알린다 —
            // 입력이 틀린 것과 쿨타임에 걸린 것은 플레이어에게 다른 이야기다.
            if (!IsReady(stratagem))
            {
                Refused?.Invoke(stratagem);
                return;
            }

            // 실행도 셈도 행동 쪽이 맡는다. 여기서 무엇을 하는지까지 알면 스트라타젬을
            // 늘릴 때마다 이 분기가 함께 늘어나고, 여기서 세면 세는 곳이 둘이 된다.
            if (_abilities != null && !_abilities.TryUse(stratagem))
            {
                // 커맨드는 맞았으니 창은 닫는다. 다만 쓴 것으로 치지는 않는다.
                Refused?.Invoke(stratagem);

                _autoOpened = false;
                SetCommandMode(false);
                return;
            }

            Authorized?.Invoke(stratagem);

            // 요청이 끝났으니 창은 물러난다. Tab으로 열었든 십자키로 열렸든 마찬가지다 —
            // 승인된 뒤에도 열려 있으면 방향키가 계속 커맨드로 먹히고, 무엇을 받았는지도
            // 화면에 남지 않는다.
            _autoOpened = false;
            SetCommandMode(false);
        }
        private void ResetCommand()
        {
            _recognizer.Reset();
            CommandReset?.Invoke();
        }
    }
}
