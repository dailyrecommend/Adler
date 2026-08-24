using UnityEngine;

namespace Adler.Abilities
{
    /// <summary>기총 사격의 수치. 도는 방식은 <see cref="FireGunAbility"/>에 있다.</summary>
    [CreateAssetMenu(fileName = "FireGun", menuName = "Adler/Abilities/Fire Gun")]
    public sealed class FireGunSpec : AbilitySpec
    {
        public override Ability Create() => new FireGunAbility(this);
    }
}
