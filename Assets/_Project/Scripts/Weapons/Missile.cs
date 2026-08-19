using System;
using Adler.Combat;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 표적을 쫓아가는 미사일.
    /// <para>
    /// 탄과 마찬가지로 스스로 움직이고 지나온 경로를 훑는다. 다만 방향을 바꿀 수 있어서,
    /// 꺾는 속도가 곧 피할 수 있는 여지가 된다 — 급기동하는 표적은 놓친다.
    /// </para>
    /// <para>
    /// 표적을 잃어도 사라지지 않는다. 마지막으로 향하던 방향으로 계속 날아가다 사거리에서
    /// 사라진다. 표적이 죽는 순간 미사일이 공중에서 증발하면 무슨 일이 있었는지 알 수 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Missile : MonoBehaviour
    {
        [Header("판정")]
        [Tooltip("무언가에 닿으면 터진다. 지형을 포함할 것.")]
        [SerializeField] private LayerMask _impactMask = ~0;

        [Tooltip("경로를 훑을 때 쓰는 굵기 (m).")]
        [Min(0.01f)]
        [SerializeField] private float _sweepRadius = 0.2f;

        [Header("연출")]
        [SerializeField] private GameObject _explosionEffect;

        [SerializeField] private float _explosionEffectLifetime = 3f;

        private MissileDefinition _definition;
        private LayerMask _blastMask;
        private GameObject _owner;
        private Transform _target;
        private Vector3 _velocity;
        private float _traveled;
        private float _straightRemaining;
        private bool _spent;

        /// <summary>터졌을 때. 쏜 쪽이 받아 화면 표시로 넘긴다.</summary>
        public event Action<BlastReport> Detonated;

        /// <summary>쏜 쪽이 성능과 표적을 넘겨준다.</summary>
        public void Launch(
            MissileDefinition definition,
            GameObject owner,
            Transform target,
            Vector3 velocity,
            LayerMask blastMask)
        {
            _definition = definition;
            _owner = owner;
            _target = target;
            _velocity = velocity;
            _blastMask = blastMask;
            _straightRemaining = definition.StraightFlightSeconds;

            AlignToTravel();
        }

        private void FixedUpdate()
        {
            if (_spent || _definition == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;

            Steer(dt);
            Accelerate(dt);

            Vector3 from = transform.position;
            Vector3 to = from + (_velocity * dt);

            if (SweepForImpact(from, to, out Vector3 point))
            {
                Detonate(point);
                return;
            }

            transform.position = to;
            AlignToTravel();

            _traveled += Vector3.Distance(from, to);
            if (_traveled >= _definition.Range)
            {
                // 사거리 끝에서는 터지지 않고 사라진다. 아무것도 없는 하늘에서
                // 폭발이 일어나면 무언가를 맞힌 것으로 잘못 읽힌다.
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 표적 쪽으로 방향을 꺾는다. 발사 직후 잠깐은 곧장 나아가 기체에서 떨어져 나온다.
        /// </summary>
        private void Steer(float deltaTime)
        {
            if (_straightRemaining > 0f)
            {
                _straightRemaining -= deltaTime;
                return;
            }

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                return;
            }

            Vector3 desired = (_target.position - transform.position).normalized;
            if (desired.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float speed = _velocity.magnitude;
            Vector3 heading = Vector3.RotateTowards(
                _velocity.normalized,
                desired,
                _definition.TurnRate * Mathf.Deg2Rad * deltaTime,
                0f);

            _velocity = heading * speed;
        }

        private void Accelerate(float deltaTime)
        {
            float speed = Mathf.MoveTowards(
                _velocity.magnitude, _definition.MaxSpeed, _definition.Acceleration * deltaTime);

            _velocity = _velocity.normalized * speed;
        }

        /// <summary>
        /// 지나온 경로를 훑는다. 표적 근처를 스치기만 해도 터지도록 근접 판정을 함께 본다 —
        /// 정확히 부딪히기를 기다리면 빠른 표적 옆을 지나쳐 버린다.
        /// </summary>
        private bool SweepForImpact(Vector3 from, Vector3 to, out Vector3 point)
        {
            Vector3 travel = to - from;
            float distance = travel.magnitude;

            if (distance > 0.0001f && Physics.SphereCast(
                    from, _sweepRadius, travel / distance, out RaycastHit hit,
                    distance, _impactMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            if (_target != null && _definition.ProximityRadius > 0f)
            {
                float toTarget = Vector3.Distance(to, _target.position);
                if (toTarget <= _definition.ProximityRadius)
                {
                    point = to;
                    return true;
                }
            }

            point = default;
            return false;
        }

        private void Detonate(Vector3 position)
        {
            _spent = true;

            Collider[] hits = Physics.OverlapSphere(
                position, _definition.BlastRadius, _blastMask, QueryTriggerInteraction.Ignore);

            var struck = new System.Collections.Generic.HashSet<IDamageable>();
            int damaged = 0;
            int blocked = 0;
            int killed = 0;

            foreach (Collider collider in hits)
            {
                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || !struck.Add(damageable))
                {
                    continue;
                }

                // 콜라이더 표면이 아니라 경계 상자를 쓴다. Collider.ClosestPoint는 볼록한
                // 것에만 쓸 수 있어 지형이나 건물 메시를 만나면 예외가 난다.
                Vector3 point = collider.bounds.ClosestPoint(position);
                float amount = ResolveDamage(Vector3.Distance(position, point));

                if (amount <= 0f)
                {
                    continue;
                }

                DamageResult result = damageable.TakeDamage(new DamageInfo(
                    amount,
                    _definition.Penetration,
                    _definition.Demolition,
                    point,
                    (point - position).sqrMagnitude > 0.0001f
                        ? (point - position).normalized
                        : Vector3.up,
                    _owner != null ? _owner : gameObject));

                if (result.Blocked)
                {
                    blocked++;
                }
                else
                {
                    damaged++;
                    if (result.Killed)
                    {
                        killed++;
                    }
                }
            }

            Detonated?.Invoke(new BlastReport(damaged, blocked, killed, position));

            if (_explosionEffect != null)
            {
                Destroy(Instantiate(_explosionEffect, position, Quaternion.identity), _explosionEffectLifetime);
            }

            Destroy(gameObject);
        }

        private float ResolveDamage(float distance)
        {
            if (distance <= _definition.InnerRadius)
            {
                return _definition.Damage;
            }

            if (distance >= _definition.BlastRadius)
            {
                return 0f;
            }

            return _definition.Damage
                   * Mathf.InverseLerp(_definition.BlastRadius, _definition.InnerRadius, distance);
        }

        private void AlignToTravel()
        {
            if (_velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity);
            }
        }
    }
}
