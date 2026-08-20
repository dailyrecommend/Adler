using System.Collections.Generic;
using Adler.Flight;
using Adler.Weapons;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 지금 알려야 하는 경고의 종류.
    /// <para>
    /// 값이 작을수록 급해서 위로 온다. 경고를 늘릴 때는 급한 만큼 앞에 끼워 넣고,
    /// <see cref="WarningDisplay"/>에 글자와 조건을 더하면 된다.
    /// </para>
    /// </summary>
    public enum WarningKind
    {
        /// <summary>발사대가 조준을 쌓고 있다.</summary>
        SamLock = 0,
    }

    /// <summary>
    /// 지금 받고 있는 경고를 화면에 늘어놓는다.
    /// <para>
    /// 디버프 목록과 나눠 둔다. 디버프는 이미 걸린 것이고 경고는 아직 일어나지 않은
    /// 것이라, 섞어 놓으면 지금 손을 써야 하는 줄이 무엇인지 가려진다.
    /// </para>
    /// <para>
    /// 급한 것이 위로 온다. 조준이 쌓이는 중이라면 숨을 시간이 있지만 이미 날아온
    /// 뒤라면 그럴 시간이 없고, 둘이 함께 떠 있을 때 먼저 읽어야 하는 것은 뒤쪽이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarningDisplay : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("만들어 둔 조각")]
        [Tooltip("경고 한 줄의 모양. WarningSlot이 붙어 있어야 한다.")]
        [SerializeField] private WarningSlot _slotPrefab;

        [Tooltip("줄이 늘어설 자리. 비워두면 이 오브젝트 아래에 붙인다.")]
        [SerializeField] private RectTransform _slotRoot;

        [Header("조준")]
        [SerializeField] private string _lockText = "SAM LOCK";

        [SerializeField] private Color _lockColor = new Color(1f, 0.75f, 0.2f, 1f);

        [Tooltip("조준이 이만큼 쌓여야 알린다. 0이면 노려지는 순간부터 뜬다.\n" +
                 "너무 낮게 잡으면 사거리를 스칠 때마다 떠서 경고가 배경이 된다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lockWarnThreshold = 0.15f;

        private readonly Dictionary<WarningKind, WarningSlot> _slots = new();
        private readonly List<WarningKind> _active = new();
        private readonly List<WarningKind> _removed = new();

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null || _slotPrefab == null)
            {
                Debug.LogError($"{nameof(WarningDisplay)}: Aircraft 또는 Slot Prefab이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (_slotRoot == null)
            {
                _slotRoot = transform as RectTransform;
            }
        }

        /// <summary>
        /// 신호를 받지 않고 매 프레임 물어본다.
        /// <para>
        /// 경고를 거는 쪽은 기체가 아니라 맵에 흩어진 발사대들이라, 하나하나 구독하려면
        /// 발사대가 생기고 사라질 때마다 이어 붙여야 한다. 몇 개 안 되는 것을 물어보는
        /// 편이 늘 맞고, 부서진 발사대가 경고를 남기고 사라지는 일도 없다.
        /// </para>
        /// </summary>
        private void Update()
        {
            CollectActive();
            Rebuild();
        }

        private void CollectActive()
        {
            _active.Clear();

            Transform self = _aircraft.transform;

            SamSite locking = SamSite.MostUrgent(self);
            if (locking != null && locking.LockProgress >= _lockWarnThreshold)
            {
                _active.Add(WarningKind.SamLock);
            }

            // 급한 것이 위로. 종류의 순서가 곧 급한 순서다.
            _active.Sort();
        }

        /// <summary>
        /// 목록에 맞춰 줄을 만들고 지운다.
        /// <para>
        /// 살아 있는 줄은 건드리지 않는다. 매 프레임 다시 만들면 글자가 계속 새로 그려져
        /// 깜빡이는 것처럼 보이고, 매 프레임이라 비용도 그대로 쌓인다.
        /// </para>
        /// </summary>
        private void Rebuild()
        {
            foreach (WarningKind kind in _active)
            {
                if (_slots.ContainsKey(kind))
                {
                    continue;
                }

                WarningSlot slot = Instantiate(_slotPrefab, _slotRoot);
                slot.Bind(kind, TextFor(kind), ColorFor(kind));
                _slots.Add(kind, slot);
            }

            _removed.Clear();

            foreach (KeyValuePair<WarningKind, WarningSlot> pair in _slots)
            {
                if (!_active.Contains(pair.Key))
                {
                    _removed.Add(pair.Key);
                }
            }

            foreach (WarningKind kind in _removed)
            {
                Destroy(_slots[kind].gameObject);
                _slots.Remove(kind);
            }

            ApplyOrder();
        }

        private void ApplyOrder()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_slots.TryGetValue(_active[i], out WarningSlot slot))
                {
                    slot.transform.SetSiblingIndex(i);
                }
            }
        }

        /// <summary>경고를 늘릴 때 여기에 글자와 색을 더한다.</summary>
        private string TextFor(WarningKind kind)
        {
            return kind switch
            {
                WarningKind.SamLock => _lockText,
                _ => string.Empty,
            };
        }

        private Color ColorFor(WarningKind kind)
        {
            return kind switch
            {
                WarningKind.SamLock => _lockColor,
                _ => Color.white,
            };
        }
    }
}
