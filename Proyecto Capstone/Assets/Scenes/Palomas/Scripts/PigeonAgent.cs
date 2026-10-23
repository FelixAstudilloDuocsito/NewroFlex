using UnityEngine;

namespace NeuroFlexVR
{
    public enum PigeonMovementLevel { Static, Low, Medium, High }

    /// <summary>
    /// Animacion + movimiento simple de palomas para Quest. No necesita NavMesh.
    /// El objeto raiz mueve la posicion; el Animator puede estar en un hijo.
    /// </summary>
    public class PigeonAgent : MonoBehaviour
    {
        [Header("Animator (estados de Base Layer)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string walkState = "Base Layer.Walk";
        [SerializeField] private string flyState = "Base Layer.Fly";
        [SerializeField] private string idleState = "Base Layer.Idle";

        [Header("Movimiento una vez dentro")]
        [Min(0.1f)] public float walkingRadius = 0.7f;
        [Min(0.3f)] public float flyingHeight = 1.2f;
        [Min(0.1f)] public float flyingSpeed = 1.5f;
        [Min(0f)] public float turnDegreesPerSecond = 240f;
        [Range(0f, 1f)] public float idleProbability = 0.4f;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 destination;
        private PigeonMovementLevel level;
        private bool motionPaused;
        private bool approaching;
        private bool flying;
        private bool landing;
        private float idleRemaining;
        private float approachSpeed;
        private float arrivalAnimationMultiplier = 1.6f;
        private string activeAnimation;

        public bool HasArrived => !approaching;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.applyRootMotion = false;
            CaptureHomePose();
        }

        public void CaptureHomePose()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        public void ResetForRound(PigeonMovementLevel newLevel)
        {
            level = newLevel;
            approaching = false;
            motionPaused = false;
            flying = false;
            landing = false;
            idleRemaining = 0;
            activeAnimation = null;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            if (animator != null) animator.speed = 1f;
            if (level == PigeonMovementLevel.Static) PlayStationary();
            else SelectNextDestination();
        }

        /// <summary>Se llama al activar la paloma desde un punto exterior.</summary>
        public void BeginApproach(Vector3 feedingPosition, float speed,
            float animationSpeed, PigeonMovementLevel newLevel)
        {
            level = newLevel;
            homePosition = feedingPosition;
            homeRotation = transform.rotation;
            destination = feedingPosition;
            approaching = true;
            motionPaused = false;
            flying = false;
            landing = false;
            idleRemaining = 0f;
            activeAnimation = null;
            approachSpeed = Mathf.Max(0.1f, speed);
            arrivalAnimationMultiplier = Mathf.Max(0.1f, animationSpeed);
            if (animator != null) animator.speed = arrivalAnimationMultiplier;
            PlayAnimation(walkState);
        }

        public void SetPaused(bool paused)
        {
            motionPaused = paused;
            if (animator != null)
            {
                animator.speed = paused ? 0f : approaching ? arrivalAnimationMultiplier :
                    (level == PigeonMovementLevel.Static && !HasAnimation(idleState) ? 0f : 1f);
            }
        }

        private void Update()
        {
            if (motionPaused) return;
            float dt = Time.deltaTime;

            if (approaching)
            {
                MoveTowardsDestination(approachSpeed, dt);
                if (Vector3.Distance(transform.position, destination) < 0.025f)
                {
                    approaching = false;
                    transform.position = destination;
                    if (animator != null) animator.speed = 1f;
                    if (level == PigeonMovementLevel.Static) PlayStationary();
                    else SettleAndExplore();
                }
                return;
            }

            if (level == PigeonMovementLevel.Static) return;

            if (idleRemaining > 0f)
            {
                idleRemaining -= dt;
                if (idleRemaining <= 0f) SelectNextDestination();
                return;
            }

            MoveTowardsDestination(flying ? flyingSpeed : WalkSpeed(), dt);
            if ((destination - transform.position).sqrMagnitude < 0.0006f)
            {
                if (flying && !landing)
                {
                    landing = true;
                    destination = RandomGroundPoint();
                }
                else
                {
                    flying = false;
                    landing = false;
                    SettleAndExplore();
                }
            }
        }

        private void MoveTowardsDestination(float speed, float dt)
        {
            Vector3 direction = destination - transform.position;
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude > 0.0001f)
            {
                Quaternion facing = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing,
                    turnDegreesPerSecond * dt);
            }
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * dt);
        }

        private void SettleAndExplore()
        {
            if (HasAnimation(idleState) && Random.value < idleProbability)
            {
                idleRemaining = Random.Range(0.6f, 1.4f);
                PlayAnimation(idleState);
            }
            else SelectNextDestination();
        }

        private void PlayStationary()
        {
            if (HasAnimation(idleState)) PlayAnimation(idleState);
            else
            {
                PlayAnimation(walkState);
                if (animator != null) animator.speed = 0f;
            }
        }

        private void SelectNextDestination()
        {
            idleRemaining = 0f;
            float flightChance = level == PigeonMovementLevel.High ? 0.20f :
                                 level == PigeonMovementLevel.Medium ? 0.07f : 0f;
            flying = Random.value < flightChance && HasAnimation(flyState);
            landing = false;
            destination = RandomGroundPoint();
            if (flying) destination.y = homePosition.y + flyingHeight;
            PlayAnimation(flying ? flyState : walkState);
        }

        private Vector3 RandomGroundPoint()
        {
            Vector2 offset = Random.insideUnitCircle * walkingRadius;
            return homePosition + new Vector3(offset.x, 0f, offset.y);
        }

        private float WalkSpeed()
        {
            switch (level)
            {
                case PigeonMovementLevel.Low: return 0.25f;
                case PigeonMovementLevel.Medium: return 0.42f;
                case PigeonMovementLevel.High: return 0.62f;
                default: return 0f;
            }
        }

        private bool HasAnimation(string state)
        {
            return animator != null && animator.runtimeAnimatorController != null &&
                   !string.IsNullOrEmpty(state) &&
                   animator.HasState(0, Animator.StringToHash(state));
        }

        private void PlayAnimation(string state)
        {
            if (activeAnimation == state || !HasAnimation(state)) return;
            activeAnimation = state;
            animator.CrossFadeInFixedTime(state, 0.12f, 0);
        }
    }
}
