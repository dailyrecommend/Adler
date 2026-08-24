using System;
using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 기체의 상태에 맞춰 소리를 켜고 끈다.
    /// <para>
    /// 상태가 이어지는 소리는 늘 셋으로 온다 ─ 켜질 때 한 번, 이어지는 동안, 꺼질 때
    /// 한 번. 부스터도 그래플도 시간 정지도 같은 모양이고 다른 것은 <b>무엇을 보느냐</b>
    /// 뿐이다. 기능마다 컴포넌트를 두면 걸치는 방식을 고칠 때 그 수만큼 고쳐야 하고,
    /// 소리가 늘수록 인스펙터가 같은 모양의 칸으로 채워진다.
    /// </para>
    /// <para>
    /// 조건은 화면 효과·이펙트와 같은 것을 쓴다. 내는 방식은 달라도 <b>언제 내는가</b>는
    /// 같은 질문이라, 답을 각자 갖고 있으면 조건을 더할 때 세 곳이 어긋난다.
    /// </para>
    /// <para>
    /// 한 방으로 끝나는 소리 ─ 피격, 격추, 커맨드 입력 ─ 는 여기 오지 않는다. 그쪽은
    /// 이어지는 상태가 아니라 일어난 사건이라, 보고 있을 조건이 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftSounds : MonoBehaviour
    {
        [Serializable]
        public struct Cue
        {
            [Tooltip("무엇을 보고 낼지.")]
            public AircraftCondition When;

            [Tooltip("Debuff를 고른 경우에만 쓴다. 어느 디버프인지.")]
            public DebuffDefinition Debuff;

            [Tooltip("걸리는 순간 한 번. 비워둬도 된다.")]
            public AudioClip Start;

            [Tooltip("걸려 있는 동안 되풀이될 소리. 비워두면 이어지는 소리가 없다.")]
            public AudioClip Loop;

            [Tooltip("풀리는 순간 한 번. 비워둬도 된다.")]
            public AudioClip End;

            [Tooltip("이어지는 소리가 나올 소스. Loop을 채웠을 때만 필요하다.\n\n" +
                     "줄마다 따로 두는 이유는 한 소스가 하나만 되풀이할 수 있기 때문이다 —\n" +
                     "부스터와 시간 정지가 겹치면 뒤엣것이 앞엣것을 밀어낸다.")]
            public AudioSource LoopSource;

            [Range(0f, 1f)]
            [Tooltip("이 줄의 크기.")]
            public float Volume;

            [Tooltip("이어지는 소리가 차오르고 잦아드는 시간(초).\n" +
                     "0이면 딱 끊긴다. 긴 소리일수록 조금 줘야 뚝 끊긴 것으로 들리지 않는다.")]
            [Min(0f)]
            public float FadeSeconds;
        }

        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("소리")]
        [Tooltip("한 번짜리 소리가 나올 소스. Loop은 끄고 Play On Awake도 끌 것.\n" +
                 "여러 줄이 함께 쓴다 — PlayOneShot은 서로를 끊지 않는다.")]
        [SerializeField] private AudioSource _oneShotSource;

        [SerializeField] private List<Cue> _cues = new();

        // 지난 프레임의 상태. 걸리고 풀리는 순간을 잡아 한 번짜리를 낸다.
        private readonly List<bool> _on = new();

        // 이어지는 소리가 차오른 정도(0~1). 페이드가 끝나야 소스를 멈춘다.
        private readonly List<float> _level = new();

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(AircraftSounds)}: 기체를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (_oneShotSource != null)
            {
                _oneShotSource.loop = false;
                _oneShotSource.playOnAwake = false;
            }

            foreach (Cue cue in _cues)
            {
                _on.Add(false);
                _level.Add(0f);

                // 에디터에서 들어보다 켜둔 채 저장하면, 조건이 참이 되기 전부터
                // 나오는 이유를 알 수 없다. 시작할 때 확실히 꺼둔다.
                if (cue.LoopSource != null)
                {
                    cue.LoopSource.loop = true;
                    cue.LoopSource.playOnAwake = false;
                    cue.LoopSource.volume = 0f;
                    cue.LoopSource.Stop();
                }
            }
        }

        private void OnDisable()
        {
            // 꺼질 때 되풀이되던 것을 남겨두면 기체가 사라진 뒤에도 소리만 남는다.
            //
            // 줄 수가 아니라 준비된 만큼만 훑는다. 기체를 못 찾아 Awake에서 스스로
            // 꺼진 경우에는 아직 아무것도 준비되지 않은 채로 여기에 온다.
            for (int i = 0; i < _on.Count; i++)
            {
                Silence(i);
            }
        }

        private void Update()
        {
            float delta = _clock.Delta;

            // 준비된 만큼만 훑는다. 실행 중에 인스펙터에서 줄을 더해도 어긋나지 않는다.
            for (int i = 0; i < _on.Count; i++)
            {
                Cue cue = _cues[i];
                bool on = AircraftConditions.IsMet(_aircraft, cue.When, cue.Debuff);

                if (on != _on[i])
                {
                    _on[i] = on;
                    Announce(in cue, on);
                }

                Fade(i, in cue, on, delta);
            }
        }

        /// <summary>걸리거나 풀리는 그 순간에만 한 번 낸다.</summary>
        private void Announce(in Cue cue, bool on)
        {
            AudioClip clip = on ? cue.Start : cue.End;

            if (clip != null && _oneShotSource != null)
            {
                _oneShotSource.PlayOneShot(clip, cue.Volume);
            }

            // 걸리는 순간 되풀이를 시작한다. 크기는 0에서 올라가므로 여기서 틀어도
            // 갑자기 튀지 않는다.
            if (on && cue.Loop != null && cue.LoopSource != null && !cue.LoopSource.isPlaying)
            {
                cue.LoopSource.clip = cue.Loop;
                cue.LoopSource.volume = 0f;
                cue.LoopSource.Play();
            }
        }

        /// <summary>
        /// 이어지는 소리를 차오르게 하고 잦아들게 한다.
        /// <para>
        /// 다 잦아든 뒤에 멈춘다. 풀리는 즉시 멈추면 페이드를 준 뜻이 없고, 멈추지 않고
        /// 두면 들리지도 않는 소리가 소스를 계속 쥐고 있어 다음에 걸릴 때 이어서 난다.
        /// </para>
        /// </summary>
        private void Fade(int index, in Cue cue, bool on, float delta)
        {
            AudioSource source = cue.LoopSource;

            if (source == null || cue.Loop == null)
            {
                return;
            }

            float target = on ? 1f : 0f;
            float level = _level[index];

            level = cue.FadeSeconds > 0f
                ? Mathf.MoveTowards(level, target, delta / cue.FadeSeconds)
                : target;

            _level[index] = level;
            source.volume = level * cue.Volume;

            if (level <= 0f && source.isPlaying)
            {
                source.Stop();
            }
        }

        private void Silence(int index)
        {
            AudioSource source = _cues[index].LoopSource;

            _on[index] = false;
            _level[index] = 0f;

            if (source != null)
            {
                source.volume = 0f;
                source.Stop();
            }
        }
    }
}
