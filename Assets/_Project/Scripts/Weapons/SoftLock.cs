using Adler.Core;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 잡아둔 적에게 기총 탄을 살짝 몰아준다.
    /// <para>
    /// 이 게임은 적이 잘 피하는 게임이 아니라 플레이어가 잘 맞히는 게임이어야 한다.
    /// 초속 120m로 날아가는 탄으로 급기동하는 기체를 맞히려면 앞을 얼마나 겨눠야
    /// 하는지 계산해야 하는데, 그 계산이 실력의 전부가 되면 남는 것은 스트레스뿐이다.
    /// </para>
    /// <para>
    /// 그래서 겨눈 곳이 아니라 <b>맞을 곳</b>을 대신 계산해 준다. 다만 오차를 전부
    /// 메우지도, 얼마든지 꺾어주지도 않는다. 다 메우면 방아쇠가 곧 명중이라 겨눌
    /// 이유가 없어지고, 꺾는 각을 열어두면 조준점 밖의 적에게 탄이 휘어 날아가
    /// 어디로 쏘고 있는지 알 수 없게 된다. 거드는 데까지가 몫이다.
    /// </para>
    /// <para>
    /// 표적을 스스로 찾지 않고 <see cref="LockOnTargeting"/>이 잡은 것을 쓴다. 따로
    /// 고르면 화면에는 이쪽을 잡았다고 표시해 놓고 탄은 저쪽으로 휘는 일이 생기는데,
    /// 어디를 쏘고 있는지 알 수 없게 만드는 가장 빠른 길이다.
    /// </para>
    /// <para>
    /// 다만 잡혔다고 다 거들지는 않는다. 잡히는 범위는 화면 전체지만 거드는 범위는
    /// 조준점 바로 옆 몇 도뿐이다 — 화면 구석의 적에게 탄이 휘면 그건 거드는 것이
    /// 아니라 빼앗는 것이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoftLock : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRoot _root;

        [Tooltip("표적을 잡는 곳. 비워두면 기체에서 찾는다.")]
        [SerializeField] private LockOnTargeting _targeting;

        [Header("거드는 범위")]
        [Tooltip("겨눈 방향에서 이 각도 안에 있어야 거든다.\n\n" +
                 "좁게 둘 것. 넓히면 겨누지도 않은 쪽으로 탄이 휘어서,\n" +
                 "조준점이 어디를 가리키는지 믿을 수 없게 된다.")]
        [Range(0f, 30f)]
        [SerializeField] private float _cone = 7f;

        [Tooltip("이 거리 안의 표적만 거든다 (m). 기총 사거리에 맞춘다.")]
        [Min(1f)]
        [SerializeField] private float _range = 300f;

        [Header("거드는 정도")]
        [Tooltip("겨눔이 빗나간 각의 몇 할을 메워줄지. 1이면 표적을 향해 완전히 돌린다.\n\n" +
                 "리드와 낙차는 이 값과 무관하게 언제나 온전히 메운다 — 여기서 정하는\n" +
                 "것은 '적을 조준점에 두는 일'을 얼마나 거들지다.\n\n" +
                 "1로 두지 말 것. 방아쇠가 곧 명중이 되면 맞히는 재미가 사라진다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _assist = 0.8f;

        [Tooltip("겨눔을 메워주는 각도의 상한.\n\n" +
                 "원뿔 가장자리의 적에게 탄이 홱 휘는 것을 막는다. 이것이 없으면\n" +
                 "거드는 정도가 거리와 각도에 따라 널뛰어 손에 익지 않는다.\n\n" +
                 "리드를 자르지 않으므로 작게 두어도 된다. 리드까지 이 값으로 묶으면\n" +
                 "빠른 표적일수록 보정이 모자라 오히려 못 맞힌다.")]
        [Range(0f, 15f)]
        [SerializeField] private float _maxBend = 4f;

        /// <summary>지금 거들 수 있는 표적. 없으면 null. 화면 표시가 읽어 간다.</summary>
        public Transform Target { get; private set; }

        private void Awake()
        {
            _root = AircraftRoot.Resolve(this, _root);

            if (_targeting == null && _root != null)
            {
                _targeting = _root.Find<LockOnTargeting>();
            }

            if (_targeting == null)
            {
                Debug.LogError($"{nameof(SoftLock)}: 표적을 잡을 컴포넌트를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 쏠 방향을 정한다. 거들 표적이 없으면 겨눈 그대로 돌려준다.
        /// <para>
        /// 두 가지를 나눠 다룬다. <b>리드와 낙차</b>는 온전히 메우고, <b>겨눔 오차</b>는
        /// 정해둔 몫만큼만 메운다.
        /// </para>
        /// <para>
        /// 나누지 않고 한꺼번에 상한으로 묶으면 소용이 없다. 100m에서 상대속도 20m/s로
        /// 가로지르는 표적의 리드각만 9도쯤인데, 상한이 4도면 절반도 못 메우고 잘려서
        /// 거들어준 것이 오히려 어중간하게 빗나간다. 그러면 빠른 표적일수록 보정이
        /// 모자라 못 맞히게 되는데, 정확히 거들어야 할 상황에서 손을 놓는 셈이다.
        /// </para>
        /// <para>
        /// 애초에 이 둘은 성격이 다르다. 탄속과 중력을 셈하는 것은 사람이 할 일이
        /// 아니라 게임이 대신해야 할 계산이고, 적을 조준점에 두는 것은 플레이어의
        /// 몫이다. 앞은 다 해주고 뒤는 거들기만 한다.
        /// </para>
        /// </summary>
        /// <param name="origin">탄이 나가는 자리.</param>
        /// <param name="aim">겨눈 방향.</param>
        /// <param name="gun">쏘는 기총. 탄속과 낙차를 여기서 읽는다.</param>
        public Vector3 Adjust(Vector3 origin, Vector3 aim, GunDefinition gun)
        {
            Target = null;

            if (gun == null || _targeting == null || !_targeting.HasLock)
            {
                return aim;
            }

            Vector3 point = _targeting.TargetPoint;
            Vector3 toTarget = point - origin;

            if (toTarget.sqrMagnitude > _range * _range
                || toTarget.sqrMagnitude < 0.0001f
                || Vector3.Angle(aim, toTarget) > _cone)
            {
                return aim;
            }

            Target = _targeting.Target;

            Vector3 toShot = ShotPoint(point, origin, gun) - origin;
            if (toShot.sqrMagnitude < 0.0001f)
            {
                return aim;
            }

            // 겨눔 오차를 정해둔 몫만큼 메운다.
            float bend = Mathf.Min(Vector3.Angle(aim, toTarget) * _assist, _maxBend);
            Vector3 aimed = bend > 0.0001f
                ? Vector3.RotateTowards(aim.normalized, toTarget.normalized, bend * Mathf.Deg2Rad, 0f)
                : aim.normalized;

            // 리드와 낙차는 그 위에 통째로 얹는다. 표적을 겨눈 방향과 실제로 쏴야 할
            // 방향의 차이를, 플레이어가 겨눈 쪽에 그대로 옮겨 붙이는 것이다.
            return Quaternion.FromToRotation(toTarget, toShot) * aimed;
        }

        /// <summary>
        /// 실제로 겨눠야 할 자리. 표적이 도망간 만큼 앞을 보고, 탄이 떨어질 만큼 위를 본다.
        /// <para>
        /// 셈은 기체를 기준으로 한다. 탄에는 기체 속도가 얹혀 나가므로, 기체에서 보면
        /// 탄은 정확히 총구 속도로 날아가고 표적은 상대 속도로 움직인다. 세계를 기준으로
        /// 풀면 탄의 실제 속도가 쏘는 방향에 따라 달라져서, 방향을 구하려고 방향이
        /// 필요해지는 셈이 된다.
        /// </para>
        /// <para>
        /// 낙차를 빼놓으면 탄이 늘 표적 아래로 지난다. 탄속 120에 낙차 비율 0.3이면
        /// 100m에서 1m가 떨어지는데, 겨눔을 아무리 거들어줘도 그만큼은 어김없이 빗나가
        /// 거드는 것이 통하지 않는 것처럼 보인다.
        /// </para>
        /// </summary>
        private Vector3 ShotPoint(Vector3 point, Vector3 origin, GunDefinition gun)
        {
            Vector3 carrier = _root != null && _root.Body != null
                ? _root.Body.linearVelocity
                : Vector3.zero;

            Transform target = _targeting.Target;
            Rigidbody body = target != null ? target.GetComponentInParent<Rigidbody>() : null;
            Vector3 relative = body != null ? body.linearVelocity - carrier : Vector3.zero;

            float time = TimeToIntercept(point - origin, relative, gun.MuzzleVelocity);
            if (time <= 0f)
            {
                return point;
            }

            // 떨어지는 만큼 위를 겨눈다. 중력은 아래를 향하므로 빼면 위로 올라간다.
            Vector3 drop = 0.5f * gun.GravityScale * time * time * Physics.gravity;

            return point + (relative * time) - drop;
        }

        /// <summary>
        /// 탄이 표적을 따라잡는 데 걸리는 시간. 못 따라잡으면 0.
        /// <para>
        /// |offset + v·t| = speed·t 를 t에 대해 푼다. 정리하면
        /// (v·v − s²)t² + 2(offset·v)t + offset·offset = 0 인 이차식이고,
        /// 양수인 해 중 작은 쪽이 먼저 닿는 순간이다.
        /// </para>
        /// </summary>
        private static float TimeToIntercept(Vector3 offset, Vector3 velocity, float speed)
        {
            float a = velocity.sqrMagnitude - (speed * speed);
            float b = 2f * Vector3.Dot(offset, velocity);
            float c = offset.sqrMagnitude;

            // 표적이 탄과 같은 속도로 달아나면 이차항이 사라진다. 이때는 일차식이다.
            if (Mathf.Abs(a) < 0.0001f)
            {
                return Mathf.Abs(b) < 0.0001f ? 0f : -c / b;
            }

            float discriminant = (b * b) - (4f * a * c);
            if (discriminant < 0f)
            {
                return 0f;
            }

            float root = Mathf.Sqrt(discriminant);
            float first = (-b - root) / (2f * a);
            float second = (-b + root) / (2f * a);

            if (first > 0f && second > 0f)
            {
                return Mathf.Min(first, second);
            }

            return Mathf.Max(0f, Mathf.Max(first, second));
        }
    }
}
