using System;
using Adler.Combat;
using Adler.Weapons;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 수리 요청이 승인되면 정해진 시간에 걸쳐 내구도를 채운다.
    /// <para>
    /// 재보급이 승인과 동시에 끝나는 것과 달리 수리는 지속된다. 그래서 스트라타젬 쪽에
    /// 두지 않고 기체가 들고 있는다 — 채우는 동안 기체가 죽거나 맞을 수 있고, 그 판단은
    /// 기체의 상태를 아는 쪽에서 해야 한다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(AircraftRig))]
    [DisallowMultipleComponent]
    public sealed class AircraftRepair : MonoBehaviour
    {
        private AircraftRig _aircraft;
        private RepairDefinition _active;
        private float _remainingSeconds;

        /// <summary>수리가 시작될 때.</summary>
        public event Action<RepairDefinition> Started;

        /// <summary>수리가 끝나거나 중단될 때. 끝까지 갔으면 true.</summary>
        public event Action<RepairDefinition, bool> Finished;

        /// <summary>수리 중인지.</summary>
        public bool IsRepairing => _active != null;

        /// <summary>남은 시간(초). 수리 중이 아니면 0.</summary>
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>진행도. 0이면 막 시작했고 1이면 다 됐다. 게이지에 그대로 넣는다.</summary>
        public float Progress
        {
            get
            {
                if (_active == null || _active.Duration <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(1f - (_remainingSeconds / _active.Duration));
            }
        }

        private void Awake() => _aircraft = GetComponent<AircraftRig>();

        private void OnEnable()
        {
            if (_aircraft.Stratagems != null)
            {
                _aircraft.Stratagems.Authorized += OnAuthorized;
            }

            if (_aircraft.Health != null)
            {
                _aircraft.Health.Damaged += OnDamaged;
                _aircraft.Health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_aircraft.Stratagems != null)
            {
                _aircraft.Stratagems.Authorized -= OnAuthorized;
            }

            if (_aircraft.Health != null)
            {
                _aircraft.Health.Damaged -= OnDamaged;
                _aircraft.Health.Died -= OnDied;
            }
        }

        private void OnAuthorized(StratagemDefinition stratagem)
        {
            if (stratagem is not RepairDefinition repair)
            {
                return;
            }

            // 진행 중이던 수리는 새로 시작한 것으로 대체된다. 남은 분량을 이어 붙이면
            // 언제 끝나는지 알 수 없게 되고, 화면에 띄울 진행도도 정의할 수 없다.
            if (_active != null)
            {
                Finished?.Invoke(_active, false);
            }

            _active = repair;
            _remainingSeconds = repair.Duration;
            Started?.Invoke(repair);
        }

        private void OnDamaged(Health health, DamageInfo damage)
        {
            if (_active != null && _active.CancelOnDamage)
            {
                Stop(completed: false);
            }
        }

        private void OnDied(Health health, DamageInfo damage) => Stop(completed: false);

        /// <summary>
        /// 지속 시간 동안 목표치 아래로 떨어져 있으면 계속 채운다.
        /// <para>
        /// 목표에 닿아도 끝나지 않고 쉬기만 한다. 그래서 다 채운 뒤에 다시 맞으면
        /// 남은 시간 동안 이어서 회복된다 — 수리는 한 번의 회복이 아니라 <em>버티는
        /// 시간</em>이고, 그 사이에 얻어맞는 것까지가 이 스킬이 감당하는 몫이다.
        /// </para>
        /// </summary>
        private void Update()
        {
            if (_active == null)
            {
                return;
            }

            float step = Mathf.Min(_remainingSeconds, Time.deltaTime);
            _remainingSeconds -= step;

            Health health = _aircraft.Health;
            float missing = _active.TargetFor(health.Max) - health.Current;

            if (missing > 0f)
            {
                // 목표를 넘겨 채우지 않는다. 남은 분량보다 한 걸음이 크면 딱 목표까지만.
                health.Heal(Mathf.Min(_active.RepairRate * step, missing));
            }

            if (_remainingSeconds <= 0f)
            {
                Stop(completed: true);
            }
        }

        /// <summary>수리를 멈춘다. 밖에서 중단시킬 때도 쓴다.</summary>
        public void Stop(bool completed)
        {
            if (_active == null)
            {
                return;
            }

            RepairDefinition finished = _active;
            _active = null;
            _remainingSeconds = 0f;
            Finished?.Invoke(finished, completed);
        }
    }
}
