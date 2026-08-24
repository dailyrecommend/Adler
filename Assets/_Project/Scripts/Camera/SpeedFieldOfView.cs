using Adler.Flight;
using Unity.Cinemachine;
using UnityEngine;

namespace Adler.CameraRig
{
    /// <summary>
    /// 속도가 붙을수록 화각을 넓혀 속도감을 만드는 Cinemachine 확장.
    /// <para>
    /// Cinemachine이 카메라의 위치와 회전을 전담하므로 이 스크립트는 렌즈만 건드린다.
    /// 확장으로 만든 이유는 실행 순서 때문이다. 일반 컴포넌트에서 LateUpdate에 렌즈를
    /// 고치면 CinemachineBrain과 순서가 보장되지 않아 한 프레임씩 어긋나거나 무시된다.
    /// </para>
    /// <para>
    /// 카메라에 설정된 화각을 덮어쓰지 않고 <em>더한다</em>. 기준 화각은 CinemachineCamera의
    /// Lens에서 정하고, 이 스크립트는 속도에 따른 가산분만 책임진다.
    /// </para>
    /// </summary>
    [AddComponentMenu("Adler/Camera/Speed Field Of View")]
    [SaveDuringPlay]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpeedFieldOfView : CinemachineExtension
    {
        [Header("추적 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("연출")]
        [Tooltip("최고 속도에서 기준 화각에 더해지는 각도.")]
        [SerializeField] private float _fieldOfViewGain = 18f;

        [Tooltip("기체가 낼 수 있는 가장 빠른 속도를 기준으로 삼는다.\n" +
                 "정비로 성능이 바뀌어도 연출이 따라가므로 대개 켜두면 된다.")]
        [SerializeField] private bool _useTopSpeedAsReference = true;

        [Tooltip("기준 속도를 직접 지정할 때 쓴다 (m/s). 위 항목을 끄면 이 값이 쓰인다.")]
        [SerializeField] private float _maxSpeed = 32f;

        [Header("부스터 시작")]
        [Tooltip("부스터를 막 켠 순간 카메라가 기체에서 밀려나는 거리 (m).\n\n" +
                 "빨라졌다는 느낌은 빠른 상태가 아니라 빨라지는 순간에서 온다.\n" +
                 "켜고 있는 내내 멀어져 있으면 그냥 다른 거리일 뿐이다.")]
        [Min(0f)]
        [SerializeField] private float _boostKickDistance = 2.5f;

        [Tooltip("그 순간 화각에 더해지는 각도.\n" +
                 "거리와 함께 튕겨야 한 번의 사건으로 읽힌다.")]
        [Min(0f)]
        [SerializeField] private float _boostKickFieldOfView = 8f;

        [Tooltip("튕긴 것이 돌아오는 속도. 클수록 짧게 끝난다.\n" +
                 "느리게 두면 부스터를 끊어 쓸 때 돌아오기 전에 또 튕겨 계속 멀어져 있게 된다.")]
        [Min(0.1f)]
        [SerializeField] private float _boostKickDecay = 3.5f;

        [Tooltip("클수록 화각 변화가 빠르게 따라붙는다.")]
        [SerializeField] private float _responsiveness = 3f;

        /// <summary>
        /// 설정이 잘못되면 이 확장은 아무 일도 하지 않는데, 화면만 봐서는 그 사실을 알 수 없다.
        /// 무엇이 빠졌는지 시작할 때 짚어준다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (!Application.isPlaying)
            {
                return;
            }

            if (GetComponent<CinemachineVirtualCameraBase>() == null)
            {
                Debug.LogError(
                    $"{nameof(SpeedFieldOfView)}: CinemachineCamera와 같은 오브젝트에 있어야 합니다. " +
                    "Brain이 붙은 Main Camera가 아니라 CinemachineCamera 쪽입니다.", this);
            }

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(SpeedFieldOfView)}: Aircraft가 비어 있어 속도를 읽을 수 없습니다.", this);
            }
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            // Body 단계에서 렌즈를 확정해야 이후 Aim 단계의 구도 계산이 같은 화각을 쓴다.
            if (stage != CinemachineCore.Stage.Body)
            {
                return;
            }

            if (_aircraft == null || _aircraft.Model == null)
            {
                return;
            }

            float reference = ResolveMaxSpeed();
            if (reference <= 0f)
            {
                return;
            }

            float target = _fieldOfViewGain * Mathf.Clamp01(_aircraft.Model.Speed / reference);

            VcamExtraState extra = GetExtraState<VcamExtraState>(vcam);
            if (deltaTime < 0f || !vcam.PreviousStateIsValid)
            {
                // 카메라가 잘리거나 새로 활성화된 프레임. 보간하면 화각이 튀므로 즉시 맞춘다.
                extra.SmoothedGain = target;
            }
            else
            {
                extra.SmoothedGain = Mathf.Lerp(
                    extra.SmoothedGain, target, 1f - Mathf.Exp(-_responsiveness * deltaTime));
            }

            ApplyBoostKick(vcam, extra, ref state, deltaTime);

            state.Lens.FieldOfView += extra.SmoothedGain + (_boostKickFieldOfView * extra.Kick);
        }

        /// <summary>
        /// 부스터가 켜지는 순간에만 카메라를 뒤로 튕긴다.
        /// <para>
        /// 켜져 있는 동안 내내 밀어두지 않는 이유는, 그러면 부스터 중의 거리가 그냥 다른
        /// 거리일 뿐이기 때문이다. 빨라졌다는 감각은 빠른 상태가 아니라 빨라지는 변화에서
        /// 오므로, 모서리에서 한 번 튕기고 곧 돌아와야 그 변화가 눈에 남는다.
        /// </para>
        /// <para>
        /// 미는 방향은 기체에서 카메라로 향하는 쪽이다. 카메라의 뒤쪽으로 밀면 아직 회전이
        /// 확정되지 않은 단계라 조준 계산 뒤에 어긋나고, 급선회하는 동안 엉뚱한 데로 밀린다.
        /// </para>
        /// </summary>
        private void ApplyBoostKick(
            CinemachineVirtualCameraBase vcam,
            VcamExtraState extra,
            ref CameraState state,
            float deltaTime)
        {
            bool boosting = _aircraft.Model.IsBoosting;

            // 켜지는 모서리에서만 채운다. 누르고 있는 동안 계속 채우면 돌아올 틈이 없다.
            if (boosting && !extra.WasBoosting)
            {
                extra.Kick = 1f;
            }

            extra.WasBoosting = boosting;

            // 잘리거나 새로 켜진 프레임에는 남은 튕김을 버린다. 그 상태로 보간하면
            // 카메라가 붙는 순간 뒤로 밀린 채 나타난다.
            if (deltaTime < 0f || !vcam.PreviousStateIsValid)
            {
                extra.Kick = 0f;
                return;
            }

            if (extra.Kick <= 0.001f)
            {
                extra.Kick = 0f;
                return;
            }

            extra.Kick = Mathf.Lerp(extra.Kick, 0f, 1f - Mathf.Exp(-_boostKickDecay * deltaTime));

            if (_boostKickDistance <= 0f || vcam.Follow == null)
            {
                return;
            }

            Vector3 away = state.RawPosition - vcam.Follow.position;

            if (away.sqrMagnitude > 0.0001f)
            {
                state.RawPosition += away.normalized * (_boostKickDistance * extra.Kick);
            }
        }

        private float ResolveMaxSpeed()
        {
            if (!_useTopSpeedAsReference && _maxSpeed > 0f)
            {
                return _maxSpeed;
            }

            // Stats는 AircraftController.Awake에서 만들어지므로 첫 프레임에는 비어 있을 수 있다.
            return _aircraft.Stats != null ? _aircraft.Stats.TopSpeed : 0f;
        }

        /// <summary>확장 하나가 여러 카메라에 붙을 수 있으므로 보간 상태는 카메라별로 갖는다.</summary>
        private class VcamExtraState : VcamExtraStateBase
        {
            public float SmoothedGain;
            public float Kick;
            public bool WasBoosting;
        }
    }
}
