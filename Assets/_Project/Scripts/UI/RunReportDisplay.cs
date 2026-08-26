using Adler.Audio;
using Adler.Flight;
using TMPro;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 판의 기록을 화면 요소에 꽂아준다. 죽으면 뜨고 되살아나면 사라진다.
    /// <para>
    /// 값이 한 번에 나오지 않는다. 숫자들이 무작위로 구르다가 시간 → 격추 → 스타일
    /// 순으로 하나씩 잠기고, 맨 끝에 최고 랭크가 찍힌다. 잠길 때마다 소리가 나고
    /// 뒤로 갈수록 커진다 — 순서와 소리가 함께 올라가야 마지막 랭크가 절정이 된다.
    /// </para>
    /// <para>
    /// 바깥 시간으로 돈다. 죽은 화면의 연출이라 게임 시계의 사정과 무관해야 한다.
    /// </para>
    /// <para>
    /// 모양은 건드리지 않는다. 만들어 둔 패널과 텍스트를 넣으면 값만 채우고,
    /// 비워둔 칸은 건너뛴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunReportDisplay : MonoBehaviour
    {
        // 잠기는 순서. 랭크가 마지막인 것이 이 연출의 요점이다.
        private const int TimeStep = 0;
        private const int KillsStep = 1;
        private const int StyleStep = 2;
        private const int RankStep = 3;
        private const int StepCount = 4;

        [Header("읽어올 대상")]
        [SerializeField] private RunReport _report;

        [Tooltip("되살아날 때 숨기기 위해 듣는다. 비워두면 기록 쪽 기체에서 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("패널")]
        [Tooltip("죽었을 때 통째로 켤 오브젝트. 평소에는 꺼둔다.")]
        [SerializeField] private GameObject _panel;

        [Header("글자")]
        [SerializeField] private TMP_Text _timeLabel;

        [Tooltip("{0}에 분, {1}에 초가 들어간다. 폰트에 콜론이 없어 m/s로 적는다.")]
        [SerializeField] private string _timeFormat = "{0}m {1:00}s";

        [SerializeField] private TMP_Text _killsLabel;

        [Tooltip("{0}에 격추 수가 들어간다.")]
        [SerializeField] private string _killsFormat = "{0}";

        [SerializeField] private TMP_Text _styleLabel;

        [Tooltip("{0}에 벌어들인 스타일 점수의 합이 들어간다.")]
        [SerializeField] private string _styleFormat = "{0:0}";

        [SerializeField] private TMP_Text _rankLabel;

        [Tooltip("무랭크로 죽었을 때 랭크 자리에 적을 글자.")]
        [SerializeField] private string _noRankText = "-";

        [Header("박자")]
        [Tooltip("죽고 나서 패널이 뜨기까지의 시간(초).\n" +
                 "터지는 그 순간은 폭발이 주인공이다 — 잔해가 떨어지고 한숨 돌린\n" +
                 "뒤에 떠야 기록이 읽힌다.")]
        [Min(0f)]
        [SerializeField] private float _showDelaySeconds = 1.5f;

        [Tooltip("값 하나가 잠기기까지의 간격(초). 시간·격추·스타일·랭크가 이 간격으로 온다.")]
        [Min(0.1f)]
        [SerializeField] private float _stepSeconds = 0.7f;

        [Tooltip("구르는 숫자가 바뀌는 간격(초). 매 프레임 바꾸면 흐릿하고, 느리면 멈춘 것 같다.")]
        [Min(0.01f)]
        [SerializeField] private float _churnInterval = 0.04f;

        [Header("잠길 때")]
        [Tooltip("잠긴 글자가 부풀었다 돌아오는 배율. 1이면 연출하지 않는다.")]
        [Min(1f)]
        [SerializeField] private float _punchScale = 1.25f;

        [Tooltip("랭크가 찍힐 때의 배율. 절정이니 다른 것보다 크게 잡는다.")]
        [Min(1f)]
        [SerializeField] private float _rankPunchScale = 1.6f;

        [Min(0.01f)]
        [SerializeField] private float _punchSeconds = 0.15f;

        [Header("소리")]
        [Tooltip("잠길 때 울릴 소스. 비워두면 소리 없이 돈다.")]
        [SerializeField] private AudioSource _source;

        [Tooltip("값이 잠길 때마다 나는 소리.")]
        [SerializeField] private AudioClip _lockSound;

        [Tooltip("랭크가 찍힐 때의 소리. 비워두면 잠기는 소리를 그대로 쓴다.")]
        [SerializeField] private AudioClip _rankSound;

        [Tooltip("첫 소리의 크기. 마지막은 아래 값까지 커진다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _firstVolume = 0.35f;

        [Range(0f, 1f)]
        [SerializeField] private float _lastVolume = 1f;

        [Header("브금")]
        [Tooltip("배경음악 재생기. 비워두면 브금은 그대로 흐른다.")]
        [SerializeField] private MusicPlayer _music;

        [Tooltip("패널이 뜰 때 이 중 하나를 뽑아 루프로 돈다.\n" +
                 "되살아나면 원래 목록으로 돌아간다.")]
        [SerializeField] private AudioClip[] _deathTracks = System.Array.Empty<AudioClip>();

        private RunReport.Report _result;

        // 보고서를 받아두고 뜨기를 기다리는 중인지.
        private bool _pending;
        private float _waitRemaining;
        private bool _revealing;
        private float _elapsed;
        private float _churnAt;
        private int _locked;

        private RectTransform _punching;
        private Vector3 _punchingBase = Vector3.one;
        private float _punchRemaining;
        private float _punchAmount = 1f;

        private void Awake()
        {
            if (_report == null)
            {
                Debug.LogError($"{nameof(RunReportDisplay)}: 기록을 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _aircraft = AircraftRig.Resolve(this, _aircraft);
        }

        private void OnEnable()
        {
            _report.Finished += Show;

            if (_aircraft != null && _aircraft.Lifecycle != null)
            {
                _aircraft.Lifecycle.Respawned += Hide;
            }
        }

        private void OnDisable()
        {
            _report.Finished -= Show;

            if (_aircraft != null && _aircraft.Lifecycle != null)
            {
                _aircraft.Lifecycle.Respawned -= Hide;
            }
        }

        /// <summary>처음에는 접어둔다. 씬에 켜둔 채 저장해도 판이 시작하면 사라진다.</summary>
        private void Start() => Hide();

        /// <summary>
        /// 보고서만 받아두고 바로 뜨지 않는다. 터지는 그 순간은 폭발이 주인공이라,
        /// 잔해가 떨어지고 한숨 돌린 뒤에 떠야 기록이 읽힌다.
        /// </summary>
        private void Show(RunReport.Report report)
        {
            _result = report;
            _pending = true;
            _waitRemaining = _showDelaySeconds;
        }

        private void Reveal()
        {
            _pending = false;
            _revealing = true;
            _elapsed = 0f;
            _churnAt = 0f;
            _locked = 0;

            // 랭크는 구르지도 않는다. 마지막까지 자리 자체가 비어 있어야
            // 찍히는 순간이 등장이 된다.
            _rankLabel?.SetText(string.Empty);

            // 패널과 함께 브금도 장면을 바꾼다. 매번 하나를 새로 뽑는다 —
            // 죽을 때마다 같은 곡이면 두 번째 죽음부터는 배경이 된다.
            if (_music != null && _deathTracks.Length > 0)
            {
                _music.Override(_deathTracks[Random.Range(0, _deathTracks.Length)]);
            }

            if (_panel != null)
            {
                _panel.SetActive(true);
            }
        }

        private void Hide()
        {
            _pending = false;
            _revealing = false;
            Settle();

            // 끼어든 것이 없으면 아무 일도 하지 않으므로, 처음 켜질 때 불려도 안전하다.
            _music?.EndOverride();

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void Update()
        {
            // 죽은 화면의 연출이다. 게임 시계가 잡혀 있든 말든 같은 박자로 돈다.
            float delta = Time.unscaledDeltaTime;

            if (_pending)
            {
                _waitRemaining -= delta;

                if (_waitRemaining <= 0f)
                {
                    Reveal();
                }

                return;
            }

            if (!_revealing)
            {
                return;
            }

            _elapsed += delta;

            Churn();

            while (_locked < StepCount && _elapsed >= (_locked + 1) * _stepSeconds)
            {
                Lock(_locked);
                _locked++;
            }

            Punch(delta);
        }

        /// <summary>아직 안 잠긴 숫자들을 굴린다. 값의 생김새는 진짜와 같은 서식으로 맞춘다.</summary>
        private void Churn()
        {
            if (_elapsed < _churnAt || _locked >= RankStep)
            {
                return;
            }

            _churnAt = _elapsed + _churnInterval;

            if (_locked <= TimeStep)
            {
                _timeLabel?.SetText(string.Format(_timeFormat, Random.Range(0, 10), Random.Range(0, 60)));
            }

            if (_locked <= KillsStep)
            {
                _killsLabel?.SetText(string.Format(_killsFormat, Random.Range(0, 100)));
            }

            if (_locked <= StyleStep)
            {
                _styleLabel?.SetText(string.Format(_styleFormat, (float)Random.Range(0, 10000)));
            }
        }

        /// <summary>
        /// 값 하나를 제자리에 박는다. 소리는 순서가 뒤일수록 커진다 —
        /// 같은 크기로 네 번 울리면 반복이고, 커지면 상승이다.
        /// </summary>
        private void Lock(int step)
        {
            switch (step)
            {
                case TimeStep:
                    int minutes = Mathf.FloorToInt(_result.Seconds / 60f);
                    int seconds = Mathf.FloorToInt(_result.Seconds % 60f);

                    _timeLabel?.SetText(string.Format(_timeFormat, minutes, seconds));
                    StartPunch(_timeLabel, _punchScale);
                    break;

                case KillsStep:
                    _killsLabel?.SetText(string.Format(_killsFormat, _result.Kills));
                    StartPunch(_killsLabel, _punchScale);
                    break;

                case StyleStep:
                    _styleLabel?.SetText(string.Format(_styleFormat, _result.Style));
                    StartPunch(_styleLabel, _punchScale);
                    break;

                case RankStep:
                    _rankLabel?.SetText(
                        string.IsNullOrEmpty(_result.BestRank) ? _noRankText : _result.BestRank);
                    StartPunch(_rankLabel, _rankPunchScale);
                    break;
            }

            if (_source == null)
            {
                return;
            }

            AudioClip clip = step == RankStep && _rankSound != null ? _rankSound : _lockSound;

            if (clip != null)
            {
                float volume = Mathf.Lerp(_firstVolume, _lastVolume, step / (float)(StepCount - 1));
                _source.PlayOneShot(clip, volume);
            }
        }

        /// <summary>
        /// 방금 잠긴 글자를 부풀린다. 한 번에 하나만 부푼다 — 다음이 잠길 때는
        /// 앞의 것이 이미 가라앉은 뒤다.
        /// </summary>
        private void StartPunch(TMP_Text label, float amount)
        {
            if (label == null || amount <= 1f)
            {
                return;
            }

            Settle();

            _punching = label.rectTransform;
            _punchingBase = _punching.localScale;
            _punchAmount = amount;
            _punchRemaining = _punchSeconds;
        }

        private void Punch(float delta)
        {
            if (_punching == null || _punchRemaining <= 0f)
            {
                return;
            }

            _punchRemaining = Mathf.Max(0f, _punchRemaining - delta);

            float t = _punchRemaining / _punchSeconds;
            _punching.localScale = _punchingBase * Mathf.Lerp(1f, _punchAmount, t * t);
        }

        /// <summary>부풀어 있던 것을 제자리로 되돌린다.</summary>
        private void Settle()
        {
            if (_punching != null)
            {
                _punching.localScale = _punchingBase;
                _punching = null;
            }

            _punchRemaining = 0f;
        }
    }
}
