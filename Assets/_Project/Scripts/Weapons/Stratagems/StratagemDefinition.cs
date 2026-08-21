using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>승인 커맨드를 이루는 방향 입력 하나.</summary>
    public enum CommandDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>
    /// 커맨드를 입력해 요청하는 것들의 공통 기반. 폭탄도 재보급도 여기서 갈라져 나온다.
    /// <para>
    /// 승인 절차가 같기 때문에 하나로 묶었다. 무엇을 요청하든 방향키를 맞게 눌러야 하고,
    /// 그 몇 초 동안 손이 조종에서 떠난다는 대가도 같다. 새 스트라타젬을 만들 때
    /// 커맨드 판정을 다시 짤 일은 없어야 한다.
    /// </para>
    /// </summary>
    public abstract class StratagemDefinition : ScriptableObject
    {
        [Header("표시")]
        public string DisplayName = "Unnamed";

        [Tooltip("화면에 띄울 아이콘.")]
        public Sprite Icon;

        [Header("승인 커맨드")]
        [Tooltip("이 순서대로 방향키를 눌러야 승인된다. 길수록 강한 요청에 어울린다.")]
        public CommandDirection[] Command =
        {
            CommandDirection.Up,
            CommandDirection.Down,
            CommandDirection.Left,
            CommandDirection.Right,
        };

        [Header("제한")]
        [Tooltip("승인된 뒤 다시 부를 수 있을 때까지의 시간(초). 0이면 제한 없다.")]
        [Min(0f)]
        public float Cooldown = 20f;

        [Tooltip("출격 한 번에 부를 수 있는 횟수. 0 이하면 무제한.")]
        public int UsesPerSortie;
    }
}
