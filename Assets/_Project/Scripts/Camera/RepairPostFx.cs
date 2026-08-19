using Adler.Flight;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adler.CameraRig
{
    /// <summary>
    /// 수리가 진행되는 동안 별도 Volume의 세기를 올린다.
    /// <para>
    /// 수리는 화면에 드러나는 것이 거의 없다. 체력 게이지가 조용히 차오를 뿐이라
    /// 지금 수리 중인지 잊기 쉬운데, 그 몇 초가 물러나 있어야 하는 시간이므로
    /// 상태로 보이는 편이 낫다.
    /// </para>
    /// <para>
    /// 부스터 쪽과 달리 가라앉는 속도를 느리게 잡아두면, 수리가 끝난 뒤에도 잠시
    /// 여운이 남아 "끝났다"가 읽힌다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RepairPostFx : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("수리 전용 Volume. 평소 Weight는 0으로 둘 것.")]
        [SerializeField] private Volume _volume;

        [Header("세기")]
        [Tooltip("수리 중에 도달할 Weight. 1이면 Volume에 담긴 효과가 온전히 걸린다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _repairWeight = 1f;

        [Tooltip("평상시 Weight. 0이면 수리 중에만 보인다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleWeight;

        [Header("전환")]
        [Tooltip("효과가 올라오는 속도.")]
        [Min(0.1f)]
        [SerializeField] private float _rampUpSpeed = 5f;

        [Tooltip("효과가 가라앉는 속도. 느릴수록 끝난 뒤 여운이 남는다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampDownSpeed = 2f;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null || _volume == null)
            {
                Debug.LogError($"{nameof(RepairPostFx)}: Aircraft 또는 Volume이 비어 있습니다.", this);
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
            AircraftRepair repair = _aircraft.Repair;

            // 수리 컴포넌트가 없으면 이 연출도 할 일이 없다. 다만 꺼두지는 않는다 —
            // 평상시 Weight로 되돌리는 일은 계속 해야 한다.
            bool repairing = repair != null && repair.IsRepairing;

            float target = repairing ? _repairWeight : _idleWeight;
            float speed = repairing ? _rampUpSpeed : _rampDownSpeed;

            _volume.weight = Mathf.Lerp(
                _volume.weight, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }
    }
}
