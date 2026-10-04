using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Lightweight authored motion for an iceberg. The iceberg always oscillates
    /// around its scene position, so fake drift cannot gradually damage the route.
    /// Physical icebergs use a kinematic rigidbody so their colliders move through
    /// the physics system instead of teleporting every rendered frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FakeIcebergBuoyancy : MonoBehaviour
    {
        private Transform cachedTransform;
        private Rigidbody body;
        private bool createdBody;
        private bool configured;
        private bool hasCollision;

        private Vector3 anchorLocalPosition;
        private Quaternion anchorLocalRotation;
        private Vector3 driftDirection;
        private Vector3 driftPerpendicular;
        private float phase;
        private float frequencyMultiplier;

        private float bobHeight;
        private float bobCycleSeconds;
        private float driftDistance;
        private float driftCycleSeconds;
        private float tiltDegrees;
        private float landingDip;
        private float standingSink;
        private float landingSpring;
        private float landingDamping;

        private float responseOffset;
        private float responseVelocity;
        private float lastSupportedTime = float.NegativeInfinity;
        private float lastImpactTime = float.NegativeInfinity;

        public void Configure(
            bool collisionEnabled,
            int seed,
            float newBobHeight,
            float newBobCycleSeconds,
            float newDriftDistance,
            float newDriftCycleSeconds,
            float newTiltDegrees,
            float newLandingDip,
            float newStandingSink,
            float newLandingSpring,
            float newLandingDamping)
        {
            cachedTransform ??= transform;

            if (!configured)
            {
                anchorLocalPosition = cachedTransform.localPosition;
                anchorLocalRotation = cachedTransform.localRotation;

                System.Random random = new System.Random(seed);
                phase = (float)random.NextDouble() * Mathf.PI * 2f;
                frequencyMultiplier = Mathf.Lerp(.86f, 1.14f, (float)random.NextDouble());
                float directionAngle = (float)random.NextDouble() * Mathf.PI * 2f;
                driftDirection = new Vector3(Mathf.Cos(directionAngle), 0f, Mathf.Sin(directionAngle));
                driftPerpendicular = new Vector3(-driftDirection.z, 0f, driftDirection.x);
            }

            hasCollision = collisionEnabled;
            bobHeight = Mathf.Max(0f, newBobHeight);
            bobCycleSeconds = Mathf.Max(.25f, newBobCycleSeconds);
            driftDistance = Mathf.Max(0f, newDriftDistance);
            driftCycleSeconds = Mathf.Max(.5f, newDriftCycleSeconds);
            tiltDegrees = Mathf.Max(0f, newTiltDegrees);
            landingDip = Mathf.Max(0f, newLandingDip);
            standingSink = Mathf.Clamp(newStandingSink, 0f, landingDip);
            landingSpring = Mathf.Max(0f, newLandingSpring);
            landingDamping = Mathf.Max(0f, newLandingDamping);
            configured = true;

            ConfigurePhysicsBody();
        }

        /// <summary>
        /// Called by the player ground probe. Repeated calls keep the slight load
        /// depression active; only a fresh descending contact creates an impact.
        /// </summary>
        public void NotifyPlayerSupport(float verticalVelocity)
        {
            float now = Time.time;
            bool freshContact = now - lastSupportedTime > .14f;
            lastSupportedTime = now;

            if (!freshContact || now - lastImpactTime < .2f)
            {
                return;
            }

            float downwardSpeed = Mathf.Max(0f, -verticalVelocity);
            if (downwardSpeed < .35f)
            {
                return;
            }

            // Convert landing speed to a bounded downward impulse. The spring
            // supplies the visible dip and recovery without real buoyancy forces.
            float normalizedImpact = Mathf.InverseLerp(.35f, 8f, downwardSpeed);
            responseVelocity -= Mathf.Lerp(.18f, .85f, normalizedImpact);
            lastImpactTime = now;
        }

        private void FixedUpdate()
        {
            if (!configured)
            {
                return;
            }

            float loadTarget = Time.time - lastSupportedTime <= .12f ? -standingSink : 0f;
            float acceleration = (loadTarget - responseOffset) * landingSpring - responseVelocity * landingDamping;
            responseVelocity += acceleration * Time.fixedDeltaTime;
            responseOffset += responseVelocity * Time.fixedDeltaTime;
            responseOffset = Mathf.Clamp(responseOffset, -landingDip, landingDip * .35f);

            float bobAngle = (Time.fixedTime / bobCycleSeconds) * Mathf.PI * 2f * frequencyMultiplier + phase;
            float driftAngle = (Time.fixedTime / driftCycleSeconds) * Mathf.PI * 2f * frequencyMultiplier + phase;

            float verticalOffset = Mathf.Sin(bobAngle) * bobHeight + responseOffset;
            Vector3 horizontalOffset =
                driftDirection * (Mathf.Sin(driftAngle) * driftDistance) +
                driftPerpendicular * (Mathf.Sin(driftAngle * .53f + phase) * driftDistance * .28f);

            Vector3 basePosition = cachedTransform.parent != null
                ? cachedTransform.parent.TransformPoint(anchorLocalPosition)
                : anchorLocalPosition;
            Quaternion baseRotation = cachedTransform.parent != null
                ? cachedTransform.parent.rotation * anchorLocalRotation
                : anchorLocalRotation;

            Vector3 targetPosition = basePosition + horizontalOffset + Vector3.up * verticalOffset;
            float pitch = Mathf.Sin(bobAngle + 1.1f) * tiltDegrees;
            float roll = Mathf.Sin(bobAngle * .79f + phase) * tiltDegrees;
            Quaternion targetRotation = Quaternion.Euler(pitch, 0f, roll) * baseRotation;

            if (body != null && hasCollision)
            {
                body.MovePosition(targetPosition);
                body.MoveRotation(targetRotation);
            }
            else
            {
                cachedTransform.SetPositionAndRotation(targetPosition, targetRotation);
            }
        }

        private void OnDestroy()
        {
            if (createdBody && body != null)
            {
                Destroy(body);
            }
        }

        private void ConfigurePhysicsBody()
        {
            if (!hasCollision)
            {
                return;
            }

            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
                createdBody = true;
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }
}
