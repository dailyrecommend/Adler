using System;
using Adler.Aircraft;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 부스터 연료. 쓰는 동안 줄고 놓으면 차오른다.
    /// <para>
    /// 대가가 없으면 부스터를 끌 이유가 없고, 그러면 최고 속도라는 값이 의미를 잃는다.
    /// 연료가 붙으면 언제 쓸지가 판단이 된다 — 대공포 사이를 빠져나갈 한 번을 남겨둘지,
    /// 지금 표적에 빨리 붙을지.
    /// </para>
    /// <para>
    /// 소모와 회복을 한 곳에서 처리한다. 회복을 따로 돌리면 조종 쪽과 실행 순서에 따라
    /// 같은 프레임에 썼다 채웠다 하게 되고, 남은 양이 프레임마다 달라진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoostFuel : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("연료 수치를 읽어올 기체. 비워두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private AircraftController _aircraft;

        private float _remaining;
        private float _rechargeAt;
        private bool _lockedOut;

        /// <summary>남은 연료가 바뀔 때마다. 화면 표시가 구독한다.</summary>
        public event Action<BoostFuel> Changed;

        /// <summary>연료가 바닥나 부스터가 끊겼을 때.</summary>
        public event Action<BoostFuel> Depleted;

        /// <summary>바닥났다가 다시 쓸 수 있게 됐을 때.</summary>
        public event Action<BoostFuel> Restored;

        public float Remaining => _remaining;

        public float Capacity { get; private set; }

        public float Normalized => Capacity > 0f ? _remaining / Capacity : 0f;

        /// <summary>바닥나서 잠긴 상태인지. 일정 비율까지 차야 풀린다.</summary>
        public bool IsLockedOut => _lockedOut;

        private void Awake()
        {
            if (_aircraft == null)
            {
                _aircraft = GetComponent<AircraftController>();
            }

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(BoostFuel)}: 기체를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            Capacity = Stats != null ? Stats.BoostCapacity : 0f;
            _remaining = Capacity;
            Changed?.Invoke(this);
        }

        private AircraftStatSheet Stats => _aircraft.Stats;

        /// <summary>
        /// 이번 스텝에 부스터를 쓸 수 있는지 묻고, 쓴 만큼 깎는다.
        /// 조종 쪽이 매 물리 스텝 한 번만 부른다.
        /// </summary>
        public bool RequestBoost(bool wanted, float deltaTime)
        {
            AircraftStatSheet stats = Stats;
            if (stats == null)
            {
                return wanted;
            }

            // 정비로 용량이 바뀔 수 있으므로 매번 확인한다.
            Capacity = stats.BoostCapacity;

            bool allowed = wanted && !_lockedOut && _remaining > 0f;

            if (allowed)
            {
                Spend(stats.BoostDrain * deltaTime);
            }
            else
            {
                Recover(stats, deltaTime, wanted);
            }

            return allowed;
        }

        private void Spend(float amount)
        {
            _remaining = Mathf.Max(0f, _remaining - amount);
            _rechargeAt = Time.time + _aircraft.Airframe.BoostRechargeDelay;

            Changed?.Invoke(this);

            if (_remaining > 0f)
            {
                return;
            }

            _lockedOut = true;
            Depleted?.Invoke(this);
        }

        private void Recover(AircraftStatSheet stats, float deltaTime, bool stillHeld)
        {
            // 누르고 있는 동안에는 차지 않는다. 바닥난 채로 계속 누르면
            // 조금 차자마자 다시 빨려 들어가 덜덜거린다.
            if (stillHeld || _remaining >= Capacity || Time.time < _rechargeAt)
            {
                return;
            }

            _remaining = Mathf.Min(Capacity, _remaining + (stats.BoostRecharge * deltaTime));
            Changed?.Invoke(this);

            if (!_lockedOut)
            {
                return;
            }

            float threshold = Capacity * _aircraft.Airframe.BoostReengageFraction;
            if (_remaining >= threshold)
            {
                _lockedOut = false;
                Restored?.Invoke(this);
            }
        }

        /// <summary>리스폰이나 재보급 때 가득 채운다.</summary>
        public void Refill()
        {
            _remaining = Capacity;
            _rechargeAt = 0f;

            if (_lockedOut)
            {
                _lockedOut = false;
                Restored?.Invoke(this);
            }

            Changed?.Invoke(this);
        }
    }
}
