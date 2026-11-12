using UnityEngine;

namespace RealityDefender
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Projectile : MonoBehaviour
    {
        public const float MaximumRange = 5f;
        public Vector3 LaunchPosition { get; private set; }
        public int RoundId { get; private set; }
        public bool IsSpent { get; private set; }
        public Rigidbody Body { get; private set; }
        WaveController owner;
        float launchedAt;

        public void Launch(WaveController wave, int roundId, Vector3 position, Vector3 forward, float speed)
        {
            owner = wave;
            RoundId = roundId;
            LaunchPosition = position;
            IsSpent = false;
            launchedAt = Time.time;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            Body = GetComponent<Rigidbody>();
            Body.isKinematic = false;
            Body.useGravity = false;
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Body.position = position;
            Body.rotation = transform.rotation;
            Body.linearVelocity = forward.normalized * speed;
            GetComponent<SphereCollider>().isTrigger = false;
            int layer = LayerMask.NameToLayer("Projectile");
            if (layer >= 0) gameObject.layer = layer;
            foreach (var trail in GetComponentsInChildren<TrailRenderer>()) trail.Clear();
        }

        void FixedUpdate()
        {
            if (IsSpent) return;
            if (!owner || !owner.IsActive || !owner.IsTracking || owner.RoundId != RoundId ||
                !Body || (Body.position - LaunchPosition).sqrMagnitude > MaximumRange * MaximumRange ||
                Time.time - launchedAt > 2f)
                Retire();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsSpent) return;
            // A collision can occur after FixedUpdate but on the step that crosses the range limit.
            bool inRange = Body && (Body.position - LaunchPosition).sqrMagnitude <= MaximumRange * MaximumRange;
            IsSpent = true;
            var target = collision.collider.GetComponentInParent<EnergyTarget>();
            if (inRange && owner && target)
            {
                Vector3 contact = collision.contactCount > 0 ? collision.GetContact(0).point : Body.position;
                owner.TryAcceptHit(this, target, contact);
            }
            DisableAndDestroy();
        }

        public void Retire()
        {
            if (IsSpent) return;
            IsSpent = true;
            DisableAndDestroy();
        }

        void DisableAndDestroy()
        {
            if (Body) Body.linearVelocity = Vector3.zero;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (owner) owner.ForgetProjectile(this);
            Destroy(gameObject);
        }
    }
}
