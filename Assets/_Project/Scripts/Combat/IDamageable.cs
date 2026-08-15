namespace Adler.Combat
{
    /// <summary>
    /// 맞을 수 있는 것. 보병, 차량, 건물, 나중에는 플레이어 기체까지 이 경계면을 공유한다.
    /// <para>
    /// 무기는 무엇을 맞혔는지 알 필요가 없다. 총알이 보병용과 차량용으로 갈라지기 시작하면
    /// 무기 종류 × 표적 종류만큼 경우의 수가 늘어난다.
    /// </para>
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>
        /// 피해를 전달하고 결과를 돌려준다. 통했는지는 받는 쪽이 판단하지만,
        /// 그 판단은 쏜 쪽이 화면에 무엇을 띄울지 정하는 데 필요하다.
        /// </summary>
        DamageResult TakeDamage(in DamageInfo damage);
    }
}
