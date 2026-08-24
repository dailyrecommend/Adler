using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// "입력을 받아 기체를 움직인다"는 책임만 갖는 경계면.
    /// <para>
    /// 이 인터페이스가 존재하는 이유는 나중에 아케이드 → 물리 기반으로 갈아탈 수 있게
    /// 하기 위해서다. <see cref="AircraftController"/>를 비롯한 나머지 코드는
    /// 구현체가 회전을 직접 지정하는지, 날개마다 양력을 계산하는지 알지 못한다.
    /// </para>
    /// </summary>
    public interface IFlightModel
    {
        /// <summary>기체 속도(m/s). HUD 표시와 카메라 연출이 참조한다.</summary>
        float Speed { get; }

        /// <summary>최저에서 부스터 속도 사이의 어디쯤인지. 0 = 최저, 1 = 부스터.</summary>
        float SpeedNormalized { get; }

        /// <summary>부스터를 쓰는 중인지. 화면 표시와 효과음이 참조한다.</summary>
        bool IsBoosting { get; }

        /// <summary>조종과 추력이 끊긴 상태인지.</summary>
        bool IsFrozen { get; }

        /// <summary>
        /// 이번 스텝에 걸린 견인. 그래플 같은 것이 매 물리 스텝 넣는다.
        /// <para>
        /// 넣어준 스텝에만 듣고 곧바로 비워진다. 줄을 놓은 뒤에도 힘이 남아
        /// 한동안 끌려가는 일이 없다.
        /// </para>
        /// </summary>
        void SetTether(in Tether tether);

        /// <summary>
        /// 조종과 추력을 끊거나 되돌린다.
        /// <para>
        /// 끊긴 동안 기체는 물리에 맡겨진다. 입력도 스로틀도 받지 않고 중력에 떨어지며,
        /// 되돌리면 그때의 속도와 자세를 이어받아 다시 날기 시작한다.
        /// </para>
        /// </summary>
        void SetFrozen(bool frozen);

        /// <summary>
        /// 물리 시뮬레이션 시작 전에 한 번 호출. 구현체가 Rigidbody 설정을
        /// 자신에게 맞게 강제하는 자리이기도 하다.
        /// </summary>
        void Initialize(Rigidbody body);

        /// <summary>
        /// FixedUpdate마다 호출. 구현체가 Rigidbody를 갱신한다.
        /// <para>
        /// 시간을 숫자가 아니라 시계로 받는다. 이 기체만 느리게 하려면 흐른 양을 줄이는
        /// 것만으로는 모자라고 — 그러면 붙는 속도만 굼떠지고 순항 속도는 그대로다 —
        /// 내놓는 속도 자체에도 같은 배율을 곱해야 한다. 둘은 언제나 짝으로 다녀야
        /// 하므로 하나로 묶어 넘긴다.
        /// </para>
        /// </summary>
        void Tick(in FlightInput input, Clock clock);
    }
}
