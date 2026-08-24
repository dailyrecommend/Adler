using Adler.Abilities;
using Adler.Core;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 커맨드를 입력해 요청하는 것들의 공통 기반. 폭탄도 재보급도 여기서 갈라져 나온다.
    /// <para>
    /// 승인 절차가 같기 때문에 하나로 묶었다. 무엇을 요청하든 방향키를 맞게 눌러야 하고,
    /// 그 몇 초 동안 손이 조종에서 떠난다는 대가도 같다. 새 스트라타젬을 만들 때
    /// 커맨드 판정을 다시 짤 일은 없어야 한다.
    /// </para>
    /// <para>
    /// 행동이기도 하다. 승인된다는 것은 곧 무언가를 시작한다는 뜻이라, 실행은 행동 쪽에
    /// 맡기고 여기는 <b>어떻게 부르는가</b>만 더한다. 그래서 스트라타젬을 늘리는 일이
    /// 컴포넌트를 늘리는 일이 되지 않는다 — 승인 신호를 듣고 자기 것인지 확인하는
    /// 감시자를 하나씩 붙이면, 스무 개가 되었을 때 기체에 감시자만 스무 개가 붙는다.
    /// </para>
    /// </summary>
    public abstract class StratagemDefinition : AbilitySpec
    {
        [Header("스트라타젬")]
        [Tooltip("화면에 띄울 아이콘.")]
        public Sprite Icon;

        [Tooltip("이 순서대로 방향키를 눌러야 승인된다. 길수록 강한 요청에 어울린다.")]
        public CommandDirection[] Command =
        {
            CommandDirection.Up,
            CommandDirection.Down,
            CommandDirection.Left,
            CommandDirection.Right,
        };

        [Tooltip("출격 한 번에 부를 수 있는 횟수. 0 이하면 무제한.\n\n" +
                 "쿨타임과 다르다. 쿨타임은 기다리면 풀리지만 이것은 출격이 끝나야\n" +
                 "돌아오므로, 아껴 쓸지 지금 쓸지를 판단하게 만든다.")]
        public int UsesPerSortie;
    }
}
