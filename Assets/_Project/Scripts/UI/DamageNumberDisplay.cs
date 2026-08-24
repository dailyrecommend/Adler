using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 피해를 입은 것들 위에 그만큼의 숫자를 띄운다.
    /// <para>
    /// 한 대 때렸을 때 알 수 있는 것은 맞았다는 사실뿐이고, 얼마나 통했는지는 알 수 없다.
    /// 장갑에 깎이는 무기와 제대로 먹히는 무기가 화면에서 똑같이 보이면, 무엇을 들고
    /// 싸워야 하는지는 표적이 터질 때까지 기다려야만 알게 된다.
    /// </para>
    /// <para>
    /// 한 발에 하나씩 띄우되 표적 둘레의 원 안에 흩는다. 기총은 초당 열다섯 발이라
    /// 전부 한 점에서 솟으면 덩어리로 뭉쳐버리는데, 몇 대 맞혔는지는 숫자가 몇 개
    /// 떴는지로 읽는 것이라 묶어서 하나로 만들면 그 정보가 사라진다.
    /// </para>
    /// <para>
    /// 원 안에서 아무 데나 고르게 두지 않는다. 무작위는 겹치지 않는다는 보장이 없어서
    /// 하필 포개지는 두 개가 반드시 생긴다. 대신 여기가 어느 자리가 비어 있는지를
    /// 기억해 두고 그 번호를 넘겨준다 — 흔드는 일은 받은 쪽이 자기 자리 안에서 한다.
    /// </para>
    /// <para>
    /// 피해가 어디서 왔는지 묻지 않는다. 기총이든 폭탄이든 들이받은 것이든 내구도가
    /// 깎였다는 사실만 듣기 때문에, 무기를 새로 만들어도 여기는 그대로다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberDisplay : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [Tooltip("숫자를 띄울 대상의 레이어. 적만 남기지 않으면 내가 맞을 때도 뜬다.")]
        [SerializeField] private LayerMask _layers = ~0;

        [Tooltip("비워두면 Camera.main을 쓴다. 화면에 보이는 그 카메라여야 한다.")]
        [SerializeField] private Camera _camera;

        [Header("만들어 둔 조각")]
        [Tooltip("숫자 하나의 모양. DamageNumberSlot이 붙어 있어야 한다.\n" +
                 "앵커와 피벗을 가운데(0.5, 0.5)로 둘 것.")]
        [SerializeField] private DamageNumberSlot _numberPrefab;

        [Tooltip("숫자들이 붙을 자리. 비워두면 이 오브젝트 아래에 붙인다.\n" +
                 "자리를 직접 정하므로 Layout Group을 붙이면 안 된다.")]
        [SerializeField] private RectTransform _numberRoot;

        [Tooltip("한 번에 띄울 수 있는 숫자의 최대 수.\n" +
                 "넘치면 가장 오래된 것부터 물러난다 — 방금 일어난 일이 더 중요하다.")]
        [Min(1)]
        [SerializeField] private int _maxNumbers = 24;

        [Header("자리")]
        [Tooltip("대상의 중심에서 얼마나 띄울지(m). 기체 안에서 숫자가 솟지 않게 한다.")]
        [SerializeField] private Vector3 _worldOffset = new(0f, 1.2f, 0f);

        [Tooltip("켜면 숫자가 대상을 따라다닌다.\n\n" +
                 "빠르게 스쳐 지나가는 기체에서는 따라다녀야 계속 읽히지만,\n" +
                 "고정된 시설이라면 맞은 자리에 남는 편이 어디를 때렸는지 알려준다.")]
        [SerializeField] private bool _followTarget = true;

        [Header("묶기")]
        [Tooltip("0이면 한 발에 하나씩 따로 띄운다.\n\n" +
                 "0보다 크면 띄운 지 그 시간 안에 같은 대상을 또 맞혔을 때 새로 띄우지\n" +
                 "않고 앞의 숫자에 더한다. 화면은 조용해지지만 몇 대 맞혔는지는 사라진다.")]
        [Min(0f)]
        [SerializeField] private float _mergeSeconds;

        private readonly List<DamageNumberSlot> _pool = new();
        private readonly List<Entry> _live = new();
        private Canvas _canvas;
        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_numberRoot == null)
            {
                _numberRoot = transform as RectTransform;
            }

            if (_numberPrefab == null || _numberRoot == null || _camera == null)
            {
                Debug.LogError(
                    $"{nameof(DamageNumberDisplay)}: Number Prefab, 붙일 자리, 카메라 중 빠진 것이 있습니다.", this);
                enabled = false;
                return;
            }

            _canvas = _numberRoot.GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// 이 통로는 씬보다 오래 살아서, 끊지 않으면 사라진 화면 표시가 계속 불려 나온다.
        /// </summary>
        private void OnEnable() => Health.AnyDamaged += OnAnyDamaged;

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Retire(i);
            }
        }

        private void OnAnyDamaged(Health target, DamageInfo damage, DamageResult result)
        {
            if (target == null || result.Applied <= 0f)
            {
                return;
            }

            if ((_layers.value & (1 << target.gameObject.layer)) == 0)
            {
                return;
            }

            // 아직 받아줄 수 있는 것이 있으면 새로 띄우지 않고 거기에 더한다.
            if (_mergeSeconds > 0f && TryMerge(target, result))
            {
                return;
            }

            // 가장 오래된 것을 물린다. 방금 일어난 일을 버리면 숫자를 띄우는 뜻이 없다.
            if (_live.Count >= _maxNumbers)
            {
                Retire(0);
            }

            int spot = FreeSpotFor(target);
            DamageNumberSlot slot = Take();

            slot.Begin(target.transform, PointOf(target, damage), result.Applied, result.Killed, spot);
            _live.Add(new Entry(target, slot, spot));
        }

        /// <summary>
        /// 기체와 카메라가 움직인 뒤에 자리를 잡는다. 보통 갱신에서 하면 그 프레임의
        /// 카메라가 아직 옮겨가기 전이라 숫자가 한 프레임씩 뒤처져 따라온다.
        /// </summary>
        private void LateUpdate()
        {
            float delta = _clock.Delta;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                DamageNumberSlot slot = _live[i].Slot;

                // 죽은 대상은 사라지거나 꺼진다. 그때는 마지막으로 봐 둔 자리에 멈춘다.
                if (_followTarget && slot.Target != null)
                {
                    slot.Follow(slot.Target.position + _worldOffset);
                }

                bool onScreen = TryPlace(slot);

                // 화면 밖은 숨기기만 한다. 되돌아오는 동안에도 수명은 흐른다.
                if (slot.gameObject.activeSelf != onScreen)
                {
                    slot.gameObject.SetActive(onScreen);
                }

                slot.Tick(delta);

                if (slot.Finished)
                {
                    Retire(i);
                }
            }
        }

        /// <summary>화면 위 자리를 정한다. 카메라 뒤로 넘어갔으면 띄우지 않는다.</summary>
        private bool TryPlace(DamageNumberSlot slot)
        {
            Vector3 screenPoint = _camera.WorldToScreenPoint(slot.LastPoint);

            // z는 화면 깊이가 아니라 카메라까지의 거리다. 음수면 등 뒤라는 뜻이고,
            // 그때 x와 y는 뒤집힌 값이라 그대로 쓰면 엉뚱한 자리에 뜬다.
            if (screenPoint.z <= 0f)
            {
                return false;
            }

            Camera uiCamera = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas != null ? _canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _numberRoot, screenPoint, uiCamera, out Vector2 local))
            {
                return false;
            }

            slot.PlaceAt(local);
            return true;
        }

        private bool TryMerge(Health target, in DamageResult result)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i].Target == target && _live[i].Slot.Age <= _mergeSeconds)
                {
                    _live[i].Slot.Add(result.Applied, result.Killed);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 이 대상 위에서 아직 아무도 쓰지 않는 가장 낮은 번호를 준다.
        /// <para>
        /// 떠 있는 개수를 세는 것으로는 모자란다. 가운데 것이 먼저 사라지면 개수는
        /// 줄어도 그 번호는 여전히 쓰이고 있어서, 다음 숫자가 남의 자리로 날아간다.
        /// </para>
        /// <para>
        /// 낮은 번호부터 채우므로 성기게 맞힐 때는 표적 가까이에만 뜨고, 긁을 때라야
        /// 원 가장자리까지 퍼진다 — 흩어진 폭이 곧 얼마나 퍼붓고 있는지를 알려준다.
        /// </para>
        /// </summary>
        private int FreeSpotFor(Health target)
        {
            int taken = 0;

            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i].Target == target && _live[i].Spot < 32)
                {
                    taken |= 1 << _live[i].Spot;
                }
            }

            for (int spot = 0; spot < 32; spot++)
            {
                if ((taken & (1 << spot)) == 0)
                {
                    return spot;
                }
            }

            return 0;
        }

        /// <summary>
        /// 따라다니지 않을 때만 맞은 자리를 쓴다. 따라다니는데 맞은 자리에서 시작하면
        /// 첫 프레임에 중심으로 튀어 옮겨간다.
        /// </summary>
        private Vector3 PointOf(Health target, in DamageInfo damage)
            => _followTarget ? target.transform.position + _worldOffset : damage.Point;

        /// <summary>모자라면 만들고, 쉬고 있는 것이 있으면 다시 쓴다.</summary>
        private DamageNumberSlot Take()
        {
            if (_pool.Count > 0)
            {
                DamageNumberSlot reused = _pool[^1];
                _pool.RemoveAt(_pool.Count - 1);
                reused.gameObject.SetActive(true);
                return reused;
            }

            DamageNumberSlot made = Instantiate(_numberPrefab, _numberRoot);

            // 프리팹을 꺼 둔 채로 두는 일이 흔한데, 그러면 Awake가 돌지 않아
            // 자기 조각을 찾지 못한 상태로 쓰이게 된다.
            made.gameObject.SetActive(true);

            return made;
        }

        private void Retire(int index)
        {
            DamageNumberSlot slot = _live[index].Slot;
            _live.RemoveAt(index);

            slot.gameObject.SetActive(false);
            _pool.Add(slot);
        }

        /// <summary>띄워 둔 숫자 하나와 그것이 매달린 대상, 그리고 차지하고 있는 자리.</summary>
        private readonly struct Entry
        {
            public readonly Health Target;
            public readonly DamageNumberSlot Slot;

            /// <summary>이 대상 위에서 몇 번째 방향으로 날아갔는지. 사라질 때까지 이 자리를 쥔다.</summary>
            public readonly int Spot;

            public Entry(Health target, DamageNumberSlot slot, int spot)
            {
                Target = target;
                Slot = slot;
                Spot = spot;
            }
        }
    }
}
