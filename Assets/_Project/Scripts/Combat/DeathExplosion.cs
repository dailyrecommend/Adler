using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 쓰러지는 자리에 폭발을 남긴다.
    /// <para>
    /// 이펙트 프리팹에 스스로 사라지는 처리를 넣지 않고 여기서 시간을 재 지운다.
    /// 이펙트 쪽에 넣으면 재생 시간이 바뀔 때마다 프리팹을 열어 값을 맞춰야 하고,
    /// 폭발을 쓰는 곳이 여럿이면 그 수만큼 반복해야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathExplosion : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [Tooltip("비워두면 이 오브젝트에서 찾는다.")]
        [SerializeField] private Health _health;

        [Header("폭발")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("이 자리에 스폰한다. 비워두면 이 오브젝트의 위치를 쓴다.")]
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("스폰한 뒤 이만큼 지나면 지운다(초). 이펙트 재생 시간보다 넉넉히 둔다.")]
        [Min(0.1f)]
        [SerializeField] private float _lifetime = 3f;

        private void Awake()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_health == null)
            {
                Debug.LogError($"{nameof(DeathExplosion)}: 지켜볼 {nameof(Health)}이 없습니다.", this);
                enabled = false;
                return;
            }

            if (_prefab == null)
            {
                Debug.LogError($"{nameof(DeathExplosion)}: 폭발 프리팹이 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable() => _health.Died += OnDied;

        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied(Health health, DamageInfo damage)
        {
            Vector3 position = _spawnPoint != null ? _spawnPoint.position : transform.position;
            GameObject instance = Instantiate(_prefab, position, Quaternion.identity);
            Destroy(instance, _lifetime);
        }
    }
}
