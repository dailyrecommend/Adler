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
        [SerializeField] private AircraftController _aircraft;

        [Header("연출")]
        [Tooltip("최고 속도에서 기준 화각에 더해지는 각도.")]
        [SerializeField] private float _fieldOfViewGain = 18f;

        [Tooltip("이 속도에서 가산분이 최대가 된다 (m/s).\n" +
                 "0 이하로 두면 기체의 부스터 속도를 자동으로 따라간다 — 정비로 부스터 성능이 " +
                 "바뀌어도 연출이 어긋나지 않는다.")]
        [SerializeField] private float _maxSpeed;

        [Tooltip("클수록 화각 변화가 빠르게 따라붙는다.")]
        [SerializeField] private float _responsiveness = 3f;

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

            state.Lens.FieldOfView += extra.SmoothedGain;
        }

        private float ResolveMaxSpeed()
        {
            if (_maxSpeed > 0f)
            {
                return _maxSpeed;
            }

            return _aircraft.Stats != null ? _aircraft.Stats.BoostSpeed : 0f;
        }

        /// <summary>확장 하나가 여러 카메라에 붙을 수 있으므로 보간 상태는 카메라별로 갖는다.</summary>
        private class VcamExtraState : VcamExtraStateBase
        {
            public float SmoothedGain;
        }
    }
}
