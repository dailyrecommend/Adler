using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using UnityEngine;

namespace Adler.Effects
{
    /// <summary>
    /// 지나온 자리에 기체 모양을 잠깐씩 남긴다.
    /// <para>
    /// 꼬리를 그리는 것과 다르다. 리본은 어디를 지나왔는지만 말하지만 잔상은 <b>어떤
    /// 자세로</b> 지나왔는지까지 남겨서, 굴리며 파고드는 동안 그 궤적이 통째로 보인다.
    /// </para>
    /// <para>
    /// 남긴 것을 지우지 않고 돌려 쓴다. 초당 스무 개씩 만들고 부수면 그 뒷정리가
    /// 프레임으로 돌아오는데, 하필 빠르게 날 때만 일어나므로 가장 매끄러워야 할
    /// 순간에 끊긴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GhostTrail : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("잔상으로 남길 렌더러들. 비워두면 이 오브젝트 아래에서 찾는다.")]
        [SerializeField] private MeshRenderer[] _renderers;

        [Header("언제")]
        [Tooltip("이 상태인 동안 잔상을 남긴다.")]
        [SerializeField] private AircraftCondition _when = AircraftCondition.Boosting;

        [Tooltip("Debuff를 고른 경우에만 쓴다.")]
        [SerializeField] private DebuffDefinition _debuff;

        [Header("잔상")]
        [Tooltip("잔상에 입힐 재질. 투명하게 사라져야 하므로 Transparent 셰이더를 쓸 것.")]
        [SerializeField] private Material _material;

        [Tooltip("잔상을 남기는 간격(초). 짧을수록 촘촘하다.")]
        [Min(0.01f)]
        [SerializeField] private float _interval = 0.05f;

        [Tooltip("하나가 사라지기까지의 시간(초).")]
        [Min(0.05f)]
        [SerializeField] private float _lifetime = 0.35f;

        [Tooltip("막 남겼을 때의 투명도. 여기서 0까지 옅어진다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _startAlpha = 0.5f;

        [Tooltip("색을 입힐 재질 속성의 이름. URP 기본은 _BaseColor다.")]
        [SerializeField] private string _colorProperty = "_BaseColor";

        /// <summary>남겨둔 잔상 하나. 자기 수명만 알고 있는다.</summary>
        private sealed class Ghost
        {
            public GameObject Root;
            public MeshRenderer[] Renderers;
            public float Remaining;
        }

        private readonly List<Ghost> _live = new();
        private readonly Stack<Ghost> _idle = new();

        private Clock _clock;
        private MaterialPropertyBlock _block;
        private int _colorId;
        private float _nextAt;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _block = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID(_colorProperty);

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: false);
            }

            if (_aircraft == null || _material == null || _renderers.Length == 0)
            {
                Debug.LogError($"{nameof(GhostTrail)}: 기체, 재질, 렌더러 중 빠진 것이 있습니다.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            Fade();

            if (!AircraftConditions.IsMet(_aircraft, _when, _debuff))
            {
                return;
            }

            if (_clock.Now < _nextAt)
            {
                return;
            }

            _nextAt = _clock.Now + _interval;
            Leave();
        }

        /// <summary>
        /// 지금 자세 그대로 한 벌 떠낸다.
        /// <para>
        /// 기체에 매달지 않고 세계에 세워 둔다. 매달면 잔상이 기체를 따라다녀서
        /// 지나온 자리가 아니라 겹쳐 붙은 그림자가 된다.
        /// </para>
        /// </summary>
        private void Leave()
        {
            Ghost ghost = _idle.Count > 0 ? _idle.Pop() : Create();

            ghost.Remaining = _lifetime;
            ghost.Root.SetActive(true);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Transform from = _renderers[i].transform;
                Transform to = ghost.Renderers[i].transform;

                to.SetPositionAndRotation(from.position, from.rotation);
                to.localScale = from.lossyScale;
            }

            _live.Add(ghost);
            Tint(ghost, 1f);
        }

        private Ghost Create()
        {
            GameObject root = new($"{name} Ghost");
            MeshRenderer[] copies = new MeshRenderer[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                GameObject piece = new("Piece");
                piece.transform.SetParent(root.transform, worldPositionStays: false);

                piece.AddComponent<MeshFilter>().sharedMesh =
                    _renderers[i].GetComponent<MeshFilter>()?.sharedMesh;

                MeshRenderer renderer = piece.AddComponent<MeshRenderer>();

                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                copies[i] = renderer;
            }

            return new Ghost { Root = root, Renderers = copies };
        }

        private void Fade()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Ghost ghost = _live[i];

                ghost.Remaining -= _clock.Delta;

                if (ghost.Remaining <= 0f)
                {
                    ghost.Root.SetActive(false);
                    _live.RemoveAt(i);
                    _idle.Push(ghost);
                    continue;
                }

                Tint(ghost, ghost.Remaining / _lifetime);
            }
        }

        /// <summary>
        /// 재질을 건드리지 않고 색만 얹는다. 잔상마다 재질을 복제하면 그만큼
        /// 드로우콜이 갈라지고, 남긴 수만큼 재질이 쌓인다.
        /// </summary>
        private void Tint(Ghost ghost, float life)
        {
            Color color = _material.HasProperty(_colorId) ? _material.GetColor(_colorId) : Color.white;

            color.a = _startAlpha * life;
            _block.SetColor(_colorId, color);

            foreach (MeshRenderer renderer in ghost.Renderers)
            {
                renderer.SetPropertyBlock(_block);
            }
        }

        private void OnDisable()
        {
            foreach (Ghost ghost in _live)
            {
                ghost.Root.SetActive(false);
                _idle.Push(ghost);
            }

            _live.Clear();
        }
    }
}
