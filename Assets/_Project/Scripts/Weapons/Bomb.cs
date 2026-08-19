using System;
using Adler.Combat;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 폭발 한 번이 남긴 결과를 뭉뚱그린 것.
    /// <para>
    /// 폭발은 한 번에 여러 표적을 때리므로, 명중 하나하나를 알려주면 화면 표시가
    /// 같은 순간에 여러 번 뜬다. 한 번의 폭발이 무엇을 했는지만 알면 충분하다.
    /// </para>
    /// </summary>
    public readonly struct BlastReport
    {
        /// <summary>피해가 들어간 표적 수.</summary>
        public readonly int Damaged;

        /// <summary>장갑이나 구조에 막힌 표적 수.</summary>
        public readonly int Blocked;

        /// <summary>쓰러뜨린 표적 수.</summary>
        public readonly int Killed;

        public readonly Vector3 Position;

        public BlastReport(int damaged, int blocked, int killed, Vector3 position)
        {
            Damaged = damaged;
            Blocked = blocked;
            Killed = killed;
            Position = position;
        }

        /// <summary>아무것도 못 맞혔다. 빈 땅에 떨어진 경우.</summary>
        public bool MissedEverything => Damaged == 0 && Blocked == 0;
    }

    /// <summary>
    /// 투하된 폭탄. 떨어지다가 무언가에 닿으면 터진다.
    /// <para>
    /// 투하 직후에는 무장되지 않아 터지지 않는다. 이 지연이 없으면 저공으로 지나가며
    /// 떨군 폭탄이 기체 바로 아래에서 터져 자기가 휘말린다. 급강하 폭격에서는
    /// 인출 시간을 벌어주는 역할도 한다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class Bomb : MonoBehaviour
    {
        [Header("판정")]
        [Tooltip("폭발이 피해를 줄 대상의 레이어.")]
        [SerializeField] private LayerMask _blastMask = ~0;

        [Tooltip("이것에 닿으면 터진다. 지면 레이어를 반드시 포함할 것.")]
        [SerializeField] private LayerMask _impactMask = ~0;

        [Tooltip("경로를 훑을 때 쓰는 굵기 (m). 폭탄 콜라이더 크기쯤이면 된다.")]
        [Min(0.01f)]
        [SerializeField] private float _impactRadius = 0.15f;

        [Tooltip("닿는 순간 그 자리에 박힌다.\n" +
                 "끄면 투하 속도를 그대로 안고 지면을 미끄러진다.")]
        [SerializeField] private bool _stopOnImpact = true;

        [Tooltip("이 시간이 지나도 아무것도 못 맞히면 스스로 사라진다(초).")]
        [Min(1f)]
        [SerializeField] private float _maxLifetime = 30f;

        [Header("연출")]
        [Tooltip("터질 때 남길 효과. 비워둬도 된다.")]
        [SerializeField] private GameObject _explosionEffect;

        [SerializeField] private float _explosionEffectLifetime = 3f;

        [Header("디버그")]
        [Tooltip("낙하 중 폭발 범위를 그리고, 터진 자리에 잠시 남긴다.\n" +
                 "씬 뷰에서 항상 보이며, 게임 뷰에서는 상단의 Gizmos를 켜야 보인다.")]
        [SerializeField] private bool _drawBlastRange = true;

        [Tooltip("터진 자리에 범위가 남아 있는 시간(초).")]
        [Min(0f)]
        [SerializeField] private float _blastDebugDuration = 3f;

        private BombDefinition _definition;
        private GameObject _owner;
        private float _armedAt;
        private bool _detonated;
        private Vector3 _previousPosition;
        private Vector3 _restingContact;
        private bool _hasRestingContact;
        private bool _stopped;

        private bool IsArmed => Time.time >= _armedAt;

        /// <summary>터졌을 때. 투하한 쪽이 받아 화면 표시로 넘긴다.</summary>
        public event Action<BlastReport> Detonated;

        /// <summary>투하한 쪽이 성능과 주인을 넘겨준다.</summary>
        public void Arm(BombDefinition definition, GameObject owner)
        {
            _definition = definition;
            _owner = owner;
            _armedAt = Time.time + definition.ArmingDelay;
            _previousPosition = transform.position;

            // 빠르게 떨어지는 물체라 물리 엔진의 기본 판정으로는 얇은 지면을 지나친다.
            // 경로를 직접 훑기도 하지만, 엔진 쪽도 함께 올려두면 확실해진다.
            if (TryGetComponent(out Rigidbody body))
            {
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        private void Start()
        {
            if (_definition == null)
            {
                Debug.LogError($"{nameof(Bomb)}: {nameof(Arm)}이 호출되지 않아 성능을 알 수 없습니다.", this);
                Destroy(gameObject);
                return;
            }

            Destroy(gameObject, _maxLifetime);
        }

        /// <summary>
        /// 지나온 경로를 훑어 무엇이든 스쳤는지 본다.
        /// <para>
        /// 폭탄은 기체 속도를 물려받고 중력까지 붙어 한 물리 스텝에 1미터 가까이 움직인다.
        /// 그 사이에 있던 얇은 지면은 충돌로 잡히지 않고 그냥 통과된다. 옆에서 부딪히는
        /// 두꺼운 물체만 걸리는 이유가 이것이다.
        /// </para>
        /// </summary>
        private void FixedUpdate()
        {
            if (_detonated || _definition == null)
            {
                return;
            }

            Vector3 current = transform.position;

            if (SweepForImpact(_previousPosition, current, out Vector3 point))
            {
                if (IsArmed)
                {
                    Detonate(point);
                    return;
                }

                // 무장 전에 닿았다. 지금은 터뜨릴 수 없으므로 그 자리에 붙잡아 둔다.
                Settle(point);
            }
            else if (IsArmed && _hasRestingContact)
            {
                Detonate(_restingContact);
                return;
            }

            _previousPosition = current;
        }

        private bool SweepForImpact(Vector3 from, Vector3 to, out Vector3 point)
        {
            Vector3 travel = to - from;
            float distance = travel.magnitude;

            if (distance > 0.0001f && Physics.SphereCast(
                    from, _impactRadius, travel / distance, out RaycastHit swept,
                    distance, _impactMask, QueryTriggerInteraction.Ignore))
            {
                point = swept.point;
                return true;
            }

            // 거의 멈춘 상태에서는 훑을 거리가 없다. 겹쳐 있는지로 판단한다.
            Collider[] overlapped = Physics.OverlapSphere(
                to, _impactRadius, _impactMask, QueryTriggerInteraction.Ignore);

            if (overlapped.Length > 0)
            {
                // 닿은 면 위의 정확한 점을 구하지 않는다. Collider.ClosestPoint는 볼록한
                // 콜라이더에서만 쓸 수 있어서 지형 메시에서는 쓸 수 없고, 어차피 폭탄이
                // 그 자리에 박혀 있으므로 자기 위치가 곧 폭발 지점이다.
                point = to;
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>
        /// 물리 충돌로도 받는다. 다만 경로를 훑는 쪽과 같은 레이어 조건을 지켜야 한다.
        /// <para>
        /// 충돌 콜백은 레이어를 가리지 않고 들어오므로, 여기서 걸러내지 않으면 투하 순간
        /// 기체를 스친 폭탄이 그 자리에 박혀 버린다. 마스크는 제대로 설정돼 있는데
        /// 코드가 보지 않는 셈이라 원인을 짐작하기 어려운 종류의 고장이다.
        /// </para>
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (_detonated)
            {
                return;
            }

            if ((_impactMask.value & (1 << collision.gameObject.layer)) == 0)
            {
                return;
            }

            Vector3 point = collision.GetContact(0).point;

            if (IsArmed)
            {
                Detonate(point);
                return;
            }

            Settle(point);
        }

        /// <summary>
        /// 닿은 자리에 박아 둔다.
        /// <para>
        /// 폭탄은 기체 속도를 그대로 물려받으므로, 놓아두면 착지 후에도 그 속도로
        /// 지면을 미끄러진다. 마찰만으로는 수십 m/s가 쉽게 죽지 않는다.
        /// </para>
        /// <para>
        /// 무장 지연 동안 미끄러지면 폭발 지점이 조준한 곳에서 벗어나기도 한다.
        /// 터지는 자리는 눈에 보이는 자리와 같아야 한다.
        /// </para>
        /// </summary>
        private void Settle(Vector3 point)
        {
            _restingContact = point;
            _hasRestingContact = true;

            if (!_stopOnImpact || _stopped)
            {
                return;
            }

            _stopped = true;

            if (TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        private void Detonate(Vector3 position)
        {
            _detonated = true;

            Collider[] hits = Physics.OverlapSphere(
                position, _definition.BlastRadius, _blastMask, QueryTriggerInteraction.Ignore);

            // 한 표적에 콜라이더가 여럿일 수 있다. 같은 대상을 여러 번 때리면
            // 몸집이 큰 표적일수록 폭발에 약해지는 이상한 결과가 나온다.
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

                // 콜라이더 표면이 아니라 경계 상자에서 가장 가까운 점을 쓴다.
                // Collider.ClosestPoint는 볼록한 것에만 쓸 수 있어 건물이나 지형처럼
                // 오목한 메시를 만나면 예외가 난다. 감쇠 계산에는 이 정도면 충분하다.
                Vector3 point = collider.bounds.ClosestPoint(position);
                float amount = ResolveDamage(Vector3.Distance(position, point));
                if (amount <= 0f)
                {
                    continue;
                }

                Vector3 normal = (point - position).sqrMagnitude > 0.0001f
                    ? (point - position).normalized
                    : Vector3.up;

                DamageResult result = damageable.TakeDamage(new DamageInfo(
                    amount,
                    _definition.Penetration,
                    _definition.Demolition,
                    point,
                    normal,
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
            DrawBlastRange(position);

            if (_explosionEffect != null)
            {
                GameObject effect = Instantiate(_explosionEffect, position, Quaternion.identity);
                Destroy(effect, _explosionEffectLifetime);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 안쪽 반경까지는 온전한 피해가, 거기서 폭발 반경까지는 줄어든 피해가 들어간다.
        /// 가장자리에 걸친 표적이 중심에서 맞은 것과 똑같이 부서지면 조준할 이유가 없어진다.
        /// </summary>
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

            float t = Mathf.InverseLerp(_definition.BlastRadius, _definition.InnerRadius, distance);
            return _definition.Damage * t;
        }

        /// <summary>
        /// 낙하 중에는 폭탄을 따라다니며 범위를 보여준다. 선택하지 않아도 그리는 이유는,
        /// 떨어지는 폭탄을 클릭해서 붙잡고 있을 수가 없기 때문이다.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!_drawBlastRange || _definition == null || _detonated)
            {
                return;
            }

            Gizmos.color = InnerColor;
            Gizmos.DrawWireSphere(transform.position, _definition.InnerRadius);
            Gizmos.color = OuterColor;
            Gizmos.DrawWireSphere(transform.position, _definition.BlastRadius);
        }

        private static readonly Color InnerColor = new Color(1f, 0.35f, 0.1f, 0.9f);
        private static readonly Color OuterColor = new Color(1f, 0.75f, 0.2f, 0.5f);

        /// <summary>
        /// 터진 자리에 범위를 잠시 남긴다. 폭탄은 터지는 즉시 사라지므로 기즈모로는
        /// 정작 확인하고 싶은 순간을 볼 수 없다. 선으로 그려두면 그 자리에 남는다.
        /// </summary>
        private void DrawBlastRange(Vector3 center)
        {
            if (!_drawBlastRange || _blastDebugDuration <= 0f)
            {
                return;
            }

            DrawDebugSphere(center, _definition.InnerRadius, InnerColor, _blastDebugDuration);
            DrawDebugSphere(center, _definition.BlastRadius, OuterColor, _blastDebugDuration);
        }

        /// <summary>세 방향의 원을 그려 구를 흉내 낸다. Debug에는 구를 그리는 수단이 없다.</summary>
        private static void DrawDebugSphere(Vector3 center, float radius, Color color, float duration)
        {
            const int Segments = 32;
            const float Step = 2f * Mathf.PI / Segments;

            for (int i = 0; i < Segments; i++)
            {
                float a = i * Step;
                float b = (i + 1) * Step;

                float ca = Mathf.Cos(a) * radius;
                float sa = Mathf.Sin(a) * radius;
                float cb = Mathf.Cos(b) * radius;
                float sb = Mathf.Sin(b) * radius;

                Debug.DrawLine(
                    center + new Vector3(ca, sa, 0f), center + new Vector3(cb, sb, 0f), color, duration);
                Debug.DrawLine(
                    center + new Vector3(ca, 0f, sa), center + new Vector3(cb, 0f, sb), color, duration);
                Debug.DrawLine(
                    center + new Vector3(0f, ca, sa), center + new Vector3(0f, cb, sb), color, duration);
            }
        }
    }
}
