using Adler.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 사출된 조명탄 하나. 미사일이 대신 쫓아갈 미끼가 된다.
    /// <para>
    /// 스스로는 아무것도 하지 않는다. 목록에 자기를 올려두고 타는 동안 떨어질 뿐이고,
    /// 속을지 말지는 미사일이 판단한다. 미끼가 미사일을 끌어당기는 구조로 만들면
    /// 조명탄 하나가 날아오는 모든 미사일을 알고 있어야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Flare : MonoBehaviour
    {

        [Tooltip("여기에 닿으면 사라진다. 지형을 넣고 기체는 빼둘 것 —\n" +
                 "사출되는 자리가 기체 위라서, 넣어두면 나오자마자 없어진다.")]
        [SerializeField] private LayerMask _despawnMask = ~0;

        private FlareDefinition _definition;
        private Rigidbody _body;
        private Clock _clock;
        private float _burnRemaining;

        /// <summary>지금 타고 있는 조명탄들. 미사일이 훑어본다.</summary>
        public static IReadOnlyList<Flare> Burning => Registry<Flare>.All;

        public float SeduceRange => _definition != null ? _definition.SeduceRange : 0f;

        public float SeduceAngle => _definition != null ? _definition.SeduceAngle : 0f;

        /// <summary>뿌린 쪽이 성능과 속도를 넘겨준다.</summary>
        public void Ignite(FlareDefinition definition, Vector3 velocity, float spin)
        {
            _definition = definition;
            _burnRemaining = definition.BurnSeconds;
            _body = GetComponent<Rigidbody>();

            if (_body == null)
            {
                Debug.LogError($"{nameof(Flare)}: Rigidbody가 없어 사출되지 않습니다.", this);
                return;
            }

            // 중력을 직접 준다. 3D Rigidbody에는 2D의 Gravity Scale에 해당하는 것이
            // 없어서, 배율을 쓰려면 끄고 넣는 수밖에 없다.
            _body.useGravity = false;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Random.onUnitSphere * spin;
        }

        /// <summary>
        /// 줄인 중력을 먹인다.
        /// <para>
        /// 질량과 무관하게 가속으로 넣는다. 힘으로 주면 프리팹의 질량을 바꿀 때마다
        /// 떨어지는 속도가 달라져서, 무게를 조절할 수 없게 된다.
        /// </para>
        /// </summary>
        private void FixedUpdate()
        {
            if (_body == null || _definition == null || _definition.GravityScale <= 0f)
            {
                return;
            }

            _body.AddForce(Physics.gravity * _definition.GravityScale, ForceMode.Acceleration);
        }

        private void Awake() => _clock = TimeScale.For(this);

        private void OnEnable() => Registry<Flare>.Add(this);

        private void OnDisable() => Registry<Flare>.Remove(this);

        /// <summary>
        /// 땅에 닿으면 사라진다.
        /// <para>
        /// 튀어 오르면 미사일을 엉뚱한 쪽으로 끌고 간다. 미끼가 어디에 있는지 예상할 수
        /// 없게 되어, 뿌린 사람도 결과를 짐작하지 못한다.
        /// </para>
        /// <para>
        /// 부딪히는 순간 물리 반응이 이미 계산된 뒤라 한 프레임쯤 튀어 보일 수 있다.
        /// 콜라이더를 Is Trigger로 두면 반응 자체가 없어 그것도 사라진다.
        /// </para>
        /// </summary>
        private void OnCollisionEnter(Collision collision) => DespawnOn(collision.gameObject);

        private void OnTriggerEnter(Collider other) => DespawnOn(other.gameObject);

        private void DespawnOn(GameObject other)
        {
            if ((_despawnMask.value & (1 << other.layer)) != 0)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 다 타면 사라진다.
        /// <para>
        /// 목록에서 빼는 것이 아니라 오브젝트를 없앤다. 꺼진 조명탄이 화면에 남아 있으면
        /// 아직 쓸 수 있는 것으로 보이고, 그것을 믿고 기동하면 맞는다.
        /// </para>
        /// </summary>
        private void Update()
        {
            if (_definition == null)
            {
                return;
            }

            _burnRemaining -= _clock.Delta;

            if (_burnRemaining <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
