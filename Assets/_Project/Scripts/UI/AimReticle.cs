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
        [SerializeField] private RectTransform _reticle;

        [Header("판정")]
        [Tooltip("조준선이 닿는 것으로 볼 레이어. AircraftGun의 Hit Mask와 맞추면 된다.")]
        [SerializeField] private LayerMask _aimMask = ~0;

        [Tooltip("아무것도 없을 때 조준점을 놓을 거리 (m).")]
        [Min(1f)]
        [SerializeField] private float _restingDistance = 150f;

        [Tooltip("조준선이 화면 뒤로 넘어가면 조준점을 숨긴다.")]
        [SerializeField] private bool _hideWhenBehind = true;

        /// <summary>조준선에 걸린 표적이 바뀔 때. 조준점 색을 바꾸는 데 쓴다.</summary>
        public event Action<IDamageable> TargetChanged;

        private IDamageable _target;

        /// <summary>지금 조준선에 걸린 표적. 없으면 null.</summary>
        public IDamageable Target => _target;

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
            }
        }

        private void LateUpdate()
        {
            // 기체와 카메라가 모두 자리를 잡은 뒤에 읽어야 조준점이 한 프레임 늦지 않는다.
            Vector3 aimPoint = ResolveAimPoint(out IDamageable hitTarget);
            SetTarget(hitTarget);

            Vector3 screenPoint = _camera.WorldToScreenPoint(aimPoint);

            if (_hideWhenBehind && screenPoint.z <= 0f)
            {
                _reticle.gameObject.SetActive(false);
                return;
            }

            if (!_reticle.gameObject.activeSelf)
            {
                _reticle.gameObject.SetActive(true);
            }

            _reticle.position = screenPoint;
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
