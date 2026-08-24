using System;
using System.Collections.Generic;
using Adler.Core;

namespace Adler.Weapons
{
    /// <summary>방향 입력 하나를 받은 결과.</summary>
    public enum CommandInput
    {
        /// <summary>어느 커맨드의 첫 칸도 되지 못해 버려졌다.</summary>
        Rejected = 0,

        /// <summary>치던 것과 어긋났지만, 이 입력을 첫 칸으로 다시 시작했다.</summary>
        Restarted = 1,

        /// <summary>한 칸 나아갔다.</summary>
        Progressed = 2,

        /// <summary>커맨드가 완성됐다. <see cref="CommandRecognizer.Completed"/>에 담긴다.</summary>
        Accepted = 3,
    }

    /// <summary>
    /// 방향 입력을 받아 어느 스트라타젬을 부르려는 것인지 알아낸다.
    /// <para>
    /// 입력 장치도 시간도 화면도 알지 못한다. 방향 하나를 넣으면 무슨 일이 일어났는지
    /// 돌려줄 뿐이라, 씬을 띄우지 않고도 그대로 시험할 수 있다. 커맨드 판정은 규칙이
    /// 촘촘해서 눈으로 확인하기 어려운 종류의 코드다 — 어긋났을 때 어디까지 되돌릴지,
    /// 쿨타임 중인 것을 후보에서 언제 빼는지 같은 것들이 그렇다.
    /// </para>
    /// <para>
    /// 쓸 수 있는지 여부는 밖에서 받는다. 쿨타임과 출격 횟수를 이쪽이 세기 시작하면
    /// 시간을 알아야 하고, 그 순간 시험할 수 없는 코드가 된다.
    /// </para>
    /// </summary>
    public sealed class CommandRecognizer
    {
        private readonly IReadOnlyList<StratagemDefinition> _loadout;
        private readonly Func<StratagemDefinition, bool> _isAvailable;

        private readonly List<CommandDirection> _entered = new();
        private readonly List<StratagemDefinition> _candidates = new();

        /// <param name="loadout">요청할 수 있는 목록. 밖에서 바뀌면 그대로 반영된다.</param>
        /// <param name="isAvailable">
        /// 지금 부를 수 있는지. 쿨타임에 걸린 것을 후보에서 빼는 데 쓴다.
        /// 비워두면 전부 부를 수 있는 것으로 본다.
        /// </param>
        public CommandRecognizer(
            IReadOnlyList<StratagemDefinition> loadout,
            Func<StratagemDefinition, bool> isAvailable = null)
        {
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _isAvailable = isAvailable;
        }

        /// <summary>지금까지 입력된 방향들.</summary>
        public IReadOnlyList<CommandDirection> Entered => _entered;

        /// <summary>지금 입력으로 아직 가능한 것들.</summary>
        public IReadOnlyList<StratagemDefinition> Candidates => _candidates;

        /// <summary>가장 최근에 완성된 것. 완성된 적이 없으면 null.</summary>
        public StratagemDefinition Completed { get; private set; }

        /// <summary>
        /// 방향 하나를 받는다.
        /// <para>
        /// 어긋나도 바로 버리지 않는다. 방금 누른 것을 새 커맨드의 첫 칸으로 다시 보는데,
        /// 그러지 않으면 한 번 틀렸을 때 손을 멈췄다 다시 시작해야 한다. 이어서 치는
        /// 도중에 다른 커맨드로 갈아타는 것도 이 규칙 덕분에 된다.
        /// </para>
        /// </summary>
        public CommandInput Accept(CommandDirection direction)
        {
            Completed = null;

            _entered.Add(direction);

            if (RefreshCandidates())
            {
                return FinishOrProgress(CommandInput.Progressed);
            }

            // 어긋났다. 방금 누른 것만 남기고 처음부터 다시 본다.
            _entered.Clear();
            _entered.Add(direction);

            if (RefreshCandidates())
            {
                return FinishOrProgress(CommandInput.Restarted);
            }

            _entered.Clear();
            _candidates.Clear();
            return CommandInput.Rejected;
        }

        /// <summary>치던 것을 버린다. 창을 닫거나 봉인에 걸렸을 때 부른다.</summary>
        public void Reset()
        {
            _entered.Clear();
            _candidates.Clear();
            Completed = null;
        }

        /// <summary>
        /// 후보 중 입력을 다 채운 것이 있으면 완성으로 친다.
        /// <para>
        /// 커맨드가 서로의 앞부분이 되는 경우 — 위위와 위위아래처럼 — 짧은 쪽이 먼저
        /// 완성된다. 길게 치려던 사람이 짧은 것을 받게 되므로, 커맨드를 정할 때
        /// 한쪽이 다른 쪽의 앞부분이 되지 않게 두는 편이 낫다.
        /// </para>
        /// </summary>
        private CommandInput FinishOrProgress(CommandInput ongoing)
        {
            foreach (StratagemDefinition candidate in _candidates)
            {
                if (candidate.Command.Length == _entered.Count)
                {
                    Completed = candidate;
                    return CommandInput.Accepted;
                }
            }

            return ongoing;
        }

        /// <summary>
        /// 지금까지의 입력으로 아직 가능한 것들을 추린다.
        /// <para>
        /// 쿨타임 중이거나 다 쓴 것은 후보에서 빠진다. 그래서 그 커맨드를 치기 시작하면
        /// 곧바로 어긋난 입력으로 처리되고, 끝까지 다 친 뒤에 거절당하는 일이 없다.
        /// </para>
        /// </summary>
        private bool RefreshCandidates()
        {
            _candidates.Clear();

            foreach (StratagemDefinition stratagem in _loadout)
            {
                if (stratagem == null || stratagem.Command.Length < _entered.Count)
                {
                    continue;
                }

                if (_isAvailable != null && !_isAvailable(stratagem))
                {
                    continue;
                }

                if (StartsWithEntered(stratagem))
                {
                    _candidates.Add(stratagem);
                }
            }

            return _candidates.Count > 0;
        }

        private bool StartsWithEntered(StratagemDefinition stratagem)
        {
            for (int i = 0; i < _entered.Count; i++)
            {
                if (stratagem.Command[i] != _entered[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
