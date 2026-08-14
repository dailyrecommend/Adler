namespace Adler.Flight
{
    /// <summary>
    /// 한 프레임 분량의 조종 입력. 입력 장치와 비행 모델 사이를 잇는 유일한 통로다.
    /// 덕분에 비행 모델은 키보드/패드/AI 중 무엇이 값을 채웠는지 알 필요가 없고,
    /// AI 편대기도 같은 구조체를 채워 같은 모델을 그대로 쓸 수 있다.
    /// </summary>
    public struct FlightInput
    {
        /// <summary>+1 = 기수를 올림(당김), -1 = 기수를 내림(밈).</summary>
        public float Pitch;

        /// <summary>+1 = 오른쪽으로 롤, -1 = 왼쪽으로 롤.</summary>
        public float Roll;

        /// <summary>+1 = 기수를 오른쪽으로, -1 = 왼쪽으로.</summary>
        public float Yaw;

        /// <summary>+1 = 가속, -1 = 감속, 0 = 현재 속도 유지.</summary>
        public float Throttle;

        /// <summary>부스터 사용 여부.</summary>
        public bool Boost;

        public static FlightInput None => default;
    }
}
