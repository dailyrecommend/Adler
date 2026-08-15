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

        /// <summary>현재 스로틀 설정값. 0 = 최저 속도, 1 = 최고 속도.</summary>
        float ThrottleNormalized { get; }

        /// <summary>부스터를 쓰는 중인지. 화면 표시와 효과음이 참조한다.</summary>
        bool IsBoosting { get; }

        /// <summary>
        /// 물리 시뮬레이션 시작 전에 한 번 호출. 구현체가 Rigidbody 설정을
        /// 자신에게 맞게 강제하는 자리이기도 하다.
        /// </summary>
        void Initialize(Rigidbody body);

        /// <summary>FixedUpdate마다 호출. 구현체가 Rigidbody를 갱신한다.</summary>
        void Tick(in FlightInput input, float deltaTime);
    }
}
