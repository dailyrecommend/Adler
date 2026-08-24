using System;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 총구를 떠나 날아가는 탄 한 발.
    /// <para>
    /// Rigidbody에 맡기지 않고 직접 움직인다. 초당 120m로 나는 탄은 물리 한 스텝에
    /// 2미터 넘게 건너뛰므로, 엔진의 충돌 판정에 맡기면 얇은 표적과 지면을 지나쳐 버린다.
    /// 매 스텝 지나온 경로를 훑으면 아무리 빨라도 놓치지 않는다.
    /// </para>
    /// <para>
    /// 직접 움직이는 편이 가볍기도 하다. 기총은 초당 열다섯 발씩 쏟아지므로 탄 하나하나가
    /// 물리 엔진에 등록되면 그만큼 부담이 쌓인다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        [Header("꼬리")]
        [Tooltip("트레일이 남길 길이 (m). 0이면 트레일 설정을 그대로 둔다.\n" +
                 "TrailRenderer에는 길이 항목이 없고 지속 시간만 있어서, 실제 속도를 보고\n" +
                 "시간을 역산한다. 기체 속도가 얹히면 탄속이 달라지므로 고정값으로는\n" +
                 "발사할 때마다 꼬리 길이가 들쭉날쭉해진다.")]
        [Min(0f)]
        [SerializeField] private float _trailLength = 8f;

        [Tooltip("맞는 순간 꼬리를 떼어내 제자리에서 사라지게 한다.\n" +
                 "끄면 탄과 함께 즉시 사라져 꼬리가 툭 끊긴다.")]
        [SerializeField] private bool _detachTrailOnImpact = true;

        private TrailRenderer _trail;
        private GunDefinition _gun;
        private GameObject _owner;
        private LayerMask _hitMask;
        private Clock _clock;
        private Vector3 _velocity;
        private float _traveled;
        private bool _spent;

        /// <summary>무언가에 맞았을 때. 쏜 총이 받아 화면 표시로 넘긴다.</summary>
        public event Action<RaycastHit, IDamageable, DamageResult> Struck;

        /// <summary>쏜 총이 성능과 초기 속도를 넘겨준다.</summary>
        public void Launch(GunDefinition gun, GameObject owner, Vector3 velocity, LayerMask hitMask)
        {
            _clock = TimeScale.For(this);
            _gun = gun;
            _owner = owner;
            _velocity = velocity;
            _hitMask = hitMask;

            _trail = GetComponentInChildren<TrailRenderer>();

            AlignToTravel();
            UpdateTrailLength();
        }

        /// <summary>
        /// 남기고 싶은 길이를 지속 시간으로 바꿔 넣는다.
        /// 꼬리 길이 = 속도 × 지속 시간 이므로, 시간 = 길이 ÷ 속도다.
        /// </summary>
        private void UpdateTrailLength()
        {
            if (_trail == null || _trailLength <= 0f)
            {
                return;
            }

            float speed = _velocity.magnitude;
            if (speed > 0.01f)
            {
                _trail.time = _trailLength / speed;
            }
        }

        private void FixedUpdate()
        {
            if (_spent || _gun == null)
            {
                return;
            }

            float dt = _clock.FixedDelta;
            _velocity += Physics.gravity * (_gun.GravityScale * dt);

            Vector3 from = transform.position;
            Vector3 to = from + (_velocity * dt);

            if (SweepForImpact(from, to, out RaycastHit hit))
            {
                Impact(hit);
                return;
            }

            transform.position = to;
            AlignToTravel();

            // 중력으로 속도가 변하므로 꼬리 길이도 따라 맞춰준다.
            UpdateTrailLength();

            _traveled += Vector3.Distance(from, to);
            if (_traveled >= _gun.Range)
            {
                ReleaseTrail();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 꼬리를 떼어내 그 자리에서 자연스럽게 사라지게 한다.
        /// 탄과 함께 지우면 남아 있던 꼬리가 한 프레임에 통째로 없어져 툭 끊긴다.
        /// </summary>
        private void ReleaseTrail()
        {
            if (!_detachTrailOnImpact || _trail == null)
            {
                return;
            }

            _trail.transform.SetParent(null, worldPositionStays: true);
            _trail.autodestruct = true;
            _trail.emitting = false;
            _trail = null;
        }

        private bool SweepForImpact(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 travel = to - from;
            float distance = travel.magnitude;

            if (distance <= 0.0001f)
            {
                hit = default;
                return false;
            }

            Vector3 direction = travel / distance;

            return _gun.HitRadius > 0f
                ? Physics.SphereCast(from, _gun.HitRadius, direction, out hit, distance,
                    _hitMask, QueryTriggerInteraction.Ignore)
                : Physics.Raycast(from, direction, out hit, distance,
                    _hitMask, QueryTriggerInteraction.Ignore);
        }

        private void Impact(RaycastHit hit)
        {
            _spent = true;

            // 콜라이더가 자식에 있어도 본체의 Health를 찾아야 한다.
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            DamageResult result = DamageResult.None;

            if (damageable != null && damageable.IsAlive)
            {
                result = damageable.TakeDamage(new DamageInfo(
                    _gun.Damage,
                    _gun.Penetration,
                    _gun.Demolition,
                    hit.point,
                    hit.normal,
                    _owner != null ? _owner : gameObject));
            }

            Struck?.Invoke(hit, damageable, result);
            ReleaseTrail();
            Destroy(gameObject);
        }

        /// <summary>탄이 날아가는 방향을 보게 한다. 길쭉한 탄일수록 이게 없으면 어색하다.</summary>
        private void AlignToTravel()
        {
            if (_velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity);
            }
        }
    }
}
