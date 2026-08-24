using System;
using System.Collections.Generic;
using Adler.Core;
using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 이 기체가 할 수 있는 행동들을 들고 굴린다.
    /// <para>
    /// 여럿이 동시에 돈다. 기총을 쏘면서 조명탄을 뿌리고 그동안 수리가 도는 것은
    /// 겨루는 일이 아니라 각자 다른 통로를 쓰는 일이라, 한 번에 하나만 돌게 묶으면
    /// "무엇이 무엇을 끊는가"라는 질문이 억지로 생긴다. 손이 하나뿐인 격투 게임이라면
    /// 그 질문이 규칙이 되지만, 여기서는 답할 필요가 없는 질문이다.
    /// </para>
    /// <para>
    /// 행동의 종류를 모른다. 무엇을 만들지는 <see cref="AbilitySpec"/>이 답하므로,
    /// 행동이 스무 개로 늘어도 이 파일은 그대로다.
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

        private readonly Dictionary<AbilitySpec, Ability> _abilities = new();

        // 쿨타임과 출격 횟수의 주인은 여기 하나다. 요청 창구가 따로 세게 두면 세는
        // 시점이 어긋나는 순간 "다 찼다고 보이는데 안 써지는" 구간이 생긴다 — 실제로
        // 그랬다. 화면이든 커맨드든 남들은 전부 여기에 물어본다.
        private readonly Dictionary<AbilitySpec, float> _readyAt = new();
        private readonly Dictionary<AbilitySpec, int> _used = new();

        private readonly List<Ability> _running = new();

        // 돌면서 지우면 순회가 깨지므로 먼저 모아둔다.
        private readonly List<Ability> _finished = new();

        private Clock _clock;

        /// <summary>행동이 시작될 때. 연출이 구독한다.</summary>
        public event Action<Ability> Started;

        /// <summary>행동이 끝날 때. 끊겨서 끝난 경우도 포함한다.</summary>
        public event Action<Ability> Ended;

        /// <summary>
        /// 행동이 세상에 닿는 통로. 기체가 자기 것을 끼워 넣는다.
        /// <para>
        /// 실행기가 스스로 찾지 않는다. 찾게 두면 이 파일이 기체의 부품 이름을 알아야
        /// 하고, 적기에 같은 실행기를 쓸 때 그 이름들이 맞지 않는다.
        /// </para>
        /// </summary>
        public AbilityContext Context { get; set; }

        /// <summary>이 행동이 지금 돌고 있는지.</summary>
        public bool IsRunning(AbilitySpec spec)
            => spec != null && _abilities.TryGetValue(spec, out Ability ability) && ability.IsRunning;

        /// <summary>
        /// 이 꼬리표를 단 행동이 지금 돌고 있는지.
        /// <para>
        /// 이름이 아니라 꼬리표로 묻는다. 화면 효과가 "수리 중인가"를 알고 싶을 때
        /// 특정 스트라타젬의 이름을 알아야 한다면, 수리하는 스트라타젬을 하나 더 만드는
        /// 순간 그 조건이 조용히 어긋난다.
        /// </para>
        /// </summary>
        public bool IsRunning(AbilityTag tag)
        {
            foreach (Ability ability in _running)
            {
                if (ability.Spec != null && ability.Spec.Has(tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>남은 쿨타임(초). 쓸 수 있으면 0.</summary>
        public float RemainingCooldown(AbilitySpec spec)
        {
            if (spec == null || _clock == null || !_readyAt.TryGetValue(spec, out float readyAt))
            {
                return 0f;
            }

            return Mathf.Max(0f, readyAt - _clock.Now);
        }

        /// <summary>쿨타임 진행도. 1이면 방금 썼고 0이면 다 찼다. 게이지에 그대로 넣는다.</summary>
        public float CooldownNormalized(AbilitySpec spec)
        {
            if (spec == null || spec.Cooldown <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(RemainingCooldown(spec) / spec.Cooldown);
        }

        /// <summary>출격 횟수 제한까지 다 써버렸는지.</summary>
        public bool IsExhausted(AbilitySpec spec)
        {
            if (spec == null || spec.UsesPerSortie <= 0)
            {
                return false;
            }

            return _used.TryGetValue(spec, out int used) && used >= spec.UsesPerSortie;
        }

        /// <summary>
        /// 쿨타임이나 횟수 제한에 걸려 있는지.
        /// <para>
        /// <see cref="CanUse"/>와 다른 질문이다. 저쪽은 행동 스스로의 조건까지 보므로
        /// "가득 찬 기체의 수리"도 못 쓴다고 답하는데, 커맨드 화면이 그 답을 쓰면
        /// 멀쩡한 커맨드가 입력 자체가 안 되는 것처럼 보인다. 제한은 제한만 묻는다.
        /// </para>
        /// </summary>
        public bool IsRestricted(AbilitySpec spec)
            => RemainingCooldown(spec) > 0f || IsExhausted(spec);

        /// <summary>
        /// 출격을 다시 시작할 때 제한을 되돌린다. 쿨타임도 횟수도 새 출격에는 새것이다.
        /// </summary>
        public void ResetSortie()
        {
            _readyAt.Clear();
            _used.Clear();
        }

        /// <summary>이 행동을 지금 시작할 수 있는지. 제한과 스스로의 조건을 함께 본다.</summary>
        public bool CanUse(AbilitySpec spec)
        {
            if (spec == null || !_abilities.TryGetValue(spec, out Ability ability) || ability.IsRunning)
            {
                return false;
            }

            if (IsRestricted(spec))
            {
                return false;
            }

            return ability.CanActivate(Context);
        }

        /// <summary>
        /// 행동을 시작한다. 못 하면 아무 일도 일어나지 않는다.
        /// <para>
        /// 거절을 예외로 알리지 않는다. 쿨타임이 남았거나 이미 돌고 있는 상황은 잘못이
        /// 아니라 흔한 일이고, 부르는 쪽은 대개 매 프레임 눌러보는 입력이라 그때마다
        /// 판단을 되풀이할 이유가 없다.
        /// </para>
        /// </summary>
        public bool TryUse(AbilitySpec spec)
        {
            if (!CanUse(spec))
            {
                return false;
            }

            Ability ability = _abilities[spec];

            ability.Begin(Context);
            _running.Add(ability);

            // 쿨타임은 끝날 때가 아니라 쓰는 순간부터 흐른다.
            //
            // 끝날 때부터 재면 지속형이 곤란해진다. 화면은 부른 순간부터 세는데 실제로
            // 열리는 것은 효과가 끝나고 나서라, 다 찼다고 보이는데 안 써지는 구간이
            // 효과 지속시간만큼 생긴다. 그 구간에서 플레이어는 입력이 씹혔다고 읽는다.
            if (spec.Cooldown > 0f && _clock != null)
            {
                _readyAt[spec] = _clock.Now + spec.Cooldown;
            }

            // 횟수도 시작이 확정된 여기서만 센다. 요청 창구에서 세면 거절당한 요청이
            // 한 번을 깎아 먹는다 — 실제로 그랬다.
            if (spec.UsesPerSortie > 0)
            {
                _used[spec] = (_used.TryGetValue(spec, out int used) ? used : 0) + 1;
            }

            Started?.Invoke(ability);
            return true;
        }

        /// <summary>이 행동을 끊는다. 쿨타임은 여기서 시작된다.</summary>
        public void Stop(AbilitySpec spec)
        {
            if (spec != null && _abilities.TryGetValue(spec, out Ability ability))
            {
                End(ability);
            }
        }

        /// <summary>돌고 있는 것을 모두 끊는다. 격추되거나 되살아날 때 부른다.</summary>
        public void Stop()
        {
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                End(_running[i]);
            }
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

                // 손을 떼야 끝나는데 잡고 있는 사람이 없는 조합. 커맨드로 부르는 것은
                // 아무도 놓아주지 않으므로 한 번 시작하면 영영 돈다. 증상이 "상태가
                // 안 풀린다"라 원인을 실행기나 행동에서 찾게 되는데, 정작 틀린 것은
                // 자산의 꼬리표 한 칸이다.
                if (spec.Has(AbilityTag.Stratagem) && spec.Has(AbilityTag.Sustained))
                {
                    Debug.LogError(
                        $"{nameof(AbilityRunner)}: '{spec.DisplayName}'이 Stratagem이면서 Sustained입니다. " +
                        "커맨드로 부르는 것은 놓아줄 손이 없어 끝나지 않습니다 — " +
                        "Sustained를 끄고 Active Seconds로 길이를 정하세요.", this);
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
            _finished.Clear();

            foreach (Ability ability in _running)
            {
                ability.Tick(Context);

                // 스스로 끝난 것을 모아둔다. 끝났는데 남아 있으면 다시 시작할 수 없다.
                if (ability.Phase == AbilityPhase.Finished)
                {
                    _finished.Add(ability);
                }
            }

            foreach (Ability ability in _finished)
            {
                End(ability);
            }
        }

        private void OnDisable() => Stop();

        private void End(Ability ability)
        {
            if (!_running.Remove(ability))
            {
                return;
            }

            ability.End(Context);

            Ended?.Invoke(ability);
        }
    }
}
