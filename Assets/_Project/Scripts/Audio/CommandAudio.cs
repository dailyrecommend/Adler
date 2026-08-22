using System.Collections.Generic;
using Adler.Flight;
using Adler.Weapons;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 커맨드를 치는 동안의 소리.
    /// <para>
    /// 한 칸 나아갈 때마다 음이 올라간다. 몇 번째를 쳤는지 화면을 보지 않고도 알 수
    /// 있게 하려는 것이다 — 커맨드는 조종을 하면서 치는 것이라, 확인하려고 눈을
    /// 내리면 그동안 기체가 어디로 가는지 모르게 된다.
    /// </para>
    /// <para>
    /// 어긋났을 때와 완성됐을 때도 서로 다른 소리를 낸다. 셋이 다르면 창을 안 보고도
    /// 지금 무슨 일이 일어났는지 구분된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandAudio : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("소리")]
        [Tooltip("소리가 나올 소스. Loop은 끄고 Play On Awake도 끌 것.")]
        [SerializeField] private AudioSource _source;

        [Tooltip("방향키가 한 번 들어갔을 때. 짧고 건조한 소리가 맞다.")]
        [SerializeField] private AudioClip _arrow;

        [Tooltip("커맨드가 완성됐을 때. 비워둬도 된다.")]
        [SerializeField] private AudioClip _accepted;

        [Tooltip("입력이 어긋났거나 쓸 수 없어 거절됐을 때. 비워둬도 된다.")]
        [SerializeField] private AudioClip _failed;

        [Header("음높이")]
        [Tooltip("첫 칸의 음높이.")]
        [Range(0.1f, 3f)]
        [SerializeField] private float _basePitch = 1f;

        [Tooltip("한 칸 나아갈 때마다 올라가는 폭.\n" +
                 "0.06이면 네 칸짜리 커맨드가 1.0에서 1.18까지 오른다 —\n" +
                 "크게 잡으면 긴 커맨드의 끝이 삑삑거려 우스워진다.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _pitchStep = 0.06f;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.5f;

        private StratagemBay _stratagems;

        // 지금까지 몇 칸 쳤는지. 헛되이 실패음을 내지 않으려고 들고 있는다.
        private int _entered;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _stratagems = _aircraft != null ? _aircraft.Stratagems : null;

            if (_stratagems == null || _source == null)
            {
                Debug.LogError($"{nameof(CommandAudio)}: 스트라타젬 또는 Audio Source가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _source.loop = false;
            _source.playOnAwake = false;
        }

        private void OnEnable()
        {
            _stratagems.CommandProgressed += OnProgressed;
            _stratagems.CommandReset += OnReset;
            _stratagems.Authorized += OnAuthorized;
            _stratagems.Refused += OnRefused;
        }

        private void OnDisable()
        {
            _stratagems.CommandProgressed -= OnProgressed;
            _stratagems.CommandReset -= OnReset;
            _stratagems.Authorized -= OnAuthorized;
            _stratagems.Refused -= OnRefused;
        }

        private void OnProgressed(IReadOnlyList<CommandDirection> entered)
        {
            _entered = entered.Count;
            Play(_arrow, _basePitch + (_pitchStep * (entered.Count - 1)));
        }

        /// <summary>
        /// 처음으로 되돌아갔을 때.
        /// <para>
        /// 친 것이 있을 때만 실패로 친다. 이 신호는 창을 닫거나 봉인에 걸릴 때도 오는데,
        /// 아무것도 안 친 상태에서 창을 닫을 때마다 실패음이 나면 닫는 것이 잘못된
        /// 행동처럼 들린다.
        /// </para>
        /// </summary>
        private void OnReset()
        {
            if (_entered > 0)
            {
                Play(_failed, 1f);
            }

            _entered = 0;
        }

        /// <summary>
        /// 승인됐을 때. 되돌림 신호보다 먼저 오므로 여기서 셈을 지워야 한다.
        /// 그러지 않으면 성공한 직후에 실패음이 따라 나온다.
        /// </summary>
        private void OnAuthorized(StratagemDefinition stratagem)
        {
            _entered = 0;
            Play(_accepted, 1f);
        }

        private void OnRefused(StratagemDefinition stratagem)
        {
            _entered = 0;
            Play(_failed, 1f);
        }

        private void Play(AudioClip clip, float pitch)
        {
            if (clip == null)
            {
                return;
            }

            _source.pitch = pitch;
            _source.PlayOneShot(clip, _volume);
        }
    }
}
