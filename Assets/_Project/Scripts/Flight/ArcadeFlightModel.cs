using System;
using Adler.Aircraft;
using Adler.Core;
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

        private float _speed;
        private bool _boosting;

        // 실제로 나아가는 방향과 빠르기. 기수를 뒤따라가므로 둘이 어긋날 수 있다.
        private Vector3 _velocity;
        private bool _frozen;

        // 세상에 견준 이번 스텝의 시간 배율. 내놓는 속도에 곱해 이 기체만 늦춘다.
        private float _relative = 1f;

        // 이번 스텝에 걸린 외부 견인. 쓰고 나면 비운다.
        private Tether _tether = Tether.None;

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
        /// <summary>
        /// 최저에서 부스터 속도 사이의 어디쯤인지. 게이지가 이 값을 그대로 채운다.
        /// <para>
        /// 스로틀 자리를 이것이 대신한다. 레버 위치가 아니라 실제 속도라서, 상승하다
        /// 느려지거나 급강하로 붙는 것까지 게이지에 나타난다.
        /// </para>
        /// </summary>
        public float SpeedNormalized =>
            Mathf.InverseLerp(_stats.MinSpeed, TopSpeed, _speed);
        public bool IsBoosting => _boosting;
        public bool IsFrozen => _frozen;

        /// <inheritdoc />
        public void SetTether(in Tether tether) => _tether = tether;

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
            // 물리에서 읽어올 때는 곱해뒀던 배율을 덜어낸다. 그러지 않으면 늦춰진 채
            // 녹은 기체가 자기 속도를 실제보다 느리게 기억한다.
            Vector3 carried = _body.linearVelocity / Mathf.Max(0.01f, _relative);

            _speed = Mathf.Clamp(carried.magnitude, _stats.MinSpeed, TopSpeed);

            // 떨어지던 방향도 이어받는다. 여기서 맞추지 않으면 얼기 직전의 진행 방향으로
            // 되돌아가면서, 추락하다 갑자기 옆으로 튄다.
            _velocity = carried;
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

            _speed = TargetSpeedFor(boosting: false);
            _velocity = _body.transform.forward * _speed;
        }

        public void Tick(in FlightInput input, Clock clock)
        {
            float deltaTime = clock.FixedDelta;
            _relative = clock.Relative;

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
            ApplyVelocity(deltaTime);

            // 견인은 넣어준 스텝에만 듣는다. 끊긴 뒤에도 힘이 남아 있으면 줄을 놓고도
            // 한동안 끌려간다.
            _tether = Tether.None;
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

        /// <summary>
        /// 지금 도달하려는 속도.
        /// <para>
        /// 순항 아니면 부스터, 둘뿐이다. 스로틀 레버를 없앤 이유는 그것이 관리할 것을
        /// 하나 늘리면서 정작 판단은 만들지 않았기 때문이다 — 답이 언제나 "끝까지 밀기"라
        /// 선택이 아니었고, 레버가 움직이는 동안의 지연만 남았다.
        /// </para>
        /// <para>
        /// 빨라지는 수단이 부스터 하나뿐이면 연료를 언제 쓸지가 유일한 속도 판단이 된다.
        /// </para>
        /// </summary>
        private float TargetSpeedFor(bool boosting)
            => boosting ? _stats.TopSpeed : _stats.CruiseSpeed;

        /// <summary>낼 수 있는 가장 빠른 속도. 게이지의 끝이자 얼었다 녹을 때의 상한이다.</summary>
        private float TopSpeed => _stats.TopSpeed;

        /// <summary>날것의 입력을 조종면 위치로 서서히 옮겨 기체에 무게감을 준다.</summary>
        private void SmoothControls(in FlightInput input, float deltaTime)
        {
            float pitchTarget = InvertPitch ? -input.Pitch : input.Pitch;
            float rate = _stats.ControlResponse * deltaTime;

            _pitch = Mathf.MoveTowards(_pitch, Mathf.Clamp(pitchTarget, -1f, 1f), rate);
            _roll = Mathf.MoveTowards(_roll, Mathf.Clamp(input.Roll, -1f, 1f), rate);
        }

        private void UpdateSpeed(in FlightInput input, float deltaTime)
        {
            bool wasBoosting = _boosting;
            _boosting = input.Boost;

            float target = _stats.CruiseSpeed;

            // 기수가 하늘을 향하면 속도가 깎이고 강하하면 붙는다. 공기역학은 아니지만
            // 상승과 급강하에 최소한의 대가와 보상을 붙여 비행이 밋밋해지지 않게 한다.
            float gravityInfluence = _stats.Airframe.GravityInfluence;
            if (gravityInfluence > 0f)
            {
                float climb = Vector3.Dot(_body.transform.forward, Vector3.up); // -1(강하) ~ +1(상승)

                // 순항에서 최저까지가 흔들릴 수 있는 폭이다. 영향을 1로 두면 수직 상승이
                // 정확히 최저 속도까지 떨어뜨린다.
                target -= climb * gravityInfluence * (_stats.CruiseSpeed - _stats.MinSpeed);
            }

            target = Mathf.Max(target, _stats.MinSpeed);

            // 곱한다. 깎이거나 붙은 뒤의 속도에 곱하므로, 상승 중에 밟으면 그만큼
            // 덜 나가고 강하 중에 밟으면 더 나간다 — 언제 밟느냐가 결과를 바꾼다.
            if (_boosting)
            {
                target *= _stats.Airframe.BoostMultiplier;
            }

            // 끌려가는 동안은 아무리 느려도 이만큼은 낸다. 잡아둔 상대의 속도에서
            // 계산해 넘어오므로, 상대가 부스터를 켜도 함께 빨라진다.
            target = Mathf.Max(target, _tether.SpeedFloor);

            // 밟는 순간에는 기다리지 않는다. 부스터는 유일한 가속 수단이라 눌렀는데
            // 잠시 뒤에 빨라지면, 밟은 것이 통했는지를 소리와 화면으로만 짐작하게 된다.
            if (_boosting && !wasBoosting)
            {
                _speed = target;
                return;
            }

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

            // 기체가 기울어진 만큼 저절로 그쪽으로 돈다. 러더가 없는 이유가 이것이다 —
            // 기울여서 도는 것으로 선회가 완결되므로, 방향을 따로 트는 조작은 키를 둘
            // 더 쓰면서 아무것도 새로 할 수 있게 해주지 않았다.
            float bank = Vector3.Dot(_body.transform.right, Vector3.up); // 오른쪽으로 기울면 음수
            float bankTurn = -bank * _stats.BankTurnRate;

            // Unity에서 +X 회전은 기수를 아래로, +Z 회전은 왼쪽으로 기울인다.
            // FlightInput의 부호 규약(+ = 위/오른쪽)에 맞추려면 뒤집어야 한다.
            Vector3 localAngular = new Vector3(
                -_pitch * _stats.PitchRate * agility,
                bankTurn,
                -_roll * _stats.RollRate * agility) * Mathf.Deg2Rad;

            // 회전을 직접 써넣지 않고 각속도로 넘긴다.
            // MoveRotation과 rotation 대입은 비-kinematic 바디에서 보간을 건너뛰기 때문에,
            // 물리 주기(50Hz)와 렌더 주기가 어긋나 선회할 때 화면이 떨린다.
            // 각속도로 주면 물리 엔진이 적분하고 Interpolate가 그 사이를 메운다.
            Vector3 world = _body.transform.TransformDirection(localAngular);

            // 물리는 세상 시간으로 적분되므로, 이 기체만 늦추려면 내놓는 각속도 자체를
            // 줄여야 한다. 흐른 양만 줄이면 붙는 속도만 굼떠지고 선회율은 그대로다.
            _body.angularVelocity = ApplyAimAssist(world) * _relative;
        }

        /// <summary>
        /// 견인이 걸린 만큼 기수를 표적 쪽으로 끌어준다.
        /// <para>
        /// 조종간이 만든 회전을 지우지 않고 그쪽으로 섞는다. 통째로 갈아치우면 겨누는
        /// 일에 실력이 필요 없어지고, 조종간이 먹지 않는 순간이 생겨 고장으로 느껴진다.
        /// </para>
        /// <para>
        /// 끌어당기는 속도는 기체 자신의 선회율과 무관하다. 줄에 매달린 것이므로 혼자
        /// 낼 수 없는 속도로 홱 돌아가는 것이 맞고, 기체 성능으로 막아두면 상대가
        /// 급기동하는 순간 기수가 뒤처져 겨눌 수가 없다.
        /// </para>
        /// </summary>
        private Vector3 ApplyAimAssist(Vector3 world)
        {
            if (!_tether.AssistsAim)
            {
                return world;
            }

            Vector3 forward = _body.transform.forward;
            Vector3 axis = Vector3.Cross(forward, _tether.Direction);

            if (axis.sqrMagnitude < 1e-6f)
            {
                return world;
            }

            float angle = Vector3.Angle(forward, _tether.Direction) * Mathf.Deg2Rad;
            float maxRate = _tether.TurnRate * Mathf.Deg2Rad;

            Vector3 assist = axis.normalized * Mathf.Min(angle * 6f, maxRate);

            return Vector3.Lerp(world, assist, _tether.AimAssist);
        }

        /// <summary>
        /// 진행 방향이 기수를 뒤따라간다.
        /// <para>
        /// 기수 방향을 그대로 속도로 쓰면 기체가 레일 위를 달리는 것처럼 느껴진다.
        /// 정확하지만 무게가 없고, 아무리 급하게 꺾어도 몸이 따라가는 느낌이 나지 않는다.
        /// </para>
        /// <para>
        /// 그래서 목표를 향해 옮겨가게만 한다. 선회하는 동안 기수는 안쪽을 보는데 몸은
        /// 아직 가던 쪽으로 밀리고, 그 어긋남이 곧 관성으로 읽힌다. 밀리는 만큼 실제
        /// 속도도 줄어들어서, 무리하게 꺾으면 느려지는 대가까지 저절로 생긴다.
        /// </para>
        /// </summary>
        private void ApplyVelocity(float deltaTime)
        {
            Vector3 heading = _body.transform.forward;

            // 밖에서 끌어당기면 나아가는 방향이 그쪽으로 휜다. 기수는 그대로라
            // 기체가 비스듬히 미끄러지는데, 줄에 매달려 끌려가는 모습이 그렇다.
            if (_tether.BendsPath)
            {
                heading = Vector3.Slerp(heading, _tether.Direction, _tether.PathBend);
            }

            Vector3 desired = heading * _speed;
            float grip = _stats.Airframe.Grip;

            _velocity = grip > 0f
                ? Vector3.Lerp(_velocity, desired, 1f - Mathf.Exp(-grip * deltaTime))
                : desired;

            // 내부 상태(_velocity)는 배율을 먹이지 않는다. 그러면 다음 스텝에 또 곱해져
            // 배율이 거듭제곱으로 쌓인다. 물리에 넘기는 그 순간에만 곱한다.
            _body.linearVelocity = _velocity * _relative;
        }
    }
}
