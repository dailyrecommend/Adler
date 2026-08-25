using Adler.Core;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 카메라가 <b>갑자기</b> 방향을 바꿀 때만 HUD를 살짝 밀었다 되돌린다.
    /// <para>
    /// 도는 동안 계속 밀려 있으면 안 된다. 선회는 대부분 등속으로 길게 이어지는데,
    /// 그동안 HUD가 한쪽에 치우쳐 있으면 고장 난 것처럼 보인다. 화면에 남아야 하는 것은
    /// "돌고 있다"가 아니라 "방금 홱 꺾었다"다.
    /// </para>
    /// <para>
    /// 그래서 도는 <em>빠르기</em>가 아니라 그 빠르기의 <em>변화</em>를 본다. 스틱을
    /// 치는 순간에만 값이 서고, 꺾은 채로 유지하는 동안에는 0이다. 그 한 번을 충격으로
    /// 실어주면 HUD가 툭 밀렸다가 제자리로 돌아온다.
    /// </para>
    /// <para>
    /// 요소 하나하나가 아니라 <b>묶음에 붙인다</b>. 층마다 세기를 다르게 줘야 깊이가
    /// 생기고 — 가까운 것이 많이, 먼 것이 적게 — 스스로 자리를 정하는 것들과 자리를
    /// 두고 다투지 않는다.
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
        /// <summary>
        /// 한 프레임에 흘렀다고 쳐줄 최대 시간(초).
        /// <para>
        /// 용수철은 한 번에 너무 많이 흐르면 발산한다. 화면이 한 번 걸렸을 때 HUD가
        /// 튕겨 나가는 것보다는, 그 프레임만 조금 느리게 도는 편이 낫다.
        /// </para>
        /// </summary>
        private const float MaxStep = 0.05f;

        [Header("읽어올 대상")]
        [Tooltip("움직임을 따라갈 카메라. 비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Camera _camera;

        [Header("반응")]
        [Tooltip("회전 속도가 갑자기 초당 1도만큼 바뀔 때 실리는 밀림(px/s).\n\n" +
                 "등속으로 도는 동안에는 아무 일도 없다. 스틱을 치는 순간에만 실린다.\n" +
                 "층마다 다르게 주면 깊이가 생긴다 — 가까이 있어야 할 것은 크게.")]
        [SerializeField] private float _kick = 2f;

        [Tooltip("아무리 세게 꺾어도 이만큼까지만 밀린다(px).\n\n" +
                 "읽을 수 있어야 하는 물건이므로 제자리에서 크게 벗어나면 안 된다.")]
        [Min(0f)]
        [SerializeField] private float _maxOffset = 30f;

        [Header("가라앉는 방식")]
        [Tooltip("제자리로 끌어당기는 힘. 클수록 빨리 돌아온다.\n\n" +
                 "작게 두면 늘어져서 한 번 밀린 것이 오래 남고, 크게 두면 밀렸다는\n" +
                 "사실 자체가 안 보인다. 60~140쯤에서 고르면 된다.")]
        [Min(1f)]
        [SerializeField] private float _stiffness = 90f;

        [Tooltip("출렁임. 1이면 지나치지 않고 곧장 멈추고, 낮출수록 넘어갔다 돌아온다.\n\n" +
                 "낮게 두면 한 번 꺾을 때마다 화면이 흔들다리처럼 떨린다. 0.7~1을 권한다.")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float _damping = 0.8f;

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
        private Vector2 _lastRate;
        private bool _primed;

        private Vector2 _offset;
        private Vector2 _velocity;

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

            delta = Mathf.Min(delta, MaxStep);

            Transform view = _camera.transform;

            // 지난 프레임의 정면을 지금 카메라 기준으로 다시 본다. 카메라가 오른쪽으로
            // 돌았으면 그 방향이 왼쪽에 있게 되므로, 이 값이 곧 "얼마나 어느 쪽으로
            // 돌았는가"다. 오일러 각으로 재면 짐벌과 축 순서에 걸려 뒤집히는 구간이 생긴다.
            Vector3 local = view.InverseTransformDirection(_lastForward);
            _lastForward = view.forward;

            // 초당 각도로 환산한다. 프레임 사이의 값을 그대로 쓰면 프레임이 빠를수록
            // 조금씩 밀려서, 잘 도는 컴퓨터에서 효과가 옅어진다.
            Vector2 rate = new Vector2(local.x, local.y) * (Mathf.Rad2Deg / delta);

            // 빠르기가 아니라 그 변화를 본다. 등속으로 도는 동안에는 0이므로 HUD는
            // 제자리에 있고, 스틱을 치는 순간에만 값이 선다.
            Vector2 jerk = _primed ? rate - _lastRate : Vector2.zero;

            _lastRate = rate;
            _primed = true;

            // 흐른 시간으로 나누지 않는다. 한 번의 기동에서 실리는 총량이 프레임 수와
            // 무관하게 같아야 하는데, 변화량을 그대로 더하면 저절로 그렇게 된다.
            _velocity += jerk * (_leadInstead ? -_kick : _kick);

            Settle(delta);

            _rect.anchoredPosition = _home + _offset;
        }

        /// <summary>
        /// 용수철 한 걸음. 언제나 제자리로 끌어당기고, 속도에 비례한 저항으로 가라앉힌다.
        /// <para>
        /// 저항 계수를 강성의 제곱근에서 뽑으므로, 강성을 올려도 출렁임의 성격은
        /// 그대로다 — 돌아오는 빠르기와 출렁임을 따로 만질 수 있다.
        /// </para>
        /// </summary>
        private void Settle(float delta)
        {
            float drag = 2f * _damping * Mathf.Sqrt(_stiffness);

            _velocity += (-_offset * _stiffness - _velocity * drag) * delta;
            _offset += _velocity * delta;

            if (_offset.sqrMagnitude <= _maxOffset * _maxOffset)
            {
                return;
            }

            // 한계에 닿으면 밀어내던 속도까지 끊는다. 자리만 붙잡아두면 속도가 계속
            // 쌓여서, 한계를 벗어나는 순간 튕겨 나간다.
            _offset = _offset.normalized * _maxOffset;
            _velocity = Vector2.zero;
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
