using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 싸움이 벌어지는 상자. 중심은 이 오브젝트의 위치고, 크기는 세 변이다.
    /// <para>
    /// 경계가 필요한 쪽이 여럿이다 — 스포너는 이 안에서만 적을 내놓아야 하고, 적기는
    /// 이 밖으로 날아가면 안 되고, 플레이어는 나가면 값을 치른다. 각자 크기를 들고
    /// 있으면 지도를 넓힐 때 하나만 고쳐져서 서로 어긋난다. 사실은 하나이므로
    /// 있는 곳도 한 곳이다.
    /// </para>
    /// <para>
    /// 상자로 잰다. 지도가 네모라 원으로 재면 구석이 버려지고, 높이도 경계의
    /// 일부여야 천장 없는 하늘로 도망치는 길이 막힌다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Battlefield : MonoBehaviour
    {
        [Tooltip("상자의 세 변 (m). 기본 큐브를 이 크기로 늘린 것과 같다.\n" +
                 "중심이 이 오브젝트의 위치이므로, 바닥 0에서 높이 300을 쓰려면\n" +
                 "오브젝트를 y=150에 두어야 한다.")]
        [SerializeField] private Vector3 _size = new(750f, 300f, 750f);

        /// <summary>상자의 세 변 (m).</summary>
        public Vector3 Size => _size;

        /// <summary>상자의 중심.</summary>
        public Vector3 Center => transform.position;

        /// <summary>
        /// 씬의 경계. 없으면 null — 경계 없는 씬도 성립해야 하므로, 읽는 쪽은
        /// 없을 때 아무 제한도 없는 것으로 친다.
        /// </summary>
        public static Battlefield Active =>
            Registry<Battlefield>.All.Count > 0 ? Registry<Battlefield>.All[0] : null;

        private void OnEnable() => Registry<Battlefield>.Add(this);

        private void OnDisable() => Registry<Battlefield>.Remove(this);

        /// <summary>
        /// 이 점에서 가장 가까운 벽까지의 거리 (m). 안쪽이면 양수, 밖이면 음수다.
        /// <para>
        /// 하나의 숫자로 주는 이유는 읽는 쪽의 질문이 "어느 벽에 가까운가"가 아니라
        /// "얼마나 위험한가"이기 때문이다. 벽이 여섯이어도 위험은 하나다.
        /// </para>
        /// </summary>
        public float DepthInside(Vector3 point)
        {
            Vector3 offset = point - Center;
            Vector3 half = _size * 0.5f;

            return Mathf.Min(
                half.x - Mathf.Abs(offset.x),
                Mathf.Min(half.y - Mathf.Abs(offset.y), half.z - Mathf.Abs(offset.z)));
        }

        /// <summary>이 점이 상자 안인지.</summary>
        public bool Contains(Vector3 point) => DepthInside(point) >= 0f;

        /// <summary>
        /// 상자 밖의 점을 안으로 끌어들인다. 안에 있어도 여유보다 벽에 가까우면
        /// 여유만큼 안쪽으로 옮긴다.
        /// </summary>
        public Vector3 ClampInside(Vector3 point, float margin)
        {
            Vector3 offset = point - Center;
            Vector3 half = _size * 0.5f;

            offset.x = Mathf.Clamp(offset.x, -(half.x - margin), half.x - margin);
            offset.y = Mathf.Clamp(offset.y, -(half.y - margin), half.y - margin);
            offset.z = Mathf.Clamp(offset.z, -(half.z - margin), half.z - margin);

            return Center + offset;
        }

#if UNITY_EDITOR
        /// <summary>씬 뷰에서 경계가 보여야 지도와 맞는지 눈으로 잴 수 있다.</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(transform.position, _size);
        }
#endif
    }
}
