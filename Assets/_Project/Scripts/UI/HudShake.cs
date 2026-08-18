using Adler.Flight;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 부스터를 쓰는 동안 HUD를 흔든다.
    /// <para>
    /// 카메라만 흔들면 세상이 흔들리는 것으로 보이지만, 계기까지 함께 떨리면 진동이
    /// 기체를 타고 조종석까지 전해지는 것으로 읽힌다. 화면 안에 흔들리지 않는 것이
    /// 하나도 없어야 온몸으로 받는 느낌이 난다.
    /// </para>
    /// <para>
    /// HUD 전체를 담은 오브젝트 하나에 붙인다. 요소를 따로따로 흔들면 서로 어긋나
    /// 진동이 아니라 화면이 깨진 것처럼 보인다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudShake : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("흔들 대상. 비워두면 이 오브젝트를 흔든다. HUD 전체를 담은 것으로 지정할 것.")]
        [SerializeField] private RectTransform _target;

        [Header("세기")]
        [Tooltip("평상시 흔들림 (픽셀). 0이면 부스터를 쓸 때만 흔들린다.")]
        [Min(0f)]
        [SerializeField] private float _idleAmplitude;

        [Tooltip("부스터를 쓸 때 흔들리는 폭 (픽셀).")]
        [Min(0f)]
        [SerializeField] private float _boostAmplitude = 9f;

        [Tooltip("부스터를 쓸 때 기울어지는 각도. 0이면 위치만 흔들린다.")]
        [Min(0f)]
        [SerializeField] private float _boostRotation = 0.6f;

        [Tooltip("떨림의 빠르기. 클수록 잘게 떤다.")]
        [Min(0.1f)]
        [SerializeField] private float _frequency = 14f;

        [Header("전환")]
        [Tooltip("흔들림이 올라오는 속도.")]
        [Min(0.1f)]
        [SerializeField] private float _rampUpSpeed = 8f;

        [Tooltip("흔들림이 가라앉는 속도. 올라올 때보다 느려야 여운이 남는다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampDownSpeed = 3f;

        private Vector2 _basePosition;
        private Quaternion _baseRotation;
        private float _amplitude;

        // 축마다 다른 자리에서 노이즈를 읽는다. 같은 값을 쓰면 대각선으로만 움직인다.
        private float _seedX;
        private float _seedY;
        private float _seedRoll;

        private void Awake()
        {
            if (_target == null)
            {
                _target = transform as RectTransform;
            }

            if (_aircraft == null || _target == null)
            {
                Debug.LogError($"{nameof(HudShake)}: Aircraft 또는 흔들 대상이 없습니다.", this);
                enabled = false;
                return;
            }

            _basePosition = _target.anchoredPosition;
            _baseRotation = _target.localRotation;

            _seedX = Random.value * 100f;
            _seedY = Random.value * 100f;
            _seedRoll = Random.value * 100f;

            _amplitude = _idleAmplitude;
        }

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            IFlightModel model = _aircraft.Model;
            if (model == null)
            {
                return;
            }

            bool boosting = model.IsBoosting;

            float target = boosting ? _boostAmplitude : _idleAmplitude;
            float speed = boosting ? _rampUpSpeed : _rampDownSpeed;
            _amplitude = Mathf.Lerp(_amplitude, target, 1f - Mathf.Exp(-speed * Time.deltaTime));

            if (_amplitude <= 0.01f)
            {
                Restore();
                return;
            }

            float t = Time.time * _frequency;

            // 무작위 값 대신 펄린 노이즈를 쓴다. 매 프레임 튀는 값은 진동이 아니라
            // 화면이 깜빡이는 것처럼 보인다.
            Vector2 offset = new Vector2(Sample(_seedX, t), Sample(_seedY, t)) * _amplitude;

            _target.anchoredPosition = _basePosition + offset;

            if (_boostRotation > 0f)
            {
                float roll = Sample(_seedRoll, t) * _boostRotation * (_amplitude / Mathf.Max(_boostAmplitude, 0.01f));
                _target.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, roll);
            }
        }

        /// <summary>-1에서 1 사이로 부드럽게 오가는 값.</summary>
        private static float Sample(float seed, float time)
        {
            return (Mathf.PerlinNoise(seed, time) - 0.5f) * 2f;
        }

        private void Restore()
        {
            if (_target == null)
            {
                return;
            }

            _target.anchoredPosition = _basePosition;
            _target.localRotation = _baseRotation;
        }
    }
}
