using System;
using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 내구도를 가진 표적. 무기가 통하는지는 두 관문으로 갈린다.
    /// <para>
    /// <b>장갑</b>은 유닛을 지킨다. 무기의 관통력이 장갑에 못 미치면 튕겨 나가고 피해가 없다.
    /// <b>요구 철거력</b>은 건물을 지킨다. 무기의 철거력이 모자라면 아무리 때려도 부서지지 않는다.
    /// </para>
    /// <para>
    /// 둘 다 0이 기본값이라 보병처럼 아무 무기에나 상하는 표적은 그대로 두면 된다.
    /// 하나만 쓰는 것이 보통이지만, 벙커처럼 장갑과 구조를 함께 갖춘 표적도 표현할 수 있다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Header("내구도")]
        [SerializeField] private float _maxHealth = 30f;

        [Header("장갑 (유닛)")]
        [Tooltip("무기의 관통력이 이 값보다 낮으면 피해가 전혀 들어가지 않는다.\n" +
                 "보병처럼 장갑이 없는 대상은 0으로 둔다.")]
        [Min(0f)]
        [SerializeField] private float _armor;

        [Header("구조 (건물)")]
        [Tooltip("무기의 철거력이 이 값보다 낮으면 부술 수 없다.\n" +
                 "건물이 아닌 표적은 0으로 둔다. 기총에는 철거력이 없으므로,\n" +
                 "이 값을 올리면 폭탄으로만 파괴되는 표적이 된다.")]
        [Min(0f)]
        [SerializeField] private float _requiredDemolition;

        [Header("처치 처리")]
        [Tooltip("체크하면 죽을 때 오브젝트를 끈다. 연출을 붙이기 전까지의 임시 처리다.")]
        [SerializeField] private bool _deactivateOnDeath = true;

        private float _current;

        /// <summary>피해를 입을 때마다. 피격 효과와 HUD가 구독한다.</summary>
        public event Action<Health, DamageInfo> Damaged;

        /// <summary>관문에 막혔을 때. 도탄 표시나 "철거력 부족" 안내를 띄우는 자리.</summary>
        public event Action<Health, DamageInfo, DamageRejection> Blocked;

        /// <summary>내구도가 0이 될 때 한 번만.</summary>
        public event Action<Health, DamageInfo> Died;

        /// <summary>되살아났을 때. 피해와 달리 신호 없이 값만 바뀌면 화면 표시가 옛 값에 머문다.</summary>
        public event Action<Health> Revived;

        /// <summary>회복됐을 때. 실제로 채워진 양이 함께 온다.</summary>
        public event Action<Health, float> Healed;

        public bool IsAlive => _current > 0f;
        public float Current => _current;
        public float Max => _maxHealth;
        public float Normalized => _maxHealth > 0f ? _current / _maxHealth : 0f;
        public float Armor => _armor;
        public float RequiredDemolition => _requiredDemolition;

        private void Awake() => _current = _maxHealth;

        private void OnEnable() => _current = _maxHealth;

        public DamageResult TakeDamage(in DamageInfo damage)
        {
            if (!IsAlive)
            {
                return DamageResult.None;
            }

            DamageRejection rejection = Evaluate(damage);
            if (rejection != DamageRejection.None)
            {
                // 맞았지만 통하지 않았다. 이것도 플레이어에게 알려야 무기를 바꿀 판단이 선다.
                Blocked?.Invoke(this, damage, rejection);
                return new DamageResult(rejection, 0f, killed: false);
            }

            float applied = Mathf.Min(_current, damage.Amount);
            _current -= applied;
            Damaged?.Invoke(this, damage);

            if (_current > 0f)
            {
                return new DamageResult(DamageRejection.None, applied, killed: false);
            }

            Died?.Invoke(this, damage);

            if (_deactivateOnDeath)
            {
                gameObject.SetActive(false);
            }

            return new DamageResult(DamageRejection.None, applied, killed: true);
        }

        /// <summary>
        /// 두 관문을 모두 통과해야 피해가 들어간다. 어느 쪽에 막혔는지 구분해 돌려주는 이유는,
        /// "튕겨 나감"과 "건물이 끄떡없음"이 플레이어에게 전혀 다른 행동을 요구하기 때문이다.
        /// 앞은 관통력이 높은 무기를, 뒤는 폭탄을 가져오라는 뜻이다.
        /// <para>
        /// 수치가 똑같으면 공격하는 쪽이 이긴다. 그래서 비교가 &lt;= 가 아니라 &lt; 다.
        /// 장갑 30을 뚫으려고 관통력 30짜리 무기를 골라온 플레이어가 튕겨 나가면,
        /// 표기된 숫자를 믿을 수 없게 된다.
        /// </para>
        /// </summary>
        private DamageRejection Evaluate(in DamageInfo damage)
        {
            if (_requiredDemolition > 0f && damage.Demolition < _requiredDemolition)
            {
                return DamageRejection.Demolition;
            }

            if (_armor > 0f && damage.Penetration < _armor)
            {
                return DamageRejection.Armor;
            }

            return DamageRejection.None;
        }

        /// <summary>
        /// 내구도를 채운다. 실제로 채워진 양을 돌려준다.
        /// <para>
        /// 이미 쓰러진 것은 회복시키지 않는다. 되살리는 일은 <see cref="Revive"/>의 몫이고,
        /// 둘을 섞으면 수리 스킬이 격추된 기체를 공중에서 일으켜 세우게 된다.
        /// </para>
        /// </summary>
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return 0f;
            }

            float applied = Mathf.Min(amount, _maxHealth - _current);
            if (applied <= 0f)
            {
                return 0f;
            }

            _current += applied;
            Healed?.Invoke(this, applied);
            return applied;
        }

        /// <summary>표적을 재사용할 때 되살린다.</summary>
        public void Revive()
        {
            _current = _maxHealth;
            if (_deactivateOnDeath)
            {
                gameObject.SetActive(true);
            }

            Revived?.Invoke(this);
        }
    }
}
