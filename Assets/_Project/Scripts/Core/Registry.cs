using System.Collections.Generic;

namespace Adler.Core
{
    /// <summary>
    /// 지금 살아 있는 같은 종류의 것들. 씬을 뒤지지 않고 서로를 찾는 데 쓴다.
    /// <para>
    /// 재머·조명탄·적기 기총이 똑같은 명단을 각자 들고 있었다. 명단 코드가 여섯 벌이면
    /// 등록을 다루는 방식을 고칠 때 여섯 곳을 고쳐야 하고, 새로 명단이 필요한 것은
    /// 일곱 번째 복사본을 만들게 된다.
    /// </para>
    /// <para>
    /// 형마다 명단이 따로다 — 제네릭 정적 필드는 형 인자별로 하나씩 생긴다.
    /// 올리고 내리는 것은 스스로 한다: OnEnable에서 올리고 <b>반드시</b> OnDisable에서
    /// 내린다. 내리지 않으면 사라진 것이 명단에 남아, 그것을 훑는 쪽이 유령을 상대한다.
    /// </para>
    /// </summary>
    public static class Registry<T> where T : class
    {
        private static readonly List<T> Items = new();

        /// <summary>지금 올라 있는 것들. 훑는 동안 올리거나 내리면 안 된다.</summary>
        public static IReadOnlyList<T> All => Items;

        public static void Add(T item)
        {
            if (item != null && !Items.Contains(item))
            {
                Items.Add(item);
            }
        }

        public static void Remove(T item) => Items.Remove(item);
    }
}
