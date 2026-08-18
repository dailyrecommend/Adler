using Adler.Combat;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 격추된 기체를 조종에서 놓아주고 물리에 맡긴다.
    /// <para>
    /// 비행 모델이 매 물리 스텝마다 속도와 회전을 덮어쓰기 때문에, 죽었다고 표시하는
    /// 것만으로는 아무것도 달라지지 않는다. 조종 컴포넌트를 꺼야 비로소 기체가
    /// 자기 힘으로 날기를 멈춘다.
    /// </para>
    /// <para>
    /// 그 순간 중력을 되돌려 주면 잔해가 관성을 그대로 안고 떨어진다. 화면이 갑자기
    /// 멈추는 대신 추락하는 모습이 남아, 무엇 때문에 죽었는지 눈으로 확인할 수 있다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class AircraftWreck : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField] private Health _health;

        [Tooltip("격추되면 끌 컴포넌트들. 조종과 무기를 넣는다.")]
        [SerializeField] private Behaviour[] _disableOnDeath = System.Array.Empty<Behaviour>();

        [Header("잔해")]
        [Tooltip("체크하면 격추 시 중력을 되돌려 잔해가 떨어진다.")]
        [SerializeField] private bool _restorePhysics = true;

        [Tooltip("잔해가 회전하며 떨어지는 정도.")]
        [SerializeField] private float _tumbleTorque = 4f;

        private Rigidbody _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();

            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_health == null)
            {
                Debug.LogError($"{nameof(AircraftWreck)}: 감시할 Health를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
            }
        }

        /// <summary>
        /// 격추될 때 손댄 것들을 되돌린다. 리스폰이 부른다.
        /// <para>
        /// 무엇을 껐는지 아는 것은 이쪽이므로 되살리는 일도 여기서 맡는다. 리스폰 쪽에
        /// 같은 목록을 한 번 더 적어두면 한쪽만 고쳤을 때 조종이 돌아오지 않는다.
        /// </para>
        /// </summary>
        public void Restore()
        {
            foreach (Behaviour behaviour in _disableOnDeath)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            // 중력과 감쇠는 비행 모델이 자기 방식대로 다시 잡는다.
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        private void OnDied(Health health, DamageInfo damage)
        {
            foreach (Behaviour behaviour in _disableOnDeath)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }

            if (!_restorePhysics)
            {
                return;
            }

            // 비행 모델이 꺼둔 것들을 되돌린다. 여기서 되살리지 않으면 잔해가
            // 부딪힌 자리에 그대로 떠 있게 된다.
            _body.useGravity = true;
            _body.linearDamping = 0.2f;
            _body.angularDamping = 0.4f;

            if (_tumbleTorque > 0f)
            {
                _body.AddTorque(Random.onUnitSphere * _tumbleTorque, ForceMode.VelocityChange);
            }
        }
    }
}
