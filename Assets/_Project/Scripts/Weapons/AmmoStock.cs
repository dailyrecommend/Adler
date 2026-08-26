using System;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 쏘면 줄고 시간이 지나면 한 발씩 돌아오는 탄.
    /// <para>
    /// 실어 나르는 탄약이 아니다. 바닥나도 재보급을 기다릴 필요 없이 잠깐 쉬면 돌아오므로,
    /// 판단거리는 "아껴서 끝까지 버티기"가 아니라 "지금 쏟아붓고 잠깐 비울 것인가"가 된다.
    /// 한정된 탄약은 아낀 사람이 이기지만, 차오르는 탄은 리듬을 읽은 사람이 이긴다.
    /// </para>
    /// <para>
    /// 개수로 센다. 게이지로 두면 "지금 두 발 남았다"를 셀 수 없어지는데, 미사일처럼
    /// 한 발이 곧 한 판단인 무기에서는 그 셈이 곧 전술이다. 다음 한 발이 얼마나 찼는지는
    /// <see cref="Progress"/>로 따로 내준다.
    /// </para>
    /// <para>
    /// 부스터 연료와 닮았지만 같은 물건은 아니다. 저쪽은 흐르듯 줄고 늘어나는 양이고
    /// 이쪽은 통째로 오가는 개수여서, 하나로 묶으면 한쪽은 소수점을 끌고 다니고
    /// 다른 쪽은 반올림에 시달린다.
    /// </para>
    /// <para>
    /// 알림을 내보내지 않는다. 지금 이것을 보는 곳은 눈금 하나뿐이고 그 눈금은 차오르는
    /// 정도까지 봐야 해서 어차피 매 프레임 읽는다. 듣는 쪽이 생기면 그때 달면 된다.
    /// </para>
    /// </summary>
    public sealed class AmmoStock
    {
        private readonly WeaponDefinition _definition;

        private int _remaining;

        // 다음 한 발이 얼마나 찼는지(0~1). 발수와 나눠 두면 쏠 때마다 차오르던 몫을
        // 버리지 않아도 되고, 눈금은 발수 사이를 매끄럽게 채울 수 있다.
        private float _progress;

        // 마지막으로 한 발 쓴 뒤 흐른 시간. 지연을 여기서 잰다.
        private float _idleFor;

        // 바닥나서 잠긴 상태. 재개 발수만큼 차야 풀린다.
        private bool _blocked;

        /// <summary>
        /// 수치는 성능 에셋에서 그때그때 읽는다.
        /// <para>
        /// 생성할 때 베껴두면 인스펙터에서 돌린 값이 다시 켤 때까지 먹지 않는다.
        /// 조율은 대개 굴려보면서 하는 일이라 그 한 박자가 통째로 손해다.
        /// </para>
        /// </summary>
        public AmmoStock(WeaponDefinition definition)
        {
            _definition = definition != null
                ? definition
                : throw new ArgumentNullException(nameof(definition));

            _remaining = Capacity;
        }

        /// <summary>가득 찼을 때의 발수.</summary>
        public int Capacity => Mathf.Max(1, _definition.AmmoCapacity);

        /// <summary>지금 쥐고 있는 발수.</summary>
        public int Remaining => _remaining;

        /// <summary>다음 한 발이 찬 정도(0~1). 가득 차 있으면 0이다.</summary>
        public float Progress => _progress;

        /// <summary>남은 발이 없다.</summary>
        public bool IsEmpty => _remaining <= 0;

        /// <summary>
        /// 바닥나서 잠겨 있다. 남은 발이 없는 것과 다르다 —
        /// 한 발 돌아왔어도 재개 발수에 못 미치면 여전히 잠겨 있다.
        /// </summary>
        public bool IsBlocked => _blocked;

        /// <summary>지금 한 발 쓸 수 있는지.</summary>
        public bool CanSpend => !_blocked && _remaining > 0;

        /// <summary>
        /// 눈금에 넣을 값(0~1). 차오르는 몫까지 함께 센다.
        /// <para>
        /// 발수만 세면 눈금이 뚝뚝 끊겨서, 다음 발이 언제 오는지 화면으로 알 수 없다.
        /// </para>
        /// </summary>
        public float Normalized => Mathf.Clamp01((_remaining + _progress) / Capacity);

        /// <summary>
        /// 한 발 쓴다. 쓸 수 없으면 아무것도 하지 않고 거짓을 준다.
        /// <para>
        /// 묻는 것과 쓰는 것을 하나로 둔다. 갈라두면 물어보고 쓰기까지 사이에 다른
        /// 것이 끼어들 수 있고, 그 틈이 곧 한 발을 두 번 쓰는 길이다.
        /// </para>
        /// </summary>
        public bool TrySpend()
        {
            if (!CanSpend)
            {
                return false;
            }

            _remaining--;

            // 차오르던 몫은 남긴다. 쏠 때마다 버리면 발사 간격이 회복 속도보다 빠른
            // 무기는 영영 한 발도 되찾지 못한다.
            _idleFor = 0f;

            // 재개 발수가 1이면 잠글 것이 없다 — 한 발 돌아오는 순간 쏠 수 있다는 뜻이다.
            _blocked = _remaining <= 0 && ResumeRounds > 1;

            return true;
        }

        /// <summary>
        /// 흐른 만큼 채운다. 쏘지 않는 동안에도 불러야 한다.
        /// <para>
        /// 한 프레임에 여러 발이 돌아올 수 있다. 프레임이 길어졌을 때 한 발만
        /// 돌려주면 화면이 버벅인 사람이 탄까지 손해 본다.
        /// </para>
        /// </summary>
        public void Advance(float delta)
        {
            if (delta <= 0f || _remaining >= Capacity)
            {
                return;
            }

            _idleFor += delta;

            if (_idleFor < Mathf.Max(0f, _definition.RechargeDelay))
            {
                return;
            }

            float perRound = _definition.RechargeSeconds;
            if (perRound <= 0f)
            {
                Gain(Capacity - _remaining);
                return;
            }

            _progress += delta / perRound;

            int gained = Mathf.FloorToInt(_progress);
            if (gained <= 0)
            {
                return;
            }

            _progress -= gained;
            Gain(gained);
        }

        /// <summary>
        /// 발의 일부를 돌려준다. 잘한 것에 값으로 돌려주는 쪽이 부른다.
        /// <para>
        /// <see cref="Restore"/>와 달리 올려주지 않는다. 절반을 주기로 한 쪽이 있는데
        /// 여기서 한 발로 부풀리면, 절반이라는 값이 거짓이 된다.
        /// </para>
        /// <para>
        /// 회복 지연은 건너뛴다. 돌려주는 뜻이 이어서 쓰라는 것인데, 방금 썼다는 이유로
        /// 받은 몫이 지연에 묶여 서 있으면 받은 줄도 모른다.
        /// </para>
        /// </summary>
        public void Hasten(float rounds)
        {
            if (rounds <= 0f || _remaining >= Capacity)
            {
                return;
            }

            _idleFor = Mathf.Max(_idleFor, _definition.RechargeDelay);
            _progress += rounds;

            int gained = Mathf.FloorToInt(_progress);
            if (gained <= 0)
            {
                return;
            }

            _progress -= gained;
            Gain(gained);
        }

        /// <summary>
        /// 용량의 일부를 돌려준다. 재보급이 부른다.
        /// <para>
        /// 올림으로 준다. 내림이면 용량이 작은 무기에 적은 비율을 넣었을 때 아무것도
        /// 들어가지 않고, 받은 쪽에서는 재보급이 먹지 않은 것으로 보인다.
        /// </para>
        /// </summary>
        public void Restore(float percent)
        {
            if (percent > 0f)
            {
                Gain(Mathf.CeilToInt(Capacity * (percent / 100f)));
            }
        }

        /// <summary>가득 채운다. 출격을 다시 시작할 때 부른다.</summary>
        public void Refill()
        {
            _remaining = Capacity;
            _progress = 0f;
            _idleFor = 0f;
            _blocked = false;
        }

        /// <summary>바닥난 뒤 다시 쏘려면 차 있어야 하는 발수. 1이면 잠기지 않는다.</summary>
        private int ResumeRounds => Mathf.Clamp(_definition.ResumeRounds, 1, Capacity);

        /// <summary>
        /// 실제로 늘려주는 유일한 자리. 잠금이 풀리는 것도 여기서만 일어난다.
        /// <para>
        /// 회복과 재보급이 각자 풀면 한쪽만 고치는 일이 생기고, 그때 증상은
        /// "재보급을 받았는데도 안 쏴진다"라 원인에서 멀찍이 떨어져 나타난다.
        /// </para>
        /// </summary>
        private void Gain(int rounds)
        {
            if (rounds <= 0)
            {
                return;
            }

            _remaining = Mathf.Min(Capacity, _remaining + rounds);

            if (_remaining >= Capacity)
            {
                _progress = 0f;
            }

            if (_blocked && _remaining >= ResumeRounds)
            {
                _blocked = false;
            }
        }
    }
}
