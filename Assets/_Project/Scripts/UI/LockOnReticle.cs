using Adler.Flight;
using Adler.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 조준 중인 표적 위에 링을 띄우고 진행도를 채운다.
    /// <para>
    /// 진행도와 위치를 한 요소로 함께 전달한다. 막대나 숫자로 보여주면 그것을 읽는 동안
    /// 표적에서 눈을 떼게 되는데, 조준은 표적을 계속 보고 있어야 유지되는 것이라 그
    /// 시선 이동 자체가 조준을 방해한다.
    /// </para>
    /// <para>
    /// 채워지면서 좁아진다. 채움은 얼마나 남았는지를 정확히 알려주고, 좁아지는 움직임은
    /// 곁눈질로도 눈에 들어온다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockOnReticle : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Camera _camera;

        [Header("옮길 요소")]
        [Tooltip("표적 위로 옮길 링. 앵커를 가운데(0.5, 0.5)로 둘 것.")]
        [SerializeField] private RectTransform _ring;

        [Tooltip("진행도를 채울 이미지. Image Type을 Filled, Fill Method를 Radial 360으로.")]
        [SerializeField] private Image _fill;

        [Tooltip("비워두면 링이 속한 캔버스를 찾아 쓴다.")]
        [SerializeField] private Canvas _canvas;

        [Header("크기")]
        [Tooltip("조준을 막 잡았을 때의 배율. 여기서 1까지 좁아진다.")]
        [Min(1f)]
        [SerializeField] private float _startScale = 2.4f;

        [Tooltip("좁아지는 움직임이 따라붙는 속도. 0이면 진행도를 그대로 따른다.")]
        [Min(0f)]
        [SerializeField] private float _scaleDamping = 12f;

        [Header("색")]
        [Tooltip("조준을 잡는 중.")]
        [SerializeField] private Color _seekingColor = new Color(1f, 1f, 1f, 0.7f);

        [Tooltip("조준이 완성됐을 때.")]
        [SerializeField] private Color _lockedColor = new Color(1f, 0.3f, 0.25f, 1f);

        [Header("완성 연출")]
        [Tooltip("조준이 걸리는 순간 켤 요소. 비워둬도 된다.")]
        [SerializeField] private GameObject _lockedFlourish;

        private LockOnTargeting _targeting;
        private CanvasGroup _group;
        private RectTransform _referenceRect;
        private Vector3 _baseScale = Vector3.one;
        private float _scale = 1f;
        private bool _wasLocked;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _targeting = _aircraft != null ? _aircraft.Targeting : null;

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_targeting == null || _ring == null || _camera == null)
            {
                Debug.LogError($"{nameof(LockOnReticle)}: 조준, 링, 카메라 중 빠진 것이 있습니다.", this);
                enabled = false;
                return;
            }

            if (_canvas == null)
            {
                _canvas = _ring.GetComponentInParent<Canvas>();
            }

            _referenceRect = _ring.parent as RectTransform ?? _canvas.transform as RectTransform;
            _baseScale = _ring.localScale;

            // 오브젝트를 끄지 않고 투명하게 만든다. 이 스크립트가 링 안에 있으면
            // 끄는 순간 함께 멈춰 다시 켜줄 코드가 돌지 않는다.
            _group = _ring.GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = _ring.gameObject.AddComponent<CanvasGroup>();
            }

            SetVisible(false);
        }

        private void LateUpdate()
        {
            Transform target = _targeting.Target;

            if (target == null)
            {
                SetVisible(false);
                _wasLocked = false;
                _scale = _startScale;
                return;
            }

            // 원점이 아니라 몸통 한가운데를 쓴다. 지상 표적은 원점이 발밑에 있어서
            // 링이 표적이 아니라 그 아래 땅에 붙는다.
            Vector3 screenPoint = _camera.WorldToScreenPoint(_targeting.TargetPoint);

            // z는 화면 깊이가 아니라 카메라까지의 거리다. 음수면 등 뒤라는 뜻이고,
            // 그때 x와 y는 뒤집힌 값이라 그대로 쓰면 엉뚱한 자리에 뜬다.
            if (screenPoint.z <= 0f)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            PlaceRing(screenPoint);
            UpdateProgress();
        }

        private void PlaceRing(Vector3 screenPoint)
        {
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _referenceRect, screenPoint, uiCamera, out Vector2 local))
            {
                _ring.anchoredPosition = local;
            }
        }

        /// <summary>채우면서 좁아진다. 완성되면 색이 바뀐다.</summary>
        private void UpdateProgress()
        {
            float progress = _targeting.Progress;

            if (_fill != null)
            {
                _fill.fillAmount = progress;
            }

            float target = Mathf.Lerp(_startScale, 1f, progress);
            _scale = _scaleDamping > 0f
                ? Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-_scaleDamping * Time.deltaTime))
                : target;

            _ring.localScale = _baseScale * _scale;

            bool locked = _targeting.HasLock;
            if (locked == _wasLocked)
            {
                return;
            }

            _wasLocked = locked;

            if (_fill != null)
            {
                _fill.color = locked ? _lockedColor : _seekingColor;
            }

            if (_lockedFlourish != null)
            {
                _lockedFlourish.SetActive(locked);
            }
        }

        private void SetVisible(bool visible) => _group.alpha = visible ? 1f : 0f;
    }
}
