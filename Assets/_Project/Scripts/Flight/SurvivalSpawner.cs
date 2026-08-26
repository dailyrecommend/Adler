using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 죽을 때까지 적을 계속 밀어 넣는다. 판 하나가 곧 게임이다.
    /// <para>
    /// 시간이 갈수록 간격이 좁아진다. 끝이 없는 판의 난이도는 목록이 아니라 박자로
    /// 만든다 — 어디까지 버티는지가 결과가 되려면, 오래 버틸수록 어려워져야 한다.
    /// </para>
    /// <para>
    /// 플레이어가 죽으면 판이 새로 시작한다. 뿌린 적을 재우고, 박자를 처음으로 되돌리고,
    /// 점수를 비운다. 죽음이 판의 끝이라는 규칙이 코드에 있는 곳이 여기 하나다.
    /// </para>
    /// <para>
    /// 시체는 지우지 않고 재운다. 적은 죽을 때 스스로 꺼지고 <see cref="Health.Revive"/>로
    /// 온전히 돌아오므로, 만들고 부수기를 반복할 이유가 없다 — 한 판에서 가장 많이
    /// 살아 있던 수만큼만 만들어지고 끝이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalSpawner : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("판의 중심. 이 기체 둘레에 적이 나타나고, 이 기체가 죽으면 판이 새로 시작한다.")]
        [SerializeField] private AircraftRig _player;

        [Tooltip("점수판. 판이 새로 시작할 때 비운다. 비워두면 점수는 이어진다.")]
        [SerializeField] private StyleMeter _style;

        [Header("무엇을")]
        [Tooltip("나타날 적의 프리팹들. 여럿이면 무작위로 고른다.")]
        [SerializeField] private GameObject[] _enemies = System.Array.Empty<GameObject>();

        [Header("박자")]
        [Tooltip("판이 시작하자마자 깔리는 수. 빈 하늘에서 시작하면 첫 간격만큼 아무 일도 없다.")]
        [Min(0)]
        [SerializeField] private int _opening = 2;

        [Tooltip("판 초반의 등장 간격(초).")]
        [Min(0.5f)]
        [SerializeField] private float _startInterval = 10f;

        [Tooltip("끝까지 갔을 때의 등장 간격(초). 이보다 빨라지지는 않는다.")]
        [Min(0.5f)]
        [SerializeField] private float _minInterval = 3f;

        [Tooltip("초반 간격에서 최소 간격까지 좁아지는 데 걸리는 시간(초).")]
        [Min(1f)]
        [SerializeField] private float _rampSeconds = 300f;

        [Tooltip("판 초반에 동시에 살아 있을 수 있는 수.")]
        [Min(1)]
        [SerializeField] private int _startAlive = 3;

        [Tooltip("끝까지 갔을 때 동시에 살아 있을 수 있는 수.\n\n" +
                 "간격과 같은 시계로 함께 커진다. 간격만 좁히면 죽이는 속도가 상한을\n" +
                 "따라잡는 순간 난이도가 멈추는데, 하늘에 있는 수 자체가 늘면 아무리\n" +
                 "잘 죽여도 판이 계속 무거워진다.\n\n" +
                 "자리가 없으면 등장을 미룬다 — 죽이는 속도가 곧 다음 적이 오는 속도가 된다.")]
        [Min(1)]
        [SerializeField] private int _maxAlive = 8;

        [Header("자리")]
        [Tooltip("플레이어에게서 이보다 가깝게는 나타나지 않는다 (m).\n" +
                 "코앞에서 솟아나면 등장이 아니라 순간이동으로 보인다.")]
        [Min(10f)]
        [SerializeField] private float _minDistance = 120f;

        [Tooltip("경계가 없는 씬에서만 쓴다. 경계가 있으면 벽에서 태어나므로\n" +
                 "거리는 벽이 정하고, 없으면 플레이어 둘레 이 반경 안의 고리에서 태어난다.")]
        [Min(10f)]
        [SerializeField] private float _maxDistance = 250f;

        [Tooltip("나타나는 높이의 아래쪽 (월드 y).")]
        [SerializeField] private float _minAltitude = 30f;

        [Tooltip("나타나는 높이의 위쪽 (월드 y). 얼어붙는 고도보다는 아래여야 한다.")]
        [SerializeField] private float _maxAltitude = 200f;

        /// <summary>재워둔 것 하나. 종류를 함께 적어야 같은 종류로만 되살린다.</summary>
        private readonly struct Pooled
        {
            public readonly int Kind;
            public readonly Health Health;

            public Pooled(int kind, Health health)
            {
                Kind = kind;
                Health = health;
            }
        }

        private readonly List<Pooled> _pool = new();

        private Clock _clock;
        private float _runStartedAt;
        private float _nextSpawnAt;

        private void Awake()
        {
            _clock = TimeScale.For(this);

            if (_player == null || _enemies.Length == 0)
            {
                Debug.LogError($"{nameof(SurvivalSpawner)}: 플레이어 또는 적 프리팹이 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_player != null && _player.Lifecycle != null)
            {
                _player.Lifecycle.Respawned += BeginRun;
            }
        }

        private void OnDisable()
        {
            if (_player != null && _player.Lifecycle != null)
            {
                _player.Lifecycle.Respawned -= BeginRun;
            }
        }

        private void Start() => BeginRun();

        /// <summary>
        /// 판을 새로 시작한다. 처음 켜질 때와 플레이어가 되살아날 때.
        /// </summary>
        private void BeginRun()
        {
            // 지난 판의 적을 재운다. 남겨두면 새 판이 지난 판의 빚을 안고 시작한다.
            foreach (Pooled pooled in _pool)
            {
                if (pooled.Health != null && pooled.Health.gameObject.activeSelf)
                {
                    pooled.Health.gameObject.SetActive(false);
                }
            }

            _runStartedAt = _clock.Now;
            _nextSpawnAt = _clock.Now + _startInterval;

            for (int i = 0; i < _opening; i++)
            {
                Spawn();
            }

            // 점수도 판에 속한다. 지난 판의 점수를 안고 시작하면 랭크가 실력이 아니라 이월이다.
            if (_style != null)
            {
                _style.ResetScore();
            }
        }

        private void Update()
        {
            if (_clock.Now < _nextSpawnAt)
            {
                return;
            }

            if (CountAlive() >= CurrentCap())
            {
                // 자리가 없다. 간격을 태우지 않고 잠깐 뒤에 다시 본다 — 여기서 간격을
                // 소모하면 가득 찬 하늘이 오히려 다음 등장을 늦추는 셈이 된다.
                _nextSpawnAt = _clock.Now + 1f;
                return;
            }

            Spawn();
            _nextSpawnAt = _clock.Now + CurrentInterval();
        }

        /// <summary>난이도 시계. 판이 시작하고 얼마나 왔는지 (0~1).</summary>
        private float Ramp => Mathf.Clamp01((_clock.Now - _runStartedAt) / _rampSeconds);

        /// <summary>지금의 등장 간격. 판이 오래될수록 최소 간격으로 좁아진다.</summary>
        private float CurrentInterval() => Mathf.Lerp(_startInterval, _minInterval, Ramp);

        /// <summary>
        /// 지금의 동시 상한. 판이 오래될수록 하늘에 있는 수 자체가 늘어난다.
        /// 간격과 같은 시계를 쓴다 — 난이도가 두 시계로 갈리면 조율이 두 배가 된다.
        /// </summary>
        private int CurrentCap() => Mathf.RoundToInt(Mathf.Lerp(_startAlive, _maxAlive, Ramp));

        private int CountAlive()
        {
            int alive = 0;

            foreach (Pooled pooled in _pool)
            {
                if (pooled.Health != null && pooled.Health.gameObject.activeInHierarchy)
                {
                    alive++;
                }
            }

            return alive;
        }

        private void Spawn()
        {
            if (CountAlive() >= CurrentCap())
            {
                return;
            }

            int kind = Random.Range(0, _enemies.Length);
            Health health = Claim(kind);

            if (health == null)
            {
                return;
            }

            // 자리는 깨어나기 전에 잡는다. 깨어난 뒤에 옮기면 옛 자리에서 한 프레임
            // 살았다가 순간이동하고, 그 한 프레임에 조준이 옛 자리를 문다.
            Transform body = health.transform;
            body.SetPositionAndRotation(PickPosition(), PickFacing(body.position));

            health.Revive();
        }

        /// <summary>재워둔 것 중 같은 종류를 깨우고, 없으면 새로 만든다.</summary>
        private Health Claim(int kind)
        {
            foreach (Pooled pooled in _pool)
            {
                if (pooled.Kind == kind
                    && pooled.Health != null
                    && !pooled.Health.gameObject.activeSelf)
                {
                    return pooled.Health;
                }
            }

            GameObject body = Instantiate(_enemies[kind], transform);
            Health health = body.GetComponentInChildren<Health>(includeInactive: true);

            if (health == null)
            {
                Debug.LogError(
                    $"{nameof(SurvivalSpawner)}: '{_enemies[kind].name}'에 {nameof(Health)}가 " +
                    "없어 등장시킬 수 없습니다.", this);
                Destroy(body);
                return null;
            }

            _pool.Add(new Pooled(kind, health));
            return health;
        }

        /// <summary>
        /// 태어날 자리. 경계가 있으면 벽에서, 없으면 플레이어 둘레의 고리에서.
        /// <para>
        /// 벽에서 태어나는 이유는 등장이 보이기 때문이다. 하늘 한가운데 솟아나면
        /// 어디서 왔는지가 없는데, 가장자리에서 날아 들어오면 바깥에서 온 것이 된다.
        /// 벽에 붙은 채 태어나도 경계 조향이 곧장 안쪽으로 데려온다.
        /// </para>
        /// <para>
        /// 플레이어가 벽에 붙어 있을 수 있으므로 몇 번 굴려 먼 쪽을 고른다.
        /// 최소 거리를 만족하는 첫 자리를 쓰고, 전부 가까우면 그중 가장 먼 곳이다 —
        /// 못 태어나는 것보다는 가까운 등장이 낫다.
        /// </para>
        /// </summary>
        private Vector3 PickPosition()
        {
            Battlefield field = Battlefield.Active;
            Vector3 center = _player.transform.position;

            if (field == null)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                float distance = Random.Range(_minDistance, _maxDistance);

                return new Vector3(
                    center.x + (direction.x * distance),
                    Random.Range(_minAltitude, _maxAltitude),
                    center.z + (direction.y * distance));
            }

            Vector3 best = Vector3.zero;
            float bestDistance = -1f;

            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = PickWallPoint(field);
                float distance = Vector3.Distance(candidate, center);

                if (distance >= _minDistance)
                {
                    return candidate;
                }

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 네 벽 중 하나의 안쪽 면 위의 한 점. 벽에서 살짝 띄운다 —
        /// 딱 붙여 태어나면 반쯤 벽에 걸친 채로 나타난다.
        /// </summary>
        private Vector3 PickWallPoint(Battlefield field)
        {
            const float Inset = 30f;

            Vector3 half = (field.Size * 0.5f) - new Vector3(Inset, 0f, Inset);
            Vector3 point = field.Center;

            // 지도가 대략 정사각형이라 네 벽을 같은 확률로 고른다.
            // 한쪽으로 긴 지도가 되면 긴 벽에 가중치를 주어야 고르게 퍼진다.
            int wall = Random.Range(0, 4);

            switch (wall)
            {
                case 0: point += new Vector3(half.x, 0f, Random.Range(-half.z, half.z)); break;
                case 1: point += new Vector3(-half.x, 0f, Random.Range(-half.z, half.z)); break;
                case 2: point += new Vector3(Random.Range(-half.x, half.x), 0f, half.z); break;
                default: point += new Vector3(Random.Range(-half.x, half.x), 0f, -half.z); break;
            }

            point.y = Random.Range(_minAltitude, _maxAltitude);

            // 높이 띠가 경계 밖으로 나가 있어도 안으로 들인다.
            return field.ClampInside(point, Inset);
        }

        /// <summary>
        /// 수평으로 플레이어를 바라보고 시작한다. 아무 데나 보고 태어나면 한동안
        /// 싸움과 무관한 방향으로 날아가서, 등장했는데 하늘이 비어 보인다.
        /// </summary>
        private Quaternion PickFacing(Vector3 from)
        {
            Vector3 toPlayer = _player.transform.position - from;
            toPlayer.y = 0f;

            return toPlayer.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toPlayer.normalized, Vector3.up)
                : Quaternion.identity;
        }
    }
}
