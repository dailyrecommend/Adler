using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 무언가에 부딪혔을 때 자신이 피해를 입는다. 지면이든 벽이든 건물이든,
    /// 걸러낼 레이어만 정해두면 같은 코드로 처리된다.
    /// <para>
    /// 충돌 피해는 장갑이나 구조 관문을 거치지 않는다. 장갑은 총탄을 막으라고 있는 것이지
    /// 절벽에 처박히는 것을 막아주지는 않는다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class ImpactDamage : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField] private Health _health;

        [Tooltip("부딪혔을 때 피해를 입을 상대의 레이어.")]
        [SerializeField] private LayerMask _impactMask = ~0;

        [Header("판정")]
        [Tooltip("이 속도(m/s) 미만으로 스치면 아무 일도 없다.\n" +
                 "활주로에 살짝 닿거나 벽을 긁는 정도까지 격추되면 답답해진다.")]
        [Min(0f)]
        [SerializeField] private float _minImpactSpeed = 6f;

        [Tooltip("체크하면 위 속도를 넘긴 충돌은 무조건 격추다.\n" +
                 "끄면 충돌 속도에 비례해 피해를 준다.")]
        [SerializeField] private bool _lethal = true;

        [Tooltip("격추가 아닐 때, 충돌 속도 1m/s당 들어가는 피해량.")]
        [Min(0f)]
        [SerializeField] private float _damagePerImpactSpeed = 4f;

        // 이번 충돌을 막아줄 것들. 매번 계층을 뒤지지 않게 한 번만 모아둔다.
        private IImpactShield[] _shields;

        private void Awake()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            // 자신과 위쪽에서 찾는다. 막아주는 쪽은 기체를 대표하는 자리에 붙으므로
            // 아래로 뒤질 일이 없고, 아래까지 훑으면 무기나 부품이 끼어들 수 있다.
            _shields = GetComponentsInParent<IImpactShield>(includeInactive: true);

            if (_health == null)
            {
                Debug.LogError($"{nameof(ImpactDamage)}: 피해를 받을 Health를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_health.IsAlive)
            {
                return;
            }

            if ((_impactMask.value & (1 << collision.gameObject.layer)) == 0)
            {
                return;
            }

            if (IsShielded(collision))
            {
                return;
            }

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < _minImpactSpeed)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            float amount = _lethal ? _health.Max : impactSpeed * _damagePerImpactSpeed;

            // 장갑과 구조 관문을 지나치게 한다. 충돌은 뚫고 말고의 문제가 아니다.
            _health.TakeDamage(new DamageInfo(
                amount,
                float.MaxValue,
                float.MaxValue,
                contact.point,
                contact.normal,
                collision.gameObject));
        }

        /// <summary>누군가 이 충돌에 손을 들었는지.</summary>
        private bool IsShielded(Collision collision)
        {
            foreach (IImpactShield shield in _shields)
            {
                if (shield.Blocks(collision))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
