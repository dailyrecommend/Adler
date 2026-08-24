using Adler.Core;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 기체와 걸린 상대를 잇는 줄을 그린다.
    /// <para>
    /// 갈고리에서 떼어낸 이유는 이쪽이 <b>아무것도 정하지 않기</b> 때문이다. 언제 걸리고
    /// 언제 끊어지는지는 갈고리가 정하고, 여기는 그 결과를 읽어 선으로 옮기기만 한다.
    /// 한 파일에 두면 줄의 생김새를 만지는 동안 갈고리의 규칙을 함께 읽어야 한다.
    /// </para>
    /// <para>
    /// 날아가는 동안에는 끝이 표적을 향해 뻗어나간다. 쏘자마자 표적까지 이어 그리면
    /// 이미 걸린 것처럼 보여서, 정작 물릴 때까지 끌려가지 않는 그 사이가 고장으로 읽힌다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrappleLine : MonoBehaviour
    {
        /// <summary>
        /// 처짐이 목표를 따라가는 속도. 팽팽해지는 것이 한 순간에 끝나지 않을 만큼만
        /// 늦다 — 이 정도가 눈에는 줄이 채이는 것으로 보인다.
        /// </summary>
        private const float SlackResponse = 14f;

        [Header("읽어올 대상")]
        [Tooltip("그릴 줄의 주인. 비워두면 이 오브젝트와 위쪽에서 찾는다.")]
        [SerializeField] private GrapplingHook _hook;

        [SerializeField] private AircraftRoot _root;

        [Header("줄")]
        [Tooltip("기체와 표적을 잇는 선.")]
        [SerializeField] private LineRenderer _line;

        [Tooltip("줄을 몇 토막으로 그릴지. 적으면 곡선이 각져 보인다.")]
        [Range(2, 64)]
        [SerializeField] private int _segments = 16;

        [Tooltip("줄이 늘어지는 정도. 줄 길이에 대한 비율이다.\n\n" +
                 "길이에 비례시키는 이유는, 고정값으로 두면 짧을 때는 우스울 만큼\n" +
                 "늘어지고 길 때는 곧은 선처럼 보이기 때문이다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _sag = 0.12f;

        [Tooltip("끌려가는 동안 남는 처짐. 0이면 완전히 곧게 펴진다.\n\n" +
                 "물렸다가 당겨지는 순간 줄이 팽팽해지는 것이 눈에 보여야, 소리와\n" +
                 "몸으로 느끼는 것과 화면이 같은 이야기를 한다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _taut = 0.2f;

        [Tooltip("처지는 방향. 0이면 아래로만, 1이면 지나온 쪽으로만 끌린다.\n\n" +
                 "빠르게 나는 기체에 매달린 줄은 중력보다 공기에 더 끌리므로,\n" +
                 "아래로만 늘어뜨리면 멈춰 있는 것처럼 보인다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _trail = 0.55f;

        // 점선은 재질이 맡는다. Line Renderer의 Texture Mode를 Tile로 두면 무늬가
        // 월드 1미터당 한 번씩 깔리므로, 재질 타일링만 정하면 칸 길이가 미터로
        // 고정된다 — 줄이 늘어나든 휘든 코드가 손댈 일이 없다.

        private Clock _clock;

        // 지금 줄이 얼마나 늘어져 있는지 (0~1). 팽팽해지는 것을 눈에 보이게 하려고
        // 곧바로 바꾸지 않고 따라가게 둔다.
        private float _slack = 1f;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _root = AircraftRoot.Resolve(this, _root);
            _hook = _hook != null ? _hook : _root?.Find<GrapplingHook>();

            if (_hook == null || _line == null)
            {
                Debug.LogError($"{nameof(GrappleLine)}: 갈고리 또는 Line Renderer가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            Show(false);
        }

        /// <summary>
        /// 기체가 움직인 뒤에 그린다. 보통 갱신에서 그리면 그 프레임의 기체가 아직
        /// 옮겨가기 전이라, 줄이 한 프레임씩 뒤처져 총구에서 떨어져 나온다.
        /// </summary>
        private void LateUpdate()
        {
            Transform hooked = _hook.Hooked;

            if (hooked == null)
            {
                Show(false);
                return;
            }

            Vector3 start = _hook.Origin.position;
            Vector3 end = _hook.Phase == GrapplePhase.Flying ? _hook.Tip : hooked.position;

            // 처짐은 단계를 따라간다. 물고 버티는 동안은 늘어져 있다가 당기기 시작하면
            // 팽팽해진다. 곧바로 바꾸지 않고 빠르게 따라가게 두는 이유는, 순간이동하면
            // 그리는 방식이 바뀐 것처럼 보이고 조금 늦으면 줄이 채이는 것으로 읽히기
            // 때문이다 — 소리와 화면이 같은 순간을 가리키게 된다.
            float wanted = _hook.Phase == GrapplePhase.Pulling ? _taut : 1f;
            _slack = Mathf.Lerp(_slack, wanted, 1f - Mathf.Exp(-SlackResponse * _clock.Delta));

            Vector3 middle = Vector3.Lerp(start, end, 0.5f)
                             + (SagDirection() * (Vector3.Distance(start, end) * _sag * _slack));

            int count = Mathf.Max(2, _segments);

            if (_line.positionCount != count)
            {
                _line.positionCount = count;
            }

            Show(true);

            for (int i = 0; i < count; i++)
            {
                _line.SetPosition(i, Bend(start, middle, end, (float)i / (count - 1)));
            }
        }

        /// <summary>
        /// 줄이 늘어지는 쪽.
        /// <para>
        /// 중력만 쓰지 않는다. 빠르게 나는 기체에 매달린 줄은 무게보다 공기에 훨씬
        /// 더 끌리므로, 아래로만 늘어뜨리면 기체가 멈춰 있는 것처럼 보인다.
        /// </para>
        /// </summary>
        private Vector3 SagDirection()
        {
            Vector3 drift = _root != null && _root.Body != null
                ? -_root.Body.linearVelocity
                : Vector3.zero;

            Vector3 direction = drift.sqrMagnitude > 0.0001f
                ? Vector3.Lerp(Vector3.down, drift.normalized, _trail)
                : Vector3.down;

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.down;
        }

        /// <summary>가운데 점 하나로 휘는 2차 베지에. 줄 하나 그리는 데는 이걸로 충분하다.</summary>
        private static Vector3 Bend(Vector3 start, Vector3 middle, Vector3 end, float t)
        {
            float u = 1f - t;

            return (u * u * start) + (2f * u * t * middle) + (t * t * end);
        }

        private void Show(bool visible)
        {
            if (_line != null && _line.enabled != visible)
            {
                _line.enabled = visible;
            }
        }
    }
}
