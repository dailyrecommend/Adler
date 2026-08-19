using Adler.Combat;
using Adler.Flight;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.DevTools
{
    /// <summary>
    /// 키 하나로 기체에 피해를 준다. 수치를 맞춰보기 위한 시험용 장치다.
    /// <para>
    /// 대공포에 맞으러 가지 않고도 체력 게이지와 피격 연출, 수리 스킬을 확인할 수 있다.
    /// 대공포 피해를 임시로 낮춰두는 것보다 낫다 — 그쪽은 되돌리는 걸 잊기 쉽고,
    /// 잊은 채로 균형을 판단하게 된다.
    /// </para>
    /// <para>
    /// 조작 에셋에 넣지 않고 키보드를 직접 읽는다. 시험용 키가 조작 체계에 섞이면
    /// 나중에 정리할 때 무엇이 진짜 조작인지 구분이 안 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageTester : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("피해")]
        [Tooltip("한 번 누를 때 입는 피해량.")]
        [Min(0f)]
        [SerializeField] private float _damage = 10f;

        private void Awake() => _aircraft = AircraftRig.Resolve(this, _aircraft);

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.backspaceKey.wasPressedThisFrame)
            {
                return;
            }

            Health health = _aircraft != null ? _aircraft.Health : null;
            if (health == null || !health.IsAlive)
            {
                return;
            }

            // 장갑과 구조 관문을 지나치게 한다. 시험용 피해가 기체 장갑에 막히면
            // 무엇을 확인하려던 것인지 알 수 없게 된다.
            health.TakeDamage(new DamageInfo(
                _damage,
                float.MaxValue,
                float.MaxValue,
                transform.position,
                Vector3.up,
                gameObject));
#endif
        }
    }
}
