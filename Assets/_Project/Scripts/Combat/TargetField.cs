using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 지정한 범위에 표적을 흩뿌린다. 사격 감각을 잡는 동안 쓰는 시험용 장치이며,
    /// 실제 임무 배치는 나중에 별도의 구성으로 대체된다.
    /// <para>
    /// 손으로 수십 개를 늘어놓으면 배치를 바꿀 때마다 같은 일을 반복하게 된다.
    /// 밀도와 범위만 바꿔가며 시험할 수 있어야 조준 관용도와 사거리를 판단할 수 있다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetField : MonoBehaviour
    {
        [Header("배치")]
        [SerializeField] private GameObject _prefab;

        [Min(1)]
        [SerializeField] private int _count = 20;

        [Tooltip("이 오브젝트를 중심으로 한 배치 반경 (m).")]
        [Min(1f)]
        [SerializeField] private float _radius = 120f;

        [Header("지면 맞추기")]
        [Tooltip("체크하면 위에서 아래로 쏜 광선이 닿은 지면에 올려놓는다.")]
        [SerializeField] private bool _snapToGround = true;

        [SerializeField] private LayerMask _groundMask = ~0;

        [Tooltip("지면을 찾기 위해 광선을 쏘기 시작할 높이 (m).")]
        [SerializeField] private float _groundProbeHeight = 200f;

        private void Start()
        {
            if (_prefab == null)
            {
                Debug.LogError($"{nameof(TargetField)}: 배치할 프리팹이 지정되지 않았습니다.", this);
                return;
            }

            for (int i = 0; i < _count; i++)
            {
                Spawn();
            }
        }

        private void Spawn()
        {
            Vector2 offset = Random.insideUnitCircle * _radius;
            Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (_snapToGround)
            {
                Vector3 probe = position + (Vector3.up * _groundProbeHeight);
                if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit,
                        _groundProbeHeight * 2f, _groundMask, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point;
                }
            }

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Instantiate(_prefab, position, rotation, transform);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
