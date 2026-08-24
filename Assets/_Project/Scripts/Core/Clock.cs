using System.Collections.Generic;
using UnityEngine;

namespace Adler.Core
{
    /// <summary>
    /// 시간을 소비하는 주체 하나가 자기 것으로 갖는 시계.
    /// <para>
    /// 시간을 <see cref="Time.deltaTime"/>에서 직접 읽지 않고 시계에서 받는 이유는,
    /// 그래야 "누구의 시간인가"를 물을 수 있기 때문이다. 엔진의 시간은 하나뿐이라
    /// 그것을 늦추면 세상 전부가 늦춰진다 — 적만 늦추거나 플레이어만 빠르게 하는 것이
    /// 규칙이 되는 순간, 시간이 전역이라는 사실 자체가 벽이 된다.
    /// </para>
    /// <para>
    /// 시계는 겹쳐 걸린다. 자기 배율에 부모의 배율이 곱해지므로, 세상이 절반으로
    /// 느려진 와중에 어떤 기체만 두 배로 빠르면 그 기체는 원래 속도로 움직인다.
    /// </para>
    /// </summary>
    public sealed class Clock
    {
        private readonly List<Clock> _children = new();
        private readonly Clock _parent;

        private float _holdRemaining;
        private float _holdScale = 1f;

        /// <summary>
        /// 모든 시계의 뿌리. 아무 데도 매달리지 않은 것들이 쓰는 시간이다.
        /// </summary>
        public static Clock World { get; } = new(null);

        public Clock(Clock parent)
        {
            _parent = parent;
            _parent?._children.Add(this);
        }

        /// <summary>
        /// 세상 배율이 내려갈 수 있는 바닥. 나누는 쪽이라 0이 되면 무한대가 나온다.
        /// <para>
        /// 매달린 시계에는 걸지 않는다. 그쪽은 나누어지는 쪽이라 0이 아무 문제가 없고,
        /// 오히려 0이라야 <b>완전히</b> 멈춘다.
        /// </para>
        /// </summary>
        public const float MinScale = 0.0001f;

        /// <summary>
        /// 이 시계에 걸어둔 배율. 디버프처럼 얼마간 이어지는 것이 쓴다.
        /// 1이면 부모와 같은 속도로 흐른다. 0이면 이 아래의 시간이 멎는다.
        /// </summary>
        public float LocalScale { get; set; } = 1f;

        /// <summary>부모까지 거슬러 올라가 곱해진 실제 배율.</summary>
        public float Scale => LocalScale * _holdScale * (_parent?.Scale ?? 1f);

        /// <summary>이번 프레임에 이 시계가 흐른 양(초).</summary>
        public float Delta { get; private set; }

        /// <summary>
        /// 이 시계가 켜진 뒤 흐른 총량(초).
        /// <para>
        /// <see cref="Time.time"/> 대신 이것으로 기한을 찍어야 한다. 엔진의 시간은
        /// 모두에게 같은 속도로 흐르므로, 자기만 늦춰진 주체가 그것으로 쿨다운을 재면
        /// 남들 시간에 맞춰 준비를 마친다.
        /// </para>
        /// </summary>
        public float Now { get; private set; }

        /// <summary>
        /// 세상 시계에 견준 이 시계의 배율.
        /// <para>
        /// 물리에 얹을 때 쓴다. 엔진의 물리 시간은 이미 세상 배율만큼 늦춰져 있으므로,
        /// 거기에 이 시계의 배율을 그대로 곱하면 세상 몫이 두 번 들어간다. 세상 몫을
        /// 덜어낸 나머지가 "남들보다 얼마나 느린가"이고, 그것만 물리에 곱해야 한다.
        /// </para>
        /// <para>
        /// 세상은 그대로 두고 이 시계만 늦추면 이 값이 1보다 작아지고, 그만큼 덜 움직인다.
        /// 0이면 아예 서고, 그때도 나누는 쪽은 세상 배율이라 0으로 나눌 일이 없다.
        /// </para>
        /// </summary>
        public float Relative => Scale / Mathf.Max(World.Scale, MinScale);

        /// <summary>
        /// 물리 스텝 하나가 이 시계에서 흐른 양(초).
        /// <para>
        /// <see cref="Delta"/>는 화면 갱신에서 밀리므로 물리 스텝에서 쓰면 어긋난다.
        /// 물리 쪽은 이것을 써야 한다.
        /// </para>
        /// </summary>
        public float FixedDelta => Time.fixedDeltaTime * Relative;

        /// <summary>
        /// 잠깐 배율을 눌러둔다. 히트스톱처럼 짧게 시간을 늦추는 것들이 부른다.
        /// <para>
        /// 더 긴 요청이 오면 갈아탄다. 짧은 것이 긴 것을 끊으면 큰 한 방이 잔챙이에게
        /// 밀려 사라진다.
        /// </para>
        /// <para>
        /// 남은 시간은 이 시계가 아니라 <b>바깥 시간</b>으로 잰다. 늦추는 그 배율로
        /// 재면 늦출수록 오래 걸려서, 배율을 0에 가깝게 둘수록 영영 풀리지 않는다.
        /// </para>
        /// </summary>
        public void Hold(float seconds, float scale)
        {
            if (seconds <= 0f || seconds < _holdRemaining)
            {
                return;
            }

            _holdRemaining = seconds;
            _holdScale = Mathf.Clamp(scale, 0.01f, 1f);
        }

        /// <summary>눌러둔 것을 즉시 놓는다.</summary>
        public void Release()
        {
            _holdRemaining = 0f;
            _holdScale = 1f;
        }

        /// <summary>
        /// 바깥 시간이 흐른 만큼 이 시계와 매달린 시계들을 밀어준다.
        /// <para>
        /// 부모가 먼저 흐른 뒤 자식이 흐른다. 반대로 하면 자식이 부모의 지난 프레임
        /// 배율을 쓰게 되어, 배율이 바뀌는 순간마다 한 프레임씩 어긋난다.
        /// </para>
        /// </summary>
        public void Advance(float unscaledDelta)
        {
            if (_holdRemaining > 0f)
            {
                _holdRemaining -= unscaledDelta;

                if (_holdRemaining <= 0f)
                {
                    Release();
                }
            }

            Delta = unscaledDelta * Scale;
            Now += Delta;

            foreach (Clock child in _children)
            {
                child.Advance(unscaledDelta);
            }
        }
    }
}
