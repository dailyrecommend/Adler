using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 폭발 한 번이 남긴 결과를 뭉뚱그린 것.
    /// <para>
    /// 폭발은 한 번에 여러 표적을 때리므로, 명중 하나하나를 알려주면 화면 표시가
    /// 같은 순간에 여러 번 뜬다. 한 번의 폭발이 무엇을 했는지만 알면 충분하다.
    /// </para>
    /// </summary>
    public readonly struct BlastReport
    {
        /// <summary>피해가 들어간 표적 수.</summary>
        public readonly int Damaged;

        /// <summary>장갑이나 구조에 막힌 표적 수.</summary>
        public readonly int Blocked;

        /// <summary>쓰러뜨린 표적 수.</summary>
        public readonly int Killed;

        public readonly Vector3 Position;

        public BlastReport(int damaged, int blocked, int killed, Vector3 position)
        {
            Damaged = damaged;
            Blocked = blocked;
            Killed = killed;
            Position = position;
        }

        /// <summary>아무것도 못 맞혔다. 빈 하늘에 터진 경우.</summary>
        public bool MissedEverything => Damaged == 0 && Blocked == 0;
    }
}
