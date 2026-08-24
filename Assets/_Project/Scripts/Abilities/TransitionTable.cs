using System;
using System.Collections.Generic;
using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>
    /// 무엇을 무엇으로 끊을 수 있는지의 표.
    /// <para>
    /// 이 판단을 행동 안에 두지 않는 이유는 조합이 폭발하기 때문이다. 행동이 스무 개면
    /// "지금 내가 무엇이고 무엇이 들어왔는가"의 경우가 사백 가지이고, 그것이 각 행동에
    /// 흩어져 있으면 새 행동을 하나 더할 때마다 기존 스무 개를 모두 열어봐야 한다.
    /// </para>
    /// <para>
    /// 행동은 "지금 끊을 수 있는 구간인가"만 답하고, 무엇으로 끊을 수 있는지는 여기가
    /// 답한다. 규칙은 이름이 아니라 꼬리표로 쓰므로, 새 행동은 맞는 꼬리표를 달기만
    /// 하면 표를 고치지 않아도 자리를 찾는다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Transitions", menuName = "Adler/Abilities/Transition Table")]
    public sealed class TransitionTable : ScriptableObject
    {
        /// <summary>규칙 한 줄.</summary>
        [Serializable]
        public struct Rule
        {
            [Tooltip("돌고 있는 행동이 이 꼬리표 중 하나라도 가지면 이 줄을 본다.\n" +
                     "None으로 두면 무엇이 돌고 있든 본다.")]
            public AbilityTag From;

            [Tooltip("들어오려는 행동이 가져야 하는 꼬리표.\n" +
                     "None으로 두면 무엇이든 들어올 수 있다.")]
            public AbilityTag To;

            [Tooltip("끊을 수 있는 구간이 아니어도 끊는다.\n\n" +
                     "쓰는 데가 있다 — 격추당하거나 얼어붙는 것처럼 행동의 사정과\n" +
                     "무관하게 끊겨야 하는 것들이다.")]
            public bool Force;
        }

        [Tooltip("위에서부터 본다. 하나라도 맞으면 끊을 수 있다.")]
        [SerializeField] private List<Rule> _rules = new();

        /// <summary>
        /// 돌고 있는 행동을 새 행동으로 끊을 수 있는지.
        /// <para>
        /// 돌고 있는 것이 없으면 언제나 시작할 수 있다. 표는 <b>끊는 일</b>에 대한
        /// 것이지 <b>시작하는 일</b>에 대한 것이 아니다.
        /// </para>
        /// </summary>
        public bool Allows(Ability running, AbilitySpec next)
        {
            if (running == null || !running.IsRunning)
            {
                return true;
            }

            if (next == null)
            {
                return false;
            }

            foreach (Rule rule in _rules)
            {
                if (!Matches(rule.From, running.Spec) || !Matches(rule.To, next))
                {
                    continue;
                }

                if (rule.Force || running.IsCancelable)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>꼬리표를 비워두면 아무거나 맞는 것으로 본다.</summary>
        private static bool Matches(AbilityTag required, AbilitySpec spec)
            => required == AbilityTag.None || (spec != null && spec.Has(required));
    }
}
