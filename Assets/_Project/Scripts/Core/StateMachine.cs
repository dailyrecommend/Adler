using System;

namespace Adler.Core
{
    /// <summary>
    /// 상태 하나와 거기 머문 시간을 들고 있는 작은 기계.
    /// <para>
    /// 상태를 private 열거형으로 감춰두면 밖에서는 그 안을 볼 수 없어서, 소리든 화면이든
    /// 상태를 알고 싶은 쪽마다 <c>IsFlying</c> 같은 창을 하나씩 뚫게 된다. 창이 늘수록
    /// 상태가 몇 개인지, 어느 것이 동시에 참일 수 있는지가 흐려진다.
    /// </para>
    /// <para>
    /// 머문 시간을 여기서 세는 이유는, 그러지 않으면 상태마다 남은 시간을 담는 필드가
    /// 따로 생기고 그것을 초기화하는 일을 잊을 수 있기 때문이다. 옮겨가는 순간 0으로
    /// 돌아가는 것이 규칙이면 잊을 자리가 없다.
    /// </para>
    /// <para>
    /// 시간은 밖에서 받는다. 이 기계는 어느 시계를 쓸지 정하지 않으므로, 늦춰진 기체에
    /// 얹으면 그 기체의 상태도 함께 늦게 흐른다.
    /// </para>
    /// </summary>
    public sealed class StateMachine<TState> where TState : struct, Enum
    {
        /// <summary>지금 상태.</summary>
        public TState Current { get; private set; }

        /// <summary>이 상태로 옮겨온 뒤 흐른 시간(초).</summary>
        public float Elapsed { get; private set; }

        /// <summary>상태가 바뀔 때. (떠난 상태, 들어선 상태)</summary>
        public event Action<TState, TState> Changed;

        public StateMachine(TState initial) => Current = initial;

        public bool Is(TState state) => Current.Equals(state);

        /// <summary>
        /// 상태를 옮긴다. 같은 상태로 옮기라는 요청은 무시한다 —
        /// 머문 시간이 되감기면 상태에 걸린 연출이 끝없이 처음부터 다시 시작한다.
        /// </summary>
        public bool Set(TState next)
        {
            if (Current.Equals(next))
            {
                return false;
            }

            TState previous = Current;

            Current = next;
            Elapsed = 0f;

            Changed?.Invoke(previous, next);
            return true;
        }

        /// <summary>
        /// 상태를 옮기되, 머문 시간은 되감는다.
        /// 같은 상태를 새로 시작하는 것이 뜻이 있는 경우에만 쓴다.
        /// </summary>
        public void Restart(TState next)
        {
            if (!Set(next))
            {
                Elapsed = 0f;
            }
        }

        /// <summary>흐른 시간만큼 머문 시간을 민다. 상태를 쓰는 쪽이 매 스텝 부른다.</summary>
        public void Advance(float delta) => Elapsed += delta;
    }
}
