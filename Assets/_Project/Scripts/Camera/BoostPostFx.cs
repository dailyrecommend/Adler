using Adler.Core;
using Adler.Flight;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adler.CameraRig
{
    /// <summary>
    /// 부스터를 쓰는 동안 별도 Volume의 세기를 올린다.
    /// <para>
    /// 기존 화면 효과를 건드리지 않고 그 위에 얹는다. 부스터용 Volume을 따로 두면
    /// 평소 화면은 그대로 두고 얹을 것만 그 안에서 만들 수 있고, Weight를 1로 올려
    /// 눈으로 보면서 조정한 뒤 다시 0으로 내려두면 된다.
    /// </para>
    /// <para>
    /// 올라올 때와 가라앉을 때의 속도를 나눈 이유는 흔들림과 같다. 짧게 끊어 쓸 때
    /// 화면 효과가 툭툭 끊기면 연출이 아니라 결함으로 보인다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoostPostFx : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("부스터 전용 Volume. 평소 Weight는 0으로 둘 것.")]
        [SerializeField] private Volume _volume;

        [Header("세기")]
        [Tooltip("부스터를 쓸 때 도달할 Weight. 1이면 Volume에 담긴 효과가 온전히 걸린다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _boostWeight = 1f;

        [Tooltip("평상시 Weight. 0이면 부스터를 쓸 때만 보인다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleWeight;

        [Header("전환")]
        [Tooltip("효과가 올라오는 속도.")]
        [Min(0.1f)]
        [SerializeField] private float _rampUpSpeed = 9f;

        [Tooltip("효과가 가라앉는 속도. 올라올 때보다 느려야 여운이 남는다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampDownSpeed = 3.5f;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            if (_aircraft == null || _volume == null)
            {
                Debug.LogError($"{nameof(BoostPostFx)}: Aircraft 또는 Volume이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _volume.weight = _idleWeight;
        }

        private void OnDisable()
        {
            if (_volume != null)
            {
                _volume.weight = _idleWeight;
            }
        }

        private void Update()
        {
            IFlightModel model = _aircraft.Model;
            if (model == null)
            {
                return;
            }

            bool boosting = model.IsBoosting;
            float target = boosting ? _boostWeight : _idleWeight;
            float speed = boosting ? _rampUpSpeed : _rampDownSpeed;

            _volume.weight = Mathf.Lerp(
                _volume.weight, target, 1f - Mathf.Exp(-speed * _clock.Delta));
        }
    }
}
