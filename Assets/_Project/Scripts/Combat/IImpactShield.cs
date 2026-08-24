using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 이번 충돌만큼은 피해를 받지 않게 막아주는 것.
    /// <para>
    /// 충돌 피해 쪽이 "언제 면제인가"를 직접 알지 않게 하려고 둔다. 조건을 그쪽에
    /// 적어 넣으면 부스터를 알아야 하고, 그러면 전투 계층이 비행 계층을 향해 거슬러
    /// 올라간다 — 다음에 면제가 하나 더 생기면 또 그쪽을 고쳐야 한다.
    /// </para>
    /// <para>
    /// 막는 쪽이 자기 사정을 알고 스스로 손을 든다. 충돌 피해는 손이 올라왔는지만
    /// 묻는다.
    /// </para>
    /// </summary>
    public interface IImpactShield
    {
        /// <summary>이 충돌을 막을지. 참이면 그 충돌로는 피해를 받지 않는다.</summary>
        bool Blocks(Collision collision);
    }
}
