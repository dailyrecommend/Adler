using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 발사할 때마다 짧은 선을 그려 탄이 나가는 것을 보이게 한다.
    /// <para>
    /// 히트스캔은 즉시 판정이라 아무 연출이 없으면 표적이 이유 없이 사라진다.
    /// 조준이 맞았는지 빗나갔는지는 이 선을 봐야 알 수 있으므로, 사격 감각을 잡는
    /// 동안에는 예광탄이 사실상 유일한 피드백이다.
    /// </para>
    /// <para>
    /// 발사가 잦아 매번 생성하면 쓰레기가 쌓인다. 미리 만들어 두고 돌려 쓴다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(AircraftGun))]
    [DisallowMultipleComponent]
    public sealed class GunTracers : MonoBehaviour
    {
        [Header("모양")]
        [Tooltip("비워두면 실행 중에 기본 재질을 만들어 쓴다. 연출을 다듬을 땐 직접 지정할 것.")]
        [SerializeField] private Material _material;

        [SerializeField] private Color _color = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private float _width = 0.03f;

        [Tooltip("한 발이 화면에 남는 시간(초). 길수록 탄줄이 굵게 이어져 보인다.")]
        [SerializeField] private float _lifetime = 0.05f;

        [Tooltip("동시에 보일 수 있는 최대 예광탄 수.")]
        [Min(1)]
        [SerializeField] private int _poolSize = 16;

        private AircraftGun _gun;
        private LineRenderer[] _pool;
        private float[] _expiry;
        private int _next;
        private Material _runtimeMaterial;

        private void Awake()
        {
            _gun = GetComponent<AircraftGun>();
            BuildPool();
        }

        private void OnEnable() => _gun.Fired += OnFired;

        private void OnDisable() => _gun.Fired -= OnFired;

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }

        private void BuildPool()
        {
            _pool = new LineRenderer[_poolSize];
            _expiry = new float[_poolSize];

            Material material = ResolveMaterial();

            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Tracer {i}");
                go.transform.SetParent(transform, worldPositionStays: false);

                var line = go.AddComponent<LineRenderer>();
                line.material = material;
                line.startColor = _color;
                line.endColor = _color;
                line.startWidth = _width;
                line.endWidth = _width;
                line.positionCount = 2;
                line.useWorldSpace = true;      // 기체가 움직여도 탄줄은 쏜 자리에 남는다
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;

                _pool[i] = line;
            }
        }

        private Material ResolveMaterial()
        {
            if (_material != null)
            {
                return _material;
            }

            // URP 기본 Unlit으로 임시 재질을 만든다. 지정하지 않아도 자홍색 오류 대신
            // 그럴듯한 선이 나오게 해서, 연출 다듬기 전에도 사격을 시험할 수 있게 한다.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _runtimeMaterial = new Material(shader) { color = _color };
            return _runtimeMaterial;
        }

        private void OnFired(Vector3 origin, Vector3 end)
        {
            LineRenderer line = _pool[_next];
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            line.enabled = true;

            _expiry[_next] = Time.time + _lifetime;
            _next = (_next + 1) % _pool.Length;
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].enabled && now >= _expiry[i])
                {
                    _pool[i].enabled = false;
                }
            }
        }
    }
}
