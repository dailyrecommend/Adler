using Adler.Core;
using Adler.Flight;
using Adler.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace Adler.CameraRig
{
    /// <summary>
    /// 카메라 흔들림을 한곳에서 맡는다. 부스터의 이어지는 떨림과 피격의 순간적인 충격.
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
    public sealed class CameraShake : MonoBehaviour
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

        [Header("충격")]
        [Tooltip("피격 반응을 내보내는 쪽. 비워두면 기체 아래에서 찾는다.")]
        [SerializeField] private DamageFeedback _damage;

        [Tooltip("내구도를 한 번에 전부 잃을 만큼 맞았을 때의 흔들림 폭.\n" +
                 "실제로는 받은 피해가 최대 내구도에서 차지하는 비율에 비례한다.")]
        [Min(0f)]
        [SerializeField] private float _damageAmplitude = 2.5f;

        [Tooltip("피격 같은 순간적인 흔들림이 잦아드는 속도. 클수록 짧게 끝난다.")]
        [Min(0.1f)]
        [SerializeField] private float _impulseDecay = 6f;

        [Header("명중")]
        [Tooltip("명중과 격추 반응을 내보내는 쪽. 비워두면 기체 아래에서 찾는다.")]
        [SerializeField] private HitImpact _hitImpact;

        [Tooltip("기총이 맞았을 때의 흔들림 폭. 자주 일어나므로 작게 둔다.")]
        [Min(0f)]
        [SerializeField] private float _hitAmplitude = 0.35f;

        [Tooltip("폭발이 표적을 덮었을 때의 흔들림 폭.")]
        [Min(0f)]
        [SerializeField] private float _blastAmplitude = 1f;

        [Tooltip("격추했을 때의 흔들림 폭. 한 발 맞힌 것과는 확실히 구별돼야 한다.")]
        [Min(0f)]
        [SerializeField] private float _killAmplitude = 1.8f;

        private CinemachineBasicMultiChannelPerlin _noise;
        private float _amplitude;
        private float _frequency;
        private float _impulse;

        /// <summary>
        /// 한순간 흔든다. 피격처럼 갑자기 일어나는 것들이 부른다.
        /// <para>
        /// 흔들림을 내는 곳을 여기 하나로 모아둔 이유는, 카메라의 흔들림 값이 하나뿐이기
        /// 때문이다. 부스터와 피격이 각자 써넣으면 나중에 쓴 쪽이 앞의 것을 지워서,
        /// 부스터를 켠 채로 맞으면 피격이 묻히거나 그 반대가 된다.
        /// </para>
        /// <para>
        /// 더 큰 충격이 오면 갈아탄다. 더하면 연달아 맞을 때 끝없이 커져서 화면을
        /// 읽을 수 없게 된다.
        /// </para>
        /// </summary>
        public void AddImpulse(float amplitude)
            => _impulse = Mathf.Max(_impulse, amplitude);

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _noise = GetComponent<CinemachineBasicMultiChannelPerlin>();

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(CameraShake)}: Aircraft가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_noise.NoiseProfile == null)
            {
                Debug.LogError(
                    $"{nameof(CameraShake)}: Noise Profile이 비어 있어 흔들림이 나오지 않습니다. " +
                    "Cinemachine이 제공하는 6D Shake 같은 프로파일을 넣으세요.", this);
            }

            _amplitude = _idleAmplitude;
            _frequency = _boostFrequency;

            if (_damage == null)
            {
                _damage = _aircraft.GetComponentInChildren<DamageFeedback>(includeInactive: true);
            }

            if (_hitImpact == null)
            {
                _hitImpact = _aircraft.GetComponentInChildren<HitImpact>(includeInactive: true);
            }
        }

        /// <summary>
        /// 피격 반응을 지켜본다.
        /// <para>
        /// 맞은 쪽이 카메라를 흔드는 것이 아니라 카메라가 맞는 것을 지켜본다. 반대로
        /// 두면 반응을 하나 붙일 때마다 피격 쪽이 그것을 알아야 하고, 연출이 늘어날수록
        /// 그 목록이 길어진다.
        /// </para>
        /// </summary>
        private void OnEnable()
        {
            if (_damage != null)
            {
                _damage.Reacted += OnDamaged;
            }

            if (_hitImpact != null)
            {
                _hitImpact.Impact += OnImpact;
            }
        }

        private void OnDisable()
        {
            if (_damage != null)
            {
                _damage.Reacted -= OnDamaged;
            }

            if (_hitImpact != null)
            {
                _hitImpact.Impact -= OnImpact;
            }
        }

        private void OnDamaged(float strength) => AddImpulse(_damageAmplitude * strength);

        private void OnImpact(ImpactWeight weight) => AddImpulse(weight switch
        {
            ImpactWeight.Kill => _killAmplitude,
            ImpactWeight.Blast => _blastAmplitude,
            _ => _hitAmplitude,
        });

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
            float t = 1f - Mathf.Exp(-speed * _clock.Delta);

            _amplitude = Mathf.Lerp(_amplitude, targetAmplitude, t);
            _frequency = Mathf.Lerp(_frequency, _boostFrequency, t);

            _impulse = Mathf.Lerp(_impulse, 0f, 1f - Mathf.Exp(-_impulseDecay * _clock.Delta));

            // 이어지는 흔들림 위에 충격을 얹는다. 부스터 중에 맞으면 그만큼 더 흔들린다.
            _noise.AmplitudeGain = _amplitude + _impulse;
            _noise.FrequencyGain = _frequency;
        }
    }
}
