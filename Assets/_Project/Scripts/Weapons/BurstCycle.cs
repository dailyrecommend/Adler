namespace Adler.Weapons
{
    /// <summary>
    /// 쏘는 시간과 쉬는 시간이 번갈아 도는 점사 리듬.
    /// <para>
    /// 언제 시작할지는 여기서 정하지 않는다. 적기는 뜸이 차야 쏘고 대공포는 겨눠지면
    /// 쏘는데, 그 판단까지 들이면 이 리듬이 무기마다의 사정을 알게 된다. 여기는
    /// 시작된 점사가 언제 끝나고 언제 다시 시작할 수 있는지만 센다.
    /// </para>
    /// </summary>
    public sealed class BurstCycle
    {
        private readonly float _burstSeconds;
        private readonly float _restSeconds;

        private float _timer;

        public BurstCycle(float burstSeconds, float restSeconds)
        {
            _burstSeconds = burstSeconds;
            _restSeconds = restSeconds;
        }

        /// <summary>지금 쏘는 구간인지.</summary>
        public bool IsFiring { get; private set; }

        /// <summary>쉬는 시간이 다 지나 다음 점사를 시작할 수 있는지.</summary>
        public bool RestDone => !IsFiring && _timer <= 0f;

        /// <summary>
        /// 시간을 흘려보낸다. 쏘는 구간이 방금 끝났으면 거짓을 돌려주고 쉬기 시작한다.
        /// </summary>
        public bool Tick(float delta)
        {
            _timer -= delta;

            if (IsFiring && _timer <= 0f)
            {
                IsFiring = false;
                _timer = _restSeconds;
                return false;
            }

            return IsFiring;
        }

        /// <summary>점사를 시작한다.</summary>
        public void Begin()
        {
            IsFiring = true;
            _timer = _burstSeconds;
        }

        /// <summary>쏘던 것을 끊고 쉬는 구간으로 보낸다. 표적을 잃거나 꺼질 때 부른다.</summary>
        public void Interrupt()
        {
            IsFiring = false;
            _timer = _restSeconds;
        }
    }
}
