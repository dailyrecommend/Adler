using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 갈고리의 무기로서의 성능. 줄의 물리는 <see cref="GrapplingHook"/>이 갖는다.
    /// <para>
    /// 여기 담기는 것은 자리·표시·탄처럼 무기라면 누구나 갖는 것들뿐이다. 던지는
    /// 속도나 유지 시간 같은 줄의 사정까지 끌어오면, 같은 물건의 수치가 에셋과
    /// 컴포넌트 두 곳에 갈라져 산다.
    /// </para>
    /// <para>
    /// 탄으로 세는 것이 쿨타임 노릇을 한다 — 한 발을 싣고 5초에 걸쳐 돌아오게 하면
    /// "5초 쿨타임"과 같은 말이 된다. 같은 제한을 다른 말로 두 번 적지 않는다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Grapple", menuName = "Adler/Weapons/Grapple Definition")]
    public sealed class GrappleDefinition : WeaponDefinition
    {
        /// <summary>에셋을 처음 만들 때의 기본값. 갈고리다운 숫자로 시작하게 한다.</summary>
        private void Reset()
        {
            Slot = WeaponSlot.Secondary;
            AmmoCapacity = 1;
            RechargeSeconds = 5f;
            RechargeDelay = 0f;
            ResumeRounds = 1;
        }
    }
}
