using System;

namespace Adler.Weapons
{
    /// <summary>
    /// 발사 간격을 지키며 밀린 발수를 따라잡는 셈.
    /// <para>
    /// 프레임이 발사 간격보다 길면 한 프레임에 여러 발을 몰아 쏴야 리듬이 맞는데,
    /// 한계 없이 따라잡으면 렉이 튄 프레임에 수십 발이 쏟아진다. 몇 발까지만
    /// 따라잡고 나머지 빚은 버린다.
    /// </para>
    /// </summary>
    public sealed class ShotCadence
    {
        /// <summary>한 프레임에 몰아 쏠 수 있는 최대 발수.</summary>
        private const int MaxShotsPerFrame = 3;

        private float _cooldown;

        /// <summary>
        /// 다음 발까지 아직 기다리는 중인지. 점사가 끊겨도 이게 참이면 마지막 발의
        /// 간격이 남아 있다는 뜻이다.
        /// </summary>
        public bool CoolingDown => _cooldown > 0f;

        /// <summary>시간을 흘려보내고, 간격이 찰 때마다 한 발씩 내보낸다.</summary>
        public void Run(float delta, float interval, Action fireOne)
        {
            _cooldown -= delta;

            int shots = 0;

            while (_cooldown <= 0f && shots < MaxShotsPerFrame)
            {
                fireOne();

                _cooldown += interval;
                shots++;
            }

            // 못 갚은 빚은 버린다. 남겨두면 다음에 쏠 수 있게 된 순간
            // 밀린 몫이 한꺼번에 쏟아진다.
            if (_cooldown < 0f)
            {
                _cooldown = 0f;
            }
        }

        public void Reset() => _cooldown = 0f;
    }
}
