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

        public ArcadeFlightModel(AircraftStatSheet stats)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        }

        public float Speed => _speed;
        public float ThrottleNormalized => _throttle;
        public bool IsBoosting => _boosting;

        /// <summary>스틱을 밀 때 기수가 올라가게 한다. 기체 성능이 아니라 플레이어 취향이다.</summary>
        public bool InvertPitch { get; set; }

        public void Initialize(Rigidbody body)
        {
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));

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

            SmoothControls(input, deltaTime);
            UpdateSpeed(input, deltaTime);
            ApplyRotation(deltaTime);
            ApplyVelocity();
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
