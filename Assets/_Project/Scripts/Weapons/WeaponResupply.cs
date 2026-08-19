using Adler.Flight;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 재보급 요청이 승인되면 들고 있는 무기들을 채운다.
    /// <para>
    /// 무기 하나가 아니라 전부를 채운다. 기총만 채워지면 미사일이 떨어졌을 때 부를 방법이
    /// 없고, 어느 것이 채워지는지 플레이어가 외워야 한다.
    /// </para>
    /// <para>
    /// 쿨타임과 횟수 제한은 여기서 보지 않는다. 부를 수 있는지는 <see cref="StratagemBay"/>가
    /// 이미 판단했고, 승인이 왔다는 것은 통과했다는 뜻이다. 양쪽에서 각자 세면 화면에
    /// 표시된 쿨타임과 실제 동작이 어긋나기 시작한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponResupply : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        private void Awake() => _aircraft = AircraftRig.Resolve(this, _aircraft);

        private void OnEnable()
        {
            if (_aircraft != null && _aircraft.Stratagems != null)
            {
                _aircraft.Stratagems.Authorized += OnAuthorized;
            }
        }

        private void OnDisable()
        {
            if (_aircraft != null && _aircraft.Stratagems != null)
            {
                _aircraft.Stratagems.Authorized -= OnAuthorized;
            }
        }

        private void OnAuthorized(StratagemDefinition stratagem)
        {
            if (stratagem is ResupplyDefinition resupply)
            {
                _aircraft.Weapons?.ResupplyAll(resupply.RefillPercent);
            }
        }
    }
}
