using Adler.Core;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 카메라가 도는 만큼 HUD를 조금 뒤처지게 민다.
    /// <para>
    /// 화면에 딱 붙어 있는 HUD는 종이에 인쇄된 것처럼 보인다. 기수를 홱 꺾었는데 글자만
    /// 미동도 없으면, 그 순간 화면이 아니라 <em>화면 위에 얹힌 그림</em>이 된다. 조금
    /// 뒤처졌다 따라오면 조종석 앞 허공에 떠 있는 것으로 읽힌다.
    /// </para>
    /// <para>
    /// 요소 하나하나가 아니라 <b>묶음에 붙인다</b>. 이유가 둘이다. 하나는 층마다 세기를
    /// 다르게 줘야 깊이가 생기기 때문이고 — 가까운 것이 많이, 먼 것이 적게 — 다른 하나는
    /// 스스로 자리를 정하는 것들(피해 숫자, 락온 표식)과 자리를 두고 다투지 않기 위해서다.
    /// </para>
    /// <para>
    /// 늦춰진 시계를 쓴다. 히트스톱이 걸린 동안 화면은 멎어 있는데 HUD만 계속 흘러가면
    /// 그 순간이 멈춘 것으로 읽히지 않는다.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HudParallax : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [Tooltip("움직임을 따라갈 카메라. 비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Camera _camera;

        [Header("세기")]
        [Tooltip("카메라가 초당 1도 돌 때 밀리는 거리(px).\n\n" +
                 "층마다 다르게 주면 깊이가 생긴다 — 가까이 있어야 할 것은 크게,\n" +
                 "멀리 있어야 할 것은 작게. 0.15~0.4쯤에서 고르면 된다.")]
        [SerializeField] private float _sway = 0.25f;

        [Tooltip("아무리 세게 꺾어도 이만큼까지만 밀린다(px).\n\n" +
                 "없으면 급기동 한 번에 HUD가 화면 밖으로 날아간다. 읽을 수 있어야 하는\n" +
                 "물건이므로 제자리에서 크게 벗어나면 안 된다.")]
        [Min(0f)]
        [SerializeField] private float _maxOffset = 40f;

        [Tooltip("밀린 것이 따라붙고 돌아오는 속도. 클수록 즉각적이다.\n\n" +
                 "작게 두면 흐물거리고, 크게 두면 뒤처지는 느낌 자체가 사라진다.")]
        [Min(0.1f)]
        [SerializeField] private float _response = 8f;

        [Header("방향")]
        [Tooltip("체크하면 카메라가 도는 쪽으로 따라간다.\n" +
                 "끄면 반대로 밀린다 — 관성이 있는 물건이라면 이쪽이 맞다.")]
        [SerializeField] private bool _leadInstead;

        private RectTransform _rect;
        private Clock _clock;

        // 인스펙터에서 잡아둔 제자리. 여기에 밀림을 얹는다. 지금 자리를 기준으로 삼으면
        // 밀린 자리에 또 얹혀서 한 방향으로 계속 흘러간다.
        private Vector2 _home;

        private Vector3 _lastForward;
        private Vector2 _offset;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _clock = TimeScale.For(this);
            _home = _rect.anchoredPosition;

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                Debug.LogError($"{nameof(HudParallax)}: 따라갈 카메라를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _lastForward = _camera.transform.forward;
        }

        /// <summary>
        /// 카메라가 자리를 잡은 뒤에 읽는다. 실행 순서를 뒤로 미뤄둔 것도 그래서다 —
        /// Cinemachine이 LateUpdate에서 카메라를 옮기므로, 그전에 읽으면 한 프레임 늦는다.
        /// </summary>
        private void LateUpdate()
        {
            float delta = _clock.Delta;

            if (delta <= 0f)
            {
                return;
            }

            Transform view = _camera.transform;

            // 지난 프레임의 정면을 지금 카메라 기준으로 다시 본다. 카메라가 오른쪽으로
            // 돌았으면 그 방향이 왼쪽에 있게 되므로, 이 값이 곧 "얼마나 어느 쪽으로
            // 돌았는가"다. 오일러 각으로 재면 짐벌과 축 순서에 걸려 뒤집히는 구간이 생긴다.
            Vector3 local = view.InverseTransformDirection(_lastForward);
            _lastForward = view.forward;

            // 초당 각도로 환산한다. 프레임 사이의 값을 그대로 쓰면 프레임이 빠를수록
            // 조금씩 밀려서, 잘 도는 컴퓨터에서 효과가 옅어진다.
            Vector2 rate = new Vector2(local.x, local.y) * (Mathf.Rad2Deg / delta);

            Vector2 target = rate * (_leadInstead ? -_sway : _sway);

            if (target.sqrMagnitude > _maxOffset * _maxOffset)
            {
                target = target.normalized * _maxOffset;
            }

            _offset = Vector2.Lerp(_offset, target, 1f - Mathf.Exp(-_response * delta));
            _rect.anchoredPosition = _home + _offset;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 실행 중에 자리를 옮기면 그것을 새 제자리로 삼는다. 그러지 않으면 배치를
        /// 다듬는 동안 밀림이 옛 자리를 기준으로 계산되어 엉뚱한 데로 튄다.
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying && _rect != null)
            {
                _home = _rect.anchoredPosition - _offset;
            }
        }
#endif
    }
}
