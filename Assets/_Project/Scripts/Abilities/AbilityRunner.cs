using System;
using System.Collections.Generic;
using Adler.Core;
using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 이 기체가 할 수 있는 행동들을 들고, 한 번에 하나를 굴린다.
    /// <para>
    /// 행동의 종류를 모른다. 무엇을 만들지는 <see cref="AbilitySpec"/>이, 무엇으로
    /// 끊을 수 있는지는 <see cref="TransitionTable"/>이 답하므로, 행동이 스무 개로
    /// 늘어도 이 파일은 그대로다.
    /// </para>
    /// <para>
    /// 연출은 여기를 구독한다. 실행기가 소리나 화면을 부르는 순간 행동 체계가 연출에
    /// 묶여서, 연출을 하나 붙일 때마다 이쪽이 그것을 알아야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityRunner : MonoBehaviour
    {
        [Header("가진 행동")]
        [Tooltip("이 기체가 쓸 수 있는 것들. 요청은 이 목록의 것만 받는다.")]
        [SerializeField] private List<AbilitySpec> _granted = new();

        [Header("규칙")]
        [Tooltip("무엇을 무엇으로 끊을 수 있는지. 비워두면 끊지 못한다 —\n" +
                 "돌고 있는 행동이 끝나야 다음이 시작된다.")]
        [SerializeField] private TransitionTable _transitions;

        private readonly Dictionary<AbilitySpec, Ability> _abilities = new();
        private readonly Dictionary<AbilitySpec, float> _readyAt = new();

        private Ability _running;
        private Clock _clock;

        /// <summary>행동이 시작될 때. 연출이 구독한다.</summary>
        public event Action<Ability> Started;

        /// <summary>행동이 끝날 때. 끊겨서 끝난 경우도 포함한다.</summary>
        public event Action<Ability> Ended;

        /// <summary>지금 돌고 있는 행동. 없으면 null.</summary>
        public Ability Running => _running != null && _running.IsRunning ? _running : null;

        /// <summary>
        /// 이 꼬리표를 단 행동이 지금 돌고 있는지.
        /// <para>
        /// 이름이 아니라 꼬리표로 묻는다. 화면 효과나 소리가 "수리 중인가"를 알고
        /// 싶을 때 특정 스트라타젬의 이름을 알아야 한다면, 수리하는 스트라타젬을
        /// 하나 더 만드는 순간 그 조건이 조용히 어긋난다.
        /// </para>
        /// </summary>
        public bool IsRunning(AbilityTag tag) => Running?.Spec?.Has(tag) == true;

        /// <summary>
        /// 행동이 세상에 닿는 통로. 기체가 자기 것을 끼워 넣는다.
        /// <para>
        /// 실행기가 스스로 찾지 않는다. 찾게 두면 이 파일이 기체의 부품 이름을 알아야
        /// 하고, 적기에 같은 실행기를 쓸 때 그 이름들이 맞지 않는다.
        /// </para>
        /// </summary>
        public AbilityContext Context { get; set; }

        /// <summary>이 행동을 지금 시작할 수 있는지. 쿨타임과 전이 규칙을 모두 본다.</summary>
        public bool CanUse(AbilitySpec spec)
        {
            if (spec == null || !_abilities.TryGetValue(spec, out Ability ability))
            {
                return false;
            }

            if (_clock != null && _readyAt.TryGetValue(spec, out float readyAt) && _clock.Now < readyAt)
            {
                return false;
            }

            return Allows(spec) && ability.CanActivate(Context);
        }

        /// <summary>
        /// 행동을 시작한다. 못 하면 아무 일도 일어나지 않는다.
        /// <para>
        /// 거절을 예외로 알리지 않는다. 쿨타임이 남았거나 끊을 수 없는 것이 돌고 있는
        /// 상황은 잘못이 아니라 흔한 일이고, 부르는 쪽은 대개 매 프레임 눌러보는
        /// 입력이라 그때마다 판단을 되풀이할 이유가 없다.
        /// </para>
        /// </summary>
        public bool TryUse(AbilitySpec spec)
        {
            if (!CanUse(spec))
            {
                return false;
            }

            Stop();

            _running = _abilities[spec];
            _running.Begin(Context);

            Started?.Invoke(_running);
            return true;
        }

        /// <summary>돌고 있는 행동을 끊는다. 쿨타임은 여기서 시작된다.</summary>
        public void Stop()
        {
            if (_running == null || !_running.IsRunning)
            {
                return;
            }

            Ability ended = _running;

            ended.End(Context);
            _running = null;

            StartCooldown(ended.Spec);
            Ended?.Invoke(ended);
        }

        private void Awake()
        {
            _clock = TimeScale.For(this);

            // 가진 것들을 미리 만들어 둔다. 쓸 때 만들면 첫 사용만 유난히 무겁고,
            // 그 무거움이 하필 처음 눌렀을 때 나타난다.
            foreach (AbilitySpec spec in _granted)
            {
                if (spec == null || _abilities.ContainsKey(spec))
                {
                    continue;
                }

                Ability ability = spec.Create();

                if (ability == null)
                {
                    Debug.LogError(
                        $"{nameof(AbilityRunner)}: '{spec.DisplayName}'이 행동을 만들지 못했습니다. " +
                        $"{nameof(AbilitySpec)}을 물려받아 Create를 채우세요.", this);
                    continue;
                }

                _abilities.Add(spec, ability);
            }
        }

        private void Update()
        {
            if (_running == null)
            {
                return;
            }

            _running.Tick(Context);

            // 스스로 끝난 것을 치운다. 끝났는데 남아 있으면 다음 행동이 "끊는" 것으로
            // 판정되어, 끊을 수 없는 행동 뒤에는 아무것도 시작하지 못한다.
            if (_running.Phase == AbilityPhase.Finished)
            {
                Stop();
            }
        }

        private void OnDisable() => Stop();

        private bool Allows(AbilitySpec next)
            => _transitions != null ? _transitions.Allows(Running, next) : Running == null;

        private void StartCooldown(AbilitySpec spec)
        {
            if (spec != null && spec.Cooldown > 0f && _clock != null)
            {
                _readyAt[spec] = _clock.Now + spec.Cooldown;
            }
        }
    }
}
