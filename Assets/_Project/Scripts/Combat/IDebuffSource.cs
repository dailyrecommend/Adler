using System.Collections.Generic;

namespace Adler.Combat
{
    /// <summary>
    /// 자기가 거는 나쁜 상태를 스스로 알리는 시스템.
    /// <para>
    /// 봉인은 스트라타젬이, 과열은 무기가, 연료 부족은 부스터가 안다. 그 상태를 이미
    /// 알고 있는 쪽이 직접 내놓게 하면, 중간에서 옮겨주는 컴포넌트가 필요 없다.
    /// </para>
    /// <para>
    /// 걸린 것만 담는다. 매 프레임 물어보므로 언제 켜지고 꺼지는지는 신경 쓰지 않아도
    /// 되고, 지금 상태만 정직하게 답하면 된다.
    /// </para>
    /// </summary>
    public interface IDebuffSource
    {
        void CollectDebuffs(List<DebuffDefinition> into);
    }
}
