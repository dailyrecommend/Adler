using System.Collections.Generic;
using Adler.Flight;
using Adler.Weapons;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 화면에 보이는 적들 위에 조준 표식을 얹는다.
    /// <para>
    /// 화면에 있는 적 모두가 표식을 받는다. 잡힌 하나만 표시하면 다음에 무엇을 잡을 수
    /// 있는지 알 수 없어서, 시선을 옮기는 것이 결과를 보고 나서야 맞았는지 아는 도박이 된다.
    /// </para>
    /// <para>
    /// 표식 하나가 대기중과 락온중을 함께 맡는다. 둘을 다른 오브젝트로 두면 락온이
    /// 옮겨갈 때 한쪽이 사라지고 다른 쪽이 나타나서, 옮겨간 것이 아니라 놓쳤다가 새로
    /// 잡은 것처럼 보인다.
    /// </para>
    /// <para>
    /// 만들어 둔 표식은 지우지 않고 숨겨서 다시 쓴다. 도그파이팅 중에는 적이 화면을
    /// 드나드는 일이 초당 여러 번인데, 그때마다 만들고 부수면 그 쓰레기를 치우느라
    /// 화면이 끊긴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockMarkDisplay : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("비워두면 Camera.main을 쓴다. 표적을 잡을 때 쓰는 카메라와 같아야 한다.")]
        [SerializeField] private Camera _camera;

        [Header("만들어 둔 조각")]
        [Tooltip("표식 하나의 모양. LockMarkSlot이 붙어 있어야 한다.\n" +
                 "앵커와 피벗을 가운데(0.5, 0.5)로 둘 것.")]
        [SerializeField] private LockMarkSlot _markPrefab;

        [Tooltip("표식들이 붙을 자리. 비워두면 이 오브젝트 아래에 붙인다.\n" +
                 "자리를 직접 정하므로 Layout Group을 붙이면 안 된다.")]
        [SerializeField] private RectTransform _markRoot;

        [Tooltip("한 번에 띄울 수 있는 표식의 최대 수.\n" +
                 "화면에 적이 이보다 많으면 조준점에 가까운 것부터 띄운다.")]
        [Min(1)]
        [SerializeField] private int _maxMarks = 12;

        private LockOnTargeting _targeting;
        private Canvas _canvas;
        private readonly List<LockMarkSlot> _slots = new();

        // 지난 프레임에 잡혀 있던 표적. 바뀌는 순간을 잡아 연출을 다시 재생한다.
        private Transform _locked;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _targeting = _aircraft != null ? _aircraft.Targeting : null;

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_markRoot == null)
            {
                _markRoot = transform as RectTransform;
            }

            if (_targeting == null || _markPrefab == null || _markRoot == null || _camera == null)
            {
                Debug.LogError(
                    $"{nameof(LockMarkDisplay)}: 조준, Mark Prefab, 붙일 자리, 카메라 중 빠진 것이 있습니다.", this);
                enabled = false;
                return;
            }

            _canvas = _markRoot.GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// 기체와 카메라가 움직인 뒤에 자리를 잡는다.
        /// <para>
        /// 보통 갱신에서 하면 그 프레임에 카메라가 아직 옮겨가기 전이라, 표식이 한 프레임씩
        /// 뒤처져 따라온다. 빠르게 도는 동안에는 그 한 프레임이 표적에서 눈에 띄게 벗어난다.
        /// </para>
        /// </summary>
        private void LateUpdate()
        {
            IReadOnlyList<LockOnTargeting.LockMark> marks = _targeting.Marks;
            int shown = 0;

            for (int i = 0; i < marks.Count && shown < _maxMarks; i++)
            {
                LockOnTargeting.LockMark mark = marks[i];
                LockMarkSlot slot = SlotAt(shown);

                if (!TryPlace(slot, mark))
                {
                    continue;
                }

                // 잡힌 대상이 바뀌었으면 연출을 처음부터 다시 재생한다. 슬롯이 아니라
                // 표적을 기준으로 견주는 이유는, 슬롯이 표적들 사이를 돌려 쓰이기
                // 때문이다 — 같은 표적이 다른 슬롯으로 넘어갔을 뿐인데도 새로 잡힌
                // 것으로 읽히면, 조준점이 흔들리는 동안 연출이 계속 다시 터진다.
                if (mark.Locked && mark.Target != _locked)
                {
                    _locked = mark.Target;
                    slot.Strike();
                }

                shown++;
            }

            // 놓쳤으면 비워둔다. 그러지 않으면 같은 표적을 다시 잡을 때 바뀐 것이
            // 없다고 보고 연출을 건너뛴다.
            if (!_targeting.HasLock)
            {
                _locked = null;
            }

            // 이번에 쓰지 않은 것들은 숨겨만 둔다. 다음 프레임에 다시 필요해진다.
            for (int i = shown; i < _slots.Count; i++)
            {
                Toggle(_slots[i], false);
            }
        }

        /// <summary>
        /// 표식을 표적 위로 옮긴다. 화면 밖으로 나갔으면 띄우지 않는다.
        /// </summary>
        private bool TryPlace(LockMarkSlot slot, LockOnTargeting.LockMark mark)
        {
            Vector3 screenPoint = _camera.WorldToScreenPoint(mark.Point);

            // z는 화면 깊이가 아니라 카메라까지의 거리다. 음수면 등 뒤라는 뜻이고,
            // 그때 x와 y는 뒤집힌 값이라 그대로 쓰면 엉뚱한 자리에 뜬다.
            if (screenPoint.z <= 0f)
            {
                Toggle(slot, false);
                return false;
            }

            Camera uiCamera = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas != null ? _canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _markRoot, screenPoint, uiCamera, out Vector2 local))
            {
                Toggle(slot, false);
                return false;
            }

            Toggle(slot, true);
            slot.Rect.anchoredPosition = local;
            slot.Bind(mark.Progress, mark.Locked);

            return true;
        }

        /// <summary>모자라면 만들고, 있으면 다시 쓴다.</summary>
        private LockMarkSlot SlotAt(int index)
        {
            while (_slots.Count <= index)
            {
                _slots.Add(Instantiate(_markPrefab, _markRoot));
            }

            return _slots[index];
        }

        private static void Toggle(LockMarkSlot slot, bool on)
        {
            if (slot != null && slot.gameObject.activeSelf != on)
            {
                slot.gameObject.SetActive(on);
            }
        }
    }
}
