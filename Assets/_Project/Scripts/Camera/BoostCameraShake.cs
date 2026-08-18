using Adler.Flight;
using Unity.Cinemachine;
using UnityEngine;

namespace Adler.CameraRig
{
    /// <summary>
    /// 부스터를 쓰는 동안 카메라를 잘게 떨어 추력이 걸린 느낌을 준다.
    /// <para>
    /// 카메라를 직접 흔들지 않고 Cinemachine의 노이즈 세기만 조절한다. 위치를 직접
    /// 건드리면 추적과 감쇠 로직과 매 프레임 싸우게 되고, 화면이 떨리는 것과 카메라가
    /// 목표를 놓치는 것을 구분할 수 없게 된다.
    /// </para>
    /// <para>
    /// 세기를 껐다 켜지 않고 서서히 올리고 내리는 이유는, 부스터를 짧게 끊어 쓸 때
    /// 화면이 툭툭 끊기며 흔들리면 오히려 고장 난 것처럼 보이기 때문이다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CinemachineBasicMultiChannelPerlin))]
    [DisallowMultipleComponent]
    public sealed class BoostCameraShake : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("세기")]
        [Tooltip("평상시 흔들림. 0이면 부스터를 쓸 때만 흔들린다.")]
        [Min(0f)]
        [SerializeField] private float _idleAmplitude;

        [Tooltip("부스터를 쓸 때의 흔들림 크기.")]
        [Min(0f)]
        [SerializeField] private float _boostAmplitude = 0.6f;

        [Tooltip("부스터를 쓸 때의 흔들림 속도. 클수록 잘게 떤다.")]
        [Min(0f)]
        [SerializeField] private float _boostFrequency = 1.4f;

        [Header("전환")]
        [Tooltip("흔들림이 올라오는 속도. 클수록 부스터를 켜자마자 흔들린다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampUpSpeed = 8f;

        [Tooltip("흔들림이 가라앉는 속도. 올라올 때보다 느려야 여운이 남는다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampDownSpeed = 3f;

        private CinemachineBasicMultiChannelPerlin _noise;
        private float _amplitude;
        private float _frequency;

        private void Awake()
        {
            _noise = GetComponent<CinemachineBasicMultiChannelPerlin>();

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(BoostCameraShake)}: Aircraft가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_noise.NoiseProfile == null)
            {
                Debug.LogError(
                    $"{nameof(BoostCameraShake)}: Noise Profile이 비어 있어 흔들림이 나오지 않습니다. " +
                    "Cinemachine이 제공하는 6D Shake 같은 프로파일을 넣으세요.", this);
            }

            _amplitude = _idleAmplitude;
            _frequency = _boostFrequency;
        }

        private void Update()
        {
            IFlightModel model = _aircraft.Model;
            if (model == null)
            {
                return;
            }

            bool boosting = model.IsBoosting;

            float targetAmplitude = boosting ? _boostAmplitude : _idleAmplitude;
            float speed = boosting ? _rampUpSpeed : _rampDownSpeed;
            float t = 1f - Mathf.Exp(-speed * Time.deltaTime);

            _amplitude = Mathf.Lerp(_amplitude, targetAmplitude, t);
            _frequency = Mathf.Lerp(_frequency, _boostFrequency, t);

            _noise.AmplitudeGain = _amplitude;
            _noise.FrequencyGain = _frequency;
        }
    }
}
