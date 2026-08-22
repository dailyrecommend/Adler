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

        // 마지막 접근에 들어섰는지. 여기서부터 스쳐 지나가면 놓친 것으로 본다.
        private bool _terminal;

        // 조명탄에 속았는지. 한 번 속으면 되돌아오지 않는다.
        private bool _seduced;

        // 표적의 속도를 읽는 통로. 없으면 위치 변화로 대신 잰다.
        private Rigidbody _targetBody;
        private Vector3 _lastTargetPosition;
        private bool _hasLastTargetPosition;

        /// <summary>터졌을 때. 쏜 쪽이 받아 화면 표시로 넘긴다.</summary>
        public event Action<BlastReport> Detonated;

        /// <summary>
        /// 아직 쫓고 있는 표적. 스쳐 지나가 놓친 뒤에는 null이다.
        /// <para>
        /// 날고 있는 것과 쫓고 있는 것은 다르다. 놓친 미사일도 사거리가 다할 때까지
        /// 계속 날아가지만, 그것을 위협으로 알리면 이미 지나간 것을 두고 계속 선회하게
        /// 만든다 — 경고가 거짓말을 하기 시작하면 다음부터는 믿지 않는다.
        /// </para>
        /// </summary>
        public Transform Target => _spent ? null : _target;

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

            _targetBody = target != null ? target.GetComponentInParent<Rigidbody>() : null;
            _hasLastTargetPosition = false;

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

            TryTakeBait();

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                return;
            }

            if (HasOverShot())
            {
                // 유도만 끈다. 없애지 않는 이유는 스쳐 지나가는 것이 보여야
                // 피했다는 사실이 전달되기 때문이다.
                _target = null;
                return;
            }

            Vector3 desired = ProportionalNavigation.Heading(
                transform.position,
                _velocity,
                _target.position,
                TargetVelocity(deltaTime),
                _definition.NavigationConstant,
                deltaTime);

            if (desired.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // 유도는 어디를 향할지만 정하고, 실제로 꺾이는 양은 여기서 잘린다.
            // 둘을 나눠두면 선회율이 "기체 성능"이라는 뜻을 그대로 유지한다 —
            // 유도를 좋게 만들어도 피할 여지가 사라지지 않는다.
            float speed = _velocity.magnitude;
            Vector3 heading = Vector3.RotateTowards(
                _velocity.normalized,
                desired,
                _definition.TurnRate * Mathf.Deg2Rad * deltaTime,
                0f);

            _velocity = heading * speed;
        }

        /// <summary>
        /// 표적의 속도. 없으면 위치 변화로 재서 쓴다.
        /// <para>
        /// Rigidbody를 먼저 보는 이유는 그쪽이 정확해서다. 위치 차이로 재면 보간된
        /// 좌표를 읽게 되어 값이 떨리고, 그 떨림이 그대로 시선각 변화로 들어가
        /// 미사일이 잔떨림을 쫓느라 경로가 출렁인다.
        /// </para>
        /// </summary>
        private Vector3 TargetVelocity(float deltaTime)
        {
            if (_targetBody != null)
            {
                return _targetBody.linearVelocity;
            }

            Vector3 position = _target.position;

            if (!_hasLastTargetPosition)
            {
                _hasLastTargetPosition = true;
                _lastTargetPosition = position;
                return Vector3.zero;
            }

            Vector3 velocity = (position - _lastTargetPosition) / deltaTime;
            _lastTargetPosition = position;
            return velocity;
        }

        /// <summary>
        /// 조명탄에 속는지 본다.
        /// <para>
        /// 한 번 속으면 되돌아오지 않는다. 잠시 뒤 다시 원래 표적을 찾게 만들면 조명탄이
        /// 시간벌기밖에 되지 않고, 그러면 언제 뿌릴지 계획할 수가 없다. 하나에 하나가
        /// 확실히 걸려야 몇 발 남았는지가 판단거리가 된다.
        /// </para>
        /// <para>
        /// 거리뿐 아니라 각도를 본다. 미사일이 <b>앞으로 보고 있는 쪽</b>에 있어야 속으므로,
        /// 이미 지나간 뒤에 뿌리면 통하지 않는다. 그 조건이 없으면 아무 때나 눌러도 되는
        /// 버튼이 되어 던질 타이밍이 사라진다.
        /// </para>
        /// </summary>
        private void TryTakeBait()
        {
            if (_seduced)
            {
                return;
            }

            Vector3 heading = _velocity.normalized;
            Flare nearest = null;
            float closest = float.MaxValue;

            foreach (Flare flare in Flare.Burning)
            {
                Vector3 toFlare = flare.transform.position - transform.position;
                float distance = toFlare.magnitude;

                if (distance > flare.SeduceRange || distance > closest || distance < 0.01f)
                {
                    continue;
                }

                if (Vector3.Angle(heading, toFlare) > flare.SeduceAngle)
                {
                    continue;
                }

                closest = distance;
                nearest = flare;
            }

            if (nearest == null)
            {
                return;
            }

            _seduced = true;
            _target = nearest.transform;

            // 새 표적이니 지금까지 쌓은 판단을 버린다. 남겨두면 기체를 향해 재던 값으로
            // 조명탄을 쫓게 되어, 엉뚱하게 이미 지나쳤다고 판정한다.
            _terminal = false;
            _targetBody = nearest.GetComponent<Rigidbody>();
            _hasLastTargetPosition = false;
        }

        /// <summary>
        /// 표적을 스쳐 지나갔는지 본다.
        /// <para>
        /// 마지막 접근에 들어선 뒤로, 표적이 진행 방향의 뒤쪽으로 넘어가면 지나친 것이다.
        /// 거리로 재지 않고 방향으로 재는 이유는 프레임마다 움직인 거리가 달라도 판정이
        /// 흔들리지 않기 때문이다.
        /// </para>
        /// <para>
        /// 멀리서부터 보지 않고 <see cref="MissileDefinition.MissRange"/> 안에 들어온
        /// 뒤에만 본다. 먼 거리에서는 표적이 옆으로 크게 움직이기만 해도 잠깐 뒤로
        /// 넘어가는데, 그것까지 놓친 것으로 치면 미사일이 아무 데서나 포기한다.
        /// </para>
        /// </summary>
        private bool HasOverShot()
        {
            if (_definition.MissRange <= 0f)
            {
                return false;
            }

            Vector3 toTarget = _target.position - transform.position;

            if (!_terminal)
            {
                _terminal = toTarget.magnitude <= _definition.MissRange;
                return false;
            }

            return Vector3.Dot(_velocity, toTarget) <= 0f;
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
