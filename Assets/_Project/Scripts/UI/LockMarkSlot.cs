using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 표적 하나에 붙는 표식.
    /// <para>
    /// 표식 본체는 대기중이든 락온중이든 자리를 지키고 색만 바뀐다. 잡혔다고 사라졌다
    /// 다른 모양이 나타나면 옮겨간 것이 아니라 놓쳤다가 새로 잡은 것처럼 보이는데,
    /// 락온이 옮겨 다니는 것은 계속 일어나는 일이라 그때마다 끊기면 눈이 따라가지 못한다.
    /// 자리는 그대로 두고 색만 갈면 무엇이 무엇으로 바뀌었는지가 그대로 보인다.
    /// </para>
    /// <para>
    /// 대기중인 표식은 크게 나타나 돌면서 작아진다. 다 줄어들어 멈추는 것이 곧 잡을 수
    /// 있게 됐다는 신호다. 막대나 채움으로 알리면 그것을 읽는 동안 표적에서 눈을 떼야
    /// 하는데, 크기가 줄고 도는 움직임은 곁눈질로도 들어온다 — 지금 봐야 하는 것은
    /// 표식이 아니라 적이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockMarkSlot : MonoBehaviour
    {
        [Header("모양")]
        [Tooltip("표식 본체. 대기중이든 락온중이든 늘 보이고 색만 바뀐다.\n" +
                 "다 차기 전에는 돌면서 줄어들고, 다 차면 제 크기로 멈춰 선다.")]
        [SerializeField] private RectTransform _standby;

        [Tooltip("락온중일 때 본체 위에 얹을 것. 비워둬도 된다.\n" +
                 "화면에 하나뿐이다 — 대기중인 것들 중 조준점에 가장 가까운 하나.")]
        [SerializeField] private RectTransform _locked;

        [Header("색")]
        [Tooltip("락온 대기중.\n\n" +
                 "본체 아래에 있는 그림 전부에 입힌다. 흰색으로 두면 곱해도 그대로라,\n" +
                 "프리팹에 칠해둔 색을 쓰고 싶으면 흰색으로 두면 된다.")]
        [SerializeField] private Color _standbyColor = new(0.35f, 1f, 0.5f, 0.85f);

        [Tooltip("락온중. 대기중과 확실히 달라야 어느 놈이 잡혔는지 한눈에 들어온다.\n" +
                 "본체와 위에 얹는 것 모두 이 색이 된다.")]
        [SerializeField] private Color _lockedColor = new(1f, 0.25f, 0.2f, 1f);

        [Header("대기 연출")]
        [Tooltip("막 보이기 시작했을 때의 배율. 여기서 1까지 줄어든다.\n" +
                 "작게 잡으면 줄어드는 것이 눈에 띄지 않아 언제 준비됐는지 알 수 없다.")]
        [Min(1f)]
        [SerializeField] private float _startScale = 2.5f;

        [Tooltip("줄어드는 동안 도는 각도. 여기서 0까지 돌아와 멈춘다.\n" +
                 "0으로 두면 돌지 않고 줄어들기만 한다.")]
        [SerializeField] private float _spin = 180f;

        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;

        // 색을 갈아입힐 그림들. 매 프레임 찾지 않으려고 한 번만 모아둔다.
        private Graphic[] _standbyGraphics;

        // 아직 한 번도 칠하지 않았음을 뜻한다. 표식은 다른 표적에게 물려주며 다시
        // 쓰이므로, 물려받은 색이 남아 있지 않도록 처음 한 번은 반드시 칠해야 한다.
        private bool _painted;
        private bool _wasLocked;

        /// <summary>표식을 옮길 때 쓴다. 매번 형변환하지 않으려고 들고 있는다.</summary>
        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        private void Awake()
        {
            if (_standby != null)
            {
                _baseScale = _standby.localScale;
                _standbyGraphics = _standby.GetComponentsInChildren<Graphic>(includeInactive: true);
            }

            // 얹는 쪽은 늘 같은 색이라 한 번만 칠하면 된다.
            Tint(_locked, _lockedColor);
        }

        /// <summary>이번 프레임 상태를 받아 그린다.</summary>
        /// <param name="progress">잡을 수 있게 되기까지의 진행도. 1이면 다 찼다.</param>
        /// <param name="locked">지금 잡혀 있는 그 표적인지.</param>
        public void Bind(float progress, bool locked)
        {
            Toggle(_locked, locked);

            if (_standby == null)
            {
                return;
            }

            Toggle(_standby, true);

            // 색은 바뀔 때만 칠한다. 매 프레임 칠하면 캔버스가 그때마다 다시 그린다.
            if (!_painted || locked != _wasLocked)
            {
                _painted = true;
                _wasLocked = locked;
                Paint(locked ? _lockedColor : _standbyColor);
            }

            float t = Mathf.Clamp01(progress);

            _standby.localScale = _baseScale * Mathf.Lerp(_startScale, 1f, t);
            _standby.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_spin, 0f, t));
        }

        private void Paint(Color color)
        {
            if (_standbyGraphics == null)
            {
                return;
            }

            foreach (Graphic graphic in _standbyGraphics)
            {
                if (graphic != null)
                {
                    graphic.color = color;
                }
            }
        }

        private static void Tint(RectTransform root, Color color)
        {
            if (root == null)
            {
                return;
            }

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                graphic.color = color;
            }
        }

        private static void Toggle(Component target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
            {
                target.gameObject.SetActive(on);
            }
        }

#if UNITY_EDITOR
        /// <summary>인스펙터에서 색을 고르는 동안 바로 보이게 한다. 쉬고 있는 모습으로 그린다.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            Tint(_standby, _standbyColor);
            Tint(_locked, _lockedColor);
        }
#endif
    }
}
