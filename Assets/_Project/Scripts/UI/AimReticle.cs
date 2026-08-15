using System;
using Adler.Combat;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 조준점을 탄이 실제로 닿을 자리에 옮겨 놓는다.
    /// <para>
    /// 총구는 카메라와 다른 자리에 있고 기체는 계속 기울어지므로, 화면 정중앙은
    /// 탄착점이 아니다. 고정된 조준점으로 쏘면 조금씩 빗나가는데 이유를 알 수 없다.
    /// </para>
    /// <para>
    /// 조준점을 화면 중앙에 고정하고 싶다면 이 컴포넌트를 쓰지 않으면 된다.
    /// 그 경우 총구를 카메라 시선과 나란히 두어야 조준이 맞는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AimReticle : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [Tooltip("탄이 나가는 자리. AircraftGun에 지정한 총구와 같은 것을 넣는다.")]
        [SerializeField] private Transform _muzzle;

        [Tooltip("비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Camera _camera;

        [Header("옮길 요소")]
        [Tooltip("앵커를 가운데(0.5, 0.5)로 둘 것. 화면 좌표를 그 기준으로 환산해 넣는다.")]
        [SerializeField] private RectTransform _reticle;

        [Tooltip("비워두면 조준점이 속한 캔버스를 찾아 쓴다.")]
        [SerializeField] private Canvas _canvas;

        [Header("판정")]
        [Tooltip("조준선이 닿는 것으로 볼 레이어. AircraftGun의 Hit Mask와 맞추면 된다.")]
        [SerializeField] private LayerMask _aimMask = ~0;

        [Tooltip("아무것도 없을 때 조준점을 놓을 거리 (m).")]
        [Min(1f)]
        [SerializeField] private float _restingDistance = 150f;

        [Tooltip("조준선이 화면 뒤로 넘어가면 조준점을 숨긴다.\n" +
                 "둘러보기로 뒤를 볼 때처럼, 총구가 화면 밖을 향하는 경우다.")]
        [SerializeField] private bool _hideWhenBehind = true;

        /// <summary>조준선에 걸린 표적이 바뀔 때. 조준점 색을 바꾸는 데 쓴다.</summary>
        public event Action<IDamageable> TargetChanged;

        private IDamageable _target;

        /// <summary>지금 조준선에 걸린 표적. 없으면 null.</summary>
        public IDamageable Target => _target;

        private RectTransform _referenceRect;
        private CanvasGroup _reticleGroup;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_muzzle == null || _reticle == null || _camera == null)
            {
                Debug.LogError($"{nameof(AimReticle)}: Muzzle, Reticle, Camera 중 빠진 것이 있습니다.", this);
                enabled = false;
                return;
            }

            if (_canvas == null)
            {
                _canvas = _reticle.GetComponentInParent<Canvas>();
            }

            if (_canvas == null)
            {
                Debug.LogError($"{nameof(AimReticle)}: 조준점이 캔버스 안에 있지 않습니다.", this);
                enabled = false;
                return;
            }

            // 조준점을 담고 있는 사각형이 좌표 환산의 기준이다.
            _referenceRect = _reticle.parent as RectTransform
                             ?? _canvas.transform as RectTransform;

            // 숨길 때 오브젝트를 끄지 않고 투명하게 만든다.
            // 이 스크립트가 조준점 자신이나 그 자식에 붙어 있으면, 오브젝트를 끄는 순간
            // 스크립트도 함께 멈춰서 다시 켜줄 코드가 영영 돌지 않는다.
            _reticleGroup = _reticle.GetComponent<CanvasGroup>();
            if (_reticleGroup == null)
            {
                _reticleGroup = _reticle.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void LateUpdate()
        {
            // 기체와 카메라가 모두 자리를 잡은 뒤에 읽어야 조준점이 한 프레임 늦지 않는다.
            Vector3 aimPoint = ResolveAimPoint(out IDamageable hitTarget);
            SetTarget(hitTarget);

            Vector3 screenPoint = _camera.WorldToScreenPoint(aimPoint);

            // z는 화면상의 깊이가 아니라 카메라로부터의 거리다. 음수면 조준선이 등 뒤를
            // 향하고 있다는 뜻이고, 이때 x와 y는 뒤집힌 값이라 그대로 쓸 수 없다.
            if (screenPoint.z <= 0f)
            {
                if (_hideWhenBehind)
                {
                    SetVisible(false);
                    return;
                }

                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            SetVisible(true);

            // 화면 좌표를 캔버스 좌표로 환산한다. 화면 좌표를 position에 그대로 넣으면
            // z(카메라까지의 거리)가 함께 들어가 조준점이 캔버스 평면에서 벗어난다.
            // 겨냥한 것이 멀수록 심해져서, 하늘을 향하면 조준점이 사라진다.
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _referenceRect, screenPoint, uiCamera, out Vector2 local))
            {
                _reticle.anchoredPosition = local;
            }
        }

        private void SetVisible(bool visible)
        {
            _reticleGroup.alpha = visible ? 1f : 0f;
        }

        private Vector3 ResolveAimPoint(out IDamageable target)
        {
            Ray ray = new Ray(_muzzle.position, _muzzle.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _restingDistance,
                    _aimMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<IDamageable>();
                return hit.point;
            }

            target = null;
            return ray.GetPoint(_restingDistance);
        }

        private void SetTarget(IDamageable target)
        {
            if (ReferenceEquals(target, _target))
            {
                return;
            }

            _target = target;
            TargetChanged?.Invoke(target);
        }
    }
}
