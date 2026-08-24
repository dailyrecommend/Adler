using Adler.Abilities;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 기체가 무언가를 내놓는 자리.
    /// <para>
    /// 폭탄이든 조명탄이든 어느 지점에서 나가야 하는데, 그 지점이 어디인지는 기체마다
    /// 다르다. 행동이 이름으로 찾으면 기체의 구조를 알아야 하므로, 어느 트랜스폼이
    /// 그 자리인지는 기체 쪽에서 정해 넘긴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftHardpoint : MonoBehaviour, IHardpoint
    {
        [Tooltip("내놓을 자리와 방향. 비워두면 이 오브젝트를 쓴다.")]
        [SerializeField] private Transform _mount;

        [Tooltip("속도를 읽어올 기체. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        /// <inheritdoc />
        public Transform Mount => _mount != null ? _mount : transform;

        /// <inheritdoc />
        public Vector3 Velocity =>
            _aircraft != null && _aircraft.Body != null ? _aircraft.Body.linearVelocity : Vector3.zero;

        private void Awake() => _aircraft = AircraftRig.Resolve(this, _aircraft);
    }
}
