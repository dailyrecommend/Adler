using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 한 번의 명중이 전달하는 내용.
    /// <para>
    /// 위력을 숫자 하나로 두지 않는다. 무기가 표적에게 통하는지는 서로 다른 두 가지 관문으로
    /// 갈리기 때문이다 — 장갑을 뚫는 <see cref="Penetration"/>과 건물을 무너뜨리는
    /// <see cref="Demolition"/>. 둘은 독립적이다. 기총은 장갑차를 뚫어도 건물은 못 부수고,
    /// 폭탄은 건물을 무너뜨려도 장갑을 못 뚫을 수 있다.
    /// </para>
    /// </summary>
    public readonly struct DamageInfo
    {
        /// <summary>관문을 통과했을 때 들어가는 피해량.</summary>
        public readonly float Amount;

        /// <summary>관통력. 표적의 장갑 이상이어야 피해가 들어간다.</summary>
        public readonly float Penetration;

        /// <summary>철거력. 건물이 요구하는 수준 이상이어야 부술 수 있다.</summary>
        public readonly float Demolition;

        /// <summary>맞은 지점. 피격 효과를 띄울 자리.</summary>
        public readonly Vector3 Point;

        /// <summary>맞은 면의 법선. 파편이 튀는 방향.</summary>
        public readonly Vector3 Normal;

        /// <summary>쏜 주체. 누가 처치했는지 집계할 때 쓴다.</summary>
        public readonly GameObject Source;

        public DamageInfo(
            float amount,
            float penetration,
            float demolition,
            Vector3 point,
            Vector3 normal,
            GameObject source)
        {
            Amount = amount;
            Penetration = penetration;
            Demolition = demolition;
            Point = point;
            Normal = normal;
            Source = source;
        }
    }

    /// <summary>피해가 막힌 이유. 화면에 무엇을 띄울지가 이것으로 갈린다.</summary>
    public enum DamageRejection
    {
        /// <summary>막히지 않음.</summary>
        None = 0,

        /// <summary>관통력이 장갑에 못 미쳐 튕겨 나감.</summary>
        Armor = 1,

        /// <summary>철거력이 모자라 건물이 끄떡없음.</summary>
        Demolition = 2,
    }
}
