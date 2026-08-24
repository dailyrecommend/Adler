using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Effects
{
    /// <summary>
    /// 맞은 자리에 이펙트를 띄운다. <see cref="Health"/>이 있는 것이면 무엇이든 붙는다.
    /// <para>
    /// 쏜 쪽이 아니라 맞은 쪽에서 띄운다. <see cref="DamageInfo"/>가 맞은 지점과 면의
    /// 법선을 함께 들고 오므로 여기서 이미 모든 것을 알고 있고, 쏜 쪽을 거치면 무기를
    /// 하나 늘릴 때마다 그쪽에도 이펙트를 붙여야 한다.
    /// </para>
    /// <para>
    /// 발마다 띄우지 않는다. 기총은 분당 천 발이 넘게 나가므로 한 발마다 오브젝트를
    /// 만들면 초당 스물다섯 개가 생겼다 사라지고, 그 뒷정리로 화면이 끊긴다. 맞고 있다는
    /// 사실은 몇 번째 발인지와 무관하게 전해지므로 솎아내도 잃는 것이 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitEffect : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [Tooltip("비워두면 이 오브젝트에서 찾는다.")]
        [SerializeField] private Health _health;

        [Header("이펙트")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("띄운 뒤 이만큼 지나면 지운다(초). 이펙트 재생 시간보다 넉넉히 둘 것.")]
        [Min(0.05f)]
        [SerializeField] private float _lifetime = 1f;

        [Tooltip("이 시간 안에는 다시 띄우지 않는다(초).\n\n" +
                 "0으로 두면 맞는 발마다 띄운다. 기총에 긁히는 동안에는 초당 스무 개가\n" +
                 "넘게 생겼다 사라져서, 타격감이 아니라 프레임 저하로 돌아온다.")]
        [Min(0f)]
        [SerializeField] private float _minInterval = 0.05f;

        [Header("자리")]
        [Tooltip("맞은 면이 바라보는 쪽으로 이펙트를 세운다.\n" +
                 "파편이 튀는 이펙트라면 켜둘 것 — 껍데기를 파고드는 것이 아니라 튀어나와야 한다.")]
        [SerializeField] private bool _alignToSurface = true;

        [Tooltip("맞은 대상에 매달아 함께 움직이게 한다.\n\n" +
                 "빠르게 나는 기체는 이펙트가 끝날 때까지 수십 미터를 지나가므로, 세계에\n" +
                 "가만히 두면 불꽃이 기체 뒤에 줄줄이 남는다.")]
        [SerializeField] private bool _attach = true;

        private Clock _clock;
        private float _nextAt;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_health == null || _prefab == null)
            {
                Debug.LogError($"{nameof(HitEffect)}: {nameof(Health)} 또는 프리팹이 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable() => _health.Damaged += OnDamaged;

        private void OnDisable() => _health.Damaged -= OnDamaged;

        private void OnDamaged(Health health, DamageInfo damage)
        {
            if (_clock.Now < _nextAt)
            {
                return;
            }

            _nextAt = _clock.Now + _minInterval;

            // 법선이 비어 있을 수 있다. 위를 향하게 두면 적어도 껍데기 안으로는 안 박힌다.
            Quaternion rotation = _alignToSurface && damage.Normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(damage.Normal)
                : Quaternion.identity;

            GameObject instance = Instantiate(_prefab, damage.Point, rotation);

            if (_attach)
            {
                instance.transform.SetParent(transform, worldPositionStays: true);
            }

            Destroy(instance, _lifetime);
        }
    }
}
