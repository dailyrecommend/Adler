using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adler.CameraRig
{
    /// <summary>
    /// 정해둔 디버프가 걸려 있는 동안 Volume의 세기를 올린다.
    /// <para>
    /// 자리가 정해진 것이라면 이것이 필요 없다. Volume의 Is Global을 끄고 콜라이더를
    /// 붙이면 위치만으로 알아서 섞이므로, 시야 제한 구역 같은 것은 그쪽이 낫다.
    /// </para>
    /// <para>
    /// 동결처럼 <b>어디서든 걸리는 상태</b>에는 그 방법을 쓸 수 없다. 걸어둘 자리가
    /// 없으니 상태를 보고 직접 세기를 움직여야 한다.
    /// </para>
    /// <para>
    /// 상태마다 전용 컴포넌트를 만들지 않는다. 어느 디버프를 볼지만 지정하면 되므로
    /// 동결이든 나중에 붙일 화재든 이것 하나를 붙여 쓴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebuffPostFx : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("이 디버프가 걸려 있는 동안 효과가 걸린다.")]
        [SerializeField] private DebuffDefinition _debuff;

        [Tooltip("전용 Volume. 평소 Weight는 0으로 둘 것.")]
        [SerializeField] private Volume _volume;

        [Header("세기")]
        [Tooltip("걸려 있는 동안 도달할 Weight.")]
        [Range(0f, 1f)]
        [SerializeField] private float _activeWeight = 1f;

        [Tooltip("평상시 Weight.")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleWeight;

        [Header("전환")]
        [Tooltip("효과가 올라오는 속도.")]
        [Min(0.1f)]
        [SerializeField] private float _rampUpSpeed = 3f;

        [Tooltip("효과가 가라앉는 속도. 풀린 것은 빨리 알려주는 편이 낫다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampDownSpeed = 5f;

        private AircraftDebuffs _debuffs;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _debuffs = _aircraft != null ? _aircraft.Debuffs : null;

            if (_debuffs == null || _volume == null || _debuff == null)
            {
                Debug.LogError($"{nameof(DebuffPostFx)}: 디버프 목록, Volume, 정의 중 빠진 것이 있습니다.", this);
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
            bool active = _debuffs.IsActive(_debuff);

            float target = active ? _activeWeight : _idleWeight;
            float speed = active ? _rampUpSpeed : _rampDownSpeed;

            _volume.weight = Mathf.Lerp(
                _volume.weight, target, 1f - Mathf.Exp(-speed * _clock.Delta));
        }
    }
}
