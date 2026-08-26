using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 배경음악을 틀고 이어 돌린다.
    /// <para>
    /// 한 곡이면 그 곡을 이음새 없이 돈다. 여러 곡이면 차례로 돌되, 곡과 곡 사이를
    /// 겹쳐 섞는다 — 뚝 끊기고 다음 곡이 시작하면 그 정적이 사건처럼 들려서,
    /// 아무 일도 없는데 화면을 살피게 된다.
    /// </para>
    /// <para>
    /// 게임 시계를 쓰지 않는다. 히트스톱과 프레임락은 세상이 멎는 연출이지 음악이
    /// 멎는 연출이 아니다 — 오히려 멎은 화면 위로 음악이 이어져야 연출로 읽힌다.
    /// </para>
    /// <para>
    /// 소스는 스스로 만든다. 두 개가 필요한 사정(겹쳐 섞기)은 이 클래스의 속사정이라,
    /// 인스펙터에서 이어줄 것이 곡 목록 말고는 없어야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [Header("곡")]
        [Tooltip("돌릴 곡들. 하나면 그 곡을 돌고, 여럿이면 차례로 돈 뒤 처음으로 돌아온다.")]
        [SerializeField] private AudioClip[] _tracks = System.Array.Empty<AudioClip>();

        [Header("소리")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.4f;

        [Tooltip("시작할 때 차오르는 시간(초). 첫 프레임부터 제 소리로 나오면 놀란다.")]
        [Min(0f)]
        [SerializeField] private float _fadeInSeconds = 2f;

        [Tooltip("곡과 곡이 겹치는 시간(초). 한 곡뿐이면 쓰이지 않는다.")]
        [Min(0f)]
        [SerializeField] private float _crossfadeSeconds = 3f;

        // 둘이 번갈아 곡을 문다. 하나가 끝나갈 때 다른 하나가 미리 시작해야 겹친다.
        private AudioSource _front;
        private AudioSource _back;

        private int _index;
        private float _fade;

        private void Awake()
        {
            _front = Build();
            _back = Build();

            if (_tracks.Length == 0)
            {
                Debug.LogWarning($"{nameof(MusicPlayer)}: 곡이 없습니다.", this);
                enabled = false;
            }
        }

        private AudioSource Build()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;

            return source;
        }

        private void Start()
        {
            _front.clip = _tracks[0];

            // 한 곡뿐이면 소스의 루프에 맡긴다. 손으로 다시 트는 것보다 이음새가 없다.
            _front.loop = _tracks.Length == 1;
            _front.Play();

            _fade = _fadeInSeconds > 0f ? 0f : 1f;
        }

        private void Update()
        {
            // 멎은 세상 위로도 흘러야 하므로 바깥 시간으로 잰다.
            float delta = Time.unscaledDeltaTime;

            if (_fade < 1f)
            {
                _fade = _fadeInSeconds > 0f
                    ? Mathf.Min(1f, _fade + (delta / _fadeInSeconds))
                    : 1f;
            }

            Advance(delta);

            _front.volume = _volume * _fade;
        }

        /// <summary>
        /// 곡이 끝나갈 무렵 다음 곡을 미리 틀고, 끝난 곡의 자리와 바꾼다.
        /// </summary>
        private void Advance(float delta)
        {
            if (_tracks.Length < 2 || _front.clip == null)
            {
                return;
            }

            float remaining = _front.clip.length - _front.time;
            float overlap = Mathf.Min(_crossfadeSeconds, _front.clip.length * 0.5f);

            // 물러나는 곡은 겹치는 동안 잦아든다.
            if (_back.isPlaying)
            {
                _back.volume = Mathf.Max(0f, _back.volume - (delta / Mathf.Max(0.01f, overlap)) * _volume);

                if (!_back.isPlaying || _back.volume <= 0f)
                {
                    _back.Stop();
                }
            }

            if (remaining > overlap && _front.isPlaying)
            {
                return;
            }

            // 다음 곡. 지금 곡은 뒷자리로 물러나 잦아들고, 새 곡이 앞자리를 받는다.
            _index = (_index + 1) % _tracks.Length;

            (_front, _back) = (_back, _front);

            _front.clip = _tracks[_index];
            _front.loop = false;
            _front.Play();

            // 들어오는 쪽도 차오른다. 물러나는 곡만 잦아들면 새 곡이 제 소리로
            // 툭 들어와서, 섞이는 것이 아니라 끼어드는 것으로 들린다.
            if (_fadeInSeconds > 0f)
            {
                _fade = 0f;
            }
        }

#if UNITY_EDITOR
        /// <summary>실행 중에 소리 크기를 돌리면 바로 들리게 한다.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying && _front != null)
            {
                _front.volume = _volume * _fade;
            }
        }
#endif
    }
}
