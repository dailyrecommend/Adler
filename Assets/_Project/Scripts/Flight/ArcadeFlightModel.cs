using System;
using Adler.Aircraft;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 기체가 조종간을 그대로 따라가는 아케이드 비행 모델.
    /// <para>
    /// 공기역학을 계산하지 않는다. 입력을 회전 속도로, 스로틀을 속도로 직접 바꾸고
    /// 기체는 언제나 자신이 향한 방향으로 나아간다. 실속도 스핀도 없다.
    /// 그 결과 조종감이 스탯 몇 개로 완전히 통제되며, 정비로 부품을 바꿨을 때
    /// 성능 변화가 플레이어에게 그대로 전달된다.
    /// </para>
    /// <para>
    /// 수치를 <see cref="AircraftStatSheet"/>에서 매 스텝 읽으므로, 비행 중에
    /// 부품이 교체되거나 일시 효과가 붙어도 다음 스텝부터 곧바로 반영된다.
    /// </para>
    /// <para>
    /// 회전과 속도를 매 스텝 직접 지정하므로 충돌로 인한 튕김이나 회전은 무시된다.
    /// 아케이드 비행 게임에서 지형 충돌은 대개 격추 처리이므로 의도된 동작이다.
    /// </para>
    /// </summary>
    public sealed class ArcadeFlightModel : IFlightModel
    {
        private readonly AircraftStatSheet _stats;
        private Rigidbody _body;

        // 조종면 입력을 보간한 값. 스틱을 튕겨도 기체가 즉시 꺾이지 않게 한다.
        private float _pitch;
        private float _roll;
        private float _yaw;

        private float _throttle = 0.5f;
        private float _speed;
        private bool _boosting;
        private bool _frozen;

        // 얼어붙은 동안의 물리 저항. 앞으로 가던 힘이 빠져야 기수가 아래로 넘어간다.
        private const float FrozenLinearDamping = 0.2f;
        private const float FrozenAngularDamping = 0.8f;

        // 기수가 떨어지는 쪽으로 돌아가는 정도.
        private const float FrozenAlignStrength = 1.6f;
        private const float FrozenAlignRate = 2.5f;

        // 0이면 나아가던 방향을, 1이면 곧장 아래를 향한다. 그 사이에서 타협한다.
        private const float FrozenNoseDownBias = 0.6f;

        public ArcadeFlightModel(AircraftStatSheet stats)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        }

        public float Speed => _speed;
        public float ThrottleNormalized => _throttle;
        public bool IsBoosting => _boosting;
        public bool IsFrozen => _frozen;

        /// <summary>
        /// 조종과 추력을 끊거나 되돌린다.
        /// <para>
        /// 이 모델은 평소에 중력을 꺼두고 속도와 회전을 매 스텝 직접 써넣는다. 그래서
        /// 얼어붙게 만드는 일은 무언가를 더하는 것이 아니라 <b>손을 놓는 것</b>이다.
        /// 쓰기를 멈추고 중력을 켜면 그때부터는 물리 엔진이 알아서 떨어뜨린다.
        /// </para>
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            if (_frozen == frozen || _body == null)
            {
                return;
            }

            _frozen = frozen;

            if (frozen)
            {
                _body.useGravity = true;
                _body.linearDamping = FrozenLinearDamping;
                _body.angularDamping = FrozenAngularDamping;
                _boosting = false;
                return;
            }

            _body.useGravity = false;
            _body.linearDamping = 0f;
            _body.angularDamping = 0f;

            // 떨어지던 속도를 이어받는다. 얼기 전 값으로 돌아가면 녹는 순간 속도가
            // 툭 바뀌어, 되살아난 것이 아니라 순간이동한 것처럼 보인다.
            _speed = Mathf.Clamp(_body.linearVelocity.magnitude, _stats.MinSpeed, _stats.MaxSpeed);
        }

        /// <summary>스틱을 밀 때 기수가 올라가게 한다. 기체 성능이 아니라 플레이어 취향이다.</summary>
        public bool InvertPitch { get; set; }

        /// <summary>
        /// 물리 설정을 맞추고 조종 상태를 처음으로 되돌린다.
        /// <para>
        /// 리스폰 때 다시 불린다. 속도만 되돌리고 스로틀이나 조종면을 그대로 두면,
        /// 추락 직전에 당기고 있던 입력이 남은 채로 되살아난다.
        /// </para>
        /// </summary>
        public void Initialize(Rigidbody body)
        {
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));

            _pitch = 0f;
            _roll = 0f;
            _yaw = 0f;
            _throttle = 0.5f;
            _boosting = false;

            // 얼어붙은 채로 격추됐을 수 있다. 여기서 풀지 않으면 재출격한 기체가
            // 중력만 받은 채 조종되지 않는다.
            _frozen = false;

            // 이 모델이 속도와 회전을 전적으로 관리하므로 물리 엔진의 개입을 끈다.
            _body.useGravity = false;
            _body.linearDamping = 0f;
            _body.angularDamping = 0f;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 기본 상한은 7rad/s(약 400도/초)라 부품으로 롤 속도를 올리면 조용히 잘린다.
            // 스탯 상한(720도/초)을 넘도록 열어두고, 제한은 스탯 쪽에서만 건다.
            _body.maxAngularVelocity = 4f * Mathf.PI;

            _speed = TargetSpeedFor(_throttle, boosting: false);
        }

        public void Tick(in FlightInput input, float deltaTime)
        {
            if (_body == null)
            {
                return;
            }

            if (_frozen)
            {
                // 조종간을 서서히 놓는다. 잡고 있던 값이 그대로 남으면 녹는 순간
                // 그쪽으로 홱 꺾여서, 되살아난 것이 아니라 튕겨나간 것으로 보인다.
                FlightInput idle = default;
                SmoothControls(in idle, deltaTime);

                _boosting = false;
                FallNoseFirst(deltaTime);
                return;
            }

            SmoothControls(input, deltaTime);
            UpdateSpeed(input, deltaTime);
            ApplyRotation(deltaTime);
            ApplyVelocity();
        }

        /// <summary>
        /// 얼어붙은 기체의 기수를 떨어지는 쪽으로 돌린다.
        /// <para>
        /// 자세를 그대로 두면 수평으로 날던 모습 그대로 가라앉아, 조종을 잃은 것이 아니라
        /// 게임이 멈춘 것처럼 보인다. 기수가 넘어가야 죽은 무게가 떨어지는 것으로 읽힌다.
        /// </para>
        /// <para>
        /// 곧장 아래를 향하게 하지 않고 나아가던 방향과 타협한다. 얼자마자 수직으로 꺾이면
        /// 관성이 없는 것처럼 보이는데, 앞으로 가던 힘은 남아 있어야 맞다.
        /// </para>
        /// <para>
        /// 회전은 각속도로 넘긴다. 자세를 직접 써넣으면 비-kinematic 바디에서 보간이
        /// 끊겨, 떨어지는 내내 화면이 떨린다.
        /// </para>
        /// </summary>
        private void FallNoseFirst(float deltaTime)
        {
            Vector3 velocity = _body.linearVelocity;

            // 거의 멈춰 있으면 어느 쪽이 앞인지 기준이 없다.
            if (velocity.sqrMagnitude < 1f)
            {
                return;
            }

            Vector3 desired = Vector3.Slerp(velocity.normalized, Vector3.down, FrozenNoseDownBias);
            Vector3 forward = _body.transform.forward;
            Vector3 axis = Vector3.Cross(forward, desired);

            // 정확히 반대를 보고 있으면 축이 0이 되어 어느 쪽으로도 돌지 못한다.
            // 아무 축이나 잡아주면 그 다음 스텝부터는 제대로 풀린다.
            if (axis.sqrMagnitude < 1e-6f)
            {
                axis = _body.transform.up;
            }

            float angle = Vector3.Angle(forward, desired) * Mathf.Deg2Rad;
            Vector3 target = axis.normalized * (angle * FrozenAlignStrength);

            // 곧바로 대입하지 않고 옮겨간다. 얼기 직전의 회전이 한 프레임에 사라지면
            // 그 순간이 툭 끊겨 보인다.
            _body.angularVelocity = Vector3.Lerp(
                _body.angularVelocity, target, 1f - Mathf.Exp(-FrozenAlignRate * deltaTime));
        }

        private float TargetSpeedFor(float throttleNormalized, bool boosting)
        {
            return boosting
                ? _stats.BoostSpeed
                : Mathf.Lerp(_stats.MinSpeed, _stats.MaxSpeed, throttleNormalized);
        }

        /// <summary>날것의 입력을 조종면 위치로 서서히 옮겨 기체에 무게감을 준다.</summary>
        private void SmoothControls(in FlightInput input, float deltaTime)
        {
            float pitchTarget = InvertPitch ? -input.Pitch : input.Pitch;
            float rate = _stats.ControlResponse * deltaTime;

            _pitch = Mathf.MoveTowards(_pitch, Mathf.Clamp(pitchTarget, -1f, 1f), rate);
            _roll = Mathf.MoveTowards(_roll, Mathf.Clamp(input.Roll, -1f, 1f), rate);
            _yaw = Mathf.MoveTowards(_yaw, Mathf.Clamp(input.Yaw, -1f, 1f), rate);
        }

        private void UpdateSpeed(in FlightInput input, float deltaTime)
        {
            _boosting = input.Boost;

            // 스로틀은 즉시 값이 아니라 레버다. 밀고 있는 동안 서서히 올라간다.
            _throttle = Mathf.Clamp01(_throttle + (input.Throttle * _stats.ThrottleResponse * deltaTime));

            float target = TargetSpeedFor(_throttle, input.Boost);

            // 기수가 하늘을 향하면 속도가 깎이고 강하하면 붙는다. 공기역학은 아니지만
            // 상승과 급강하에 최소한의 대가와 보상을 붙여 비행이 밋밋해지지 않게 한다.
            float gravityInfluence = _stats.Airframe.GravityInfluence;
            if (gravityInfluence > 0f)
            {
                float climb = Vector3.Dot(_body.transform.forward, Vector3.up); // -1(강하) ~ +1(상승)
                target -= climb * gravityInfluence * (_stats.MaxSpeed - _stats.MinSpeed) * 0.5f;
            }

            target = Mathf.Clamp(target, _stats.MinSpeed, _stats.BoostSpeed);

            float accel = target > _speed ? _stats.Acceleration : _stats.Deceleration;
            _speed = Mathf.MoveTowards(_speed, target, accel * deltaTime);
        }

        private void ApplyRotation(float deltaTime)
        {
            // 느리게 날수록 둔해진다. 실속 대신 이것이 저속의 유일한 대가다.
            float agility = Mathf.Lerp(
                _stats.LowSpeedAgility,
                1f,
                Mathf.InverseLerp(_stats.MinSpeed, _stats.CruiseSpeed, _speed));

            // 기체가 기울어진 만큼 저절로 그쪽으로 돈다. 러더를 몰라도 선회가 되는
            // 이 보정이 아케이드 조작감을 만든다.
            float bank = Vector3.Dot(_body.transform.right, Vector3.up); // 오른쪽으로 기울면 음수
            float bankTurn = -bank * _stats.BankTurnRate;

            // Unity에서 +X 회전은 기수를 아래로, +Z 회전은 왼쪽으로 기울인다.
            // FlightInput의 부호 규약(+ = 위/오른쪽)에 맞추려면 뒤집어야 한다.
            Vector3 localAngular = new Vector3(
                -_pitch * _stats.PitchRate * agility,
                (_yaw * _stats.YawRate * agility) + bankTurn,
                -_roll * _stats.RollRate * agility) * Mathf.Deg2Rad;

            // 회전을 직접 써넣지 않고 각속도로 넘긴다.
            // MoveRotation과 rotation 대입은 비-kinematic 바디에서 보간을 건너뛰기 때문에,
            // 물리 주기(50Hz)와 렌더 주기가 어긋나 선회할 때 화면이 떨린다.
            // 각속도로 주면 물리 엔진이 적분하고 Interpolate가 그 사이를 메운다.
            _body.angularVelocity = _body.transform.TransformDirection(localAngular);
        }

        /// <summary>기체는 언제나 자신이 향한 방향으로 나아간다. 관성도 미끄러짐도 없다.</summary>
        private void ApplyVelocity()
        {
            _body.linearVelocity = _body.transform.forward * _speed;
        }
    }
}
