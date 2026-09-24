using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class EnemyBrain : MonoBehaviour
{
    public enum Species { Tin, Flame, Acid, Slime, Smoke }
    public enum Behaviour { Idle, Patrol, Chase, Attack, Return, Dead }
    public Species species;
    public Animator animator;
    public GameObject projectilePrefab;
    public float maximumHealth = 70f, moveSpeed = 1.5f, chaseSpeed = 2.6f;
    public float detectionRange = 12f, attackRange = 6f, preferredRange = 4f, leashRange = 20f;
    public float damage = 10f, cooldown = 2f, windup = 0.5f, attackDuration = 1f, projectileSpeed = 7f;
    public float patrolRadius = 2f;
    public Behaviour State { get; private set; }
    public float Health { get; private set; }
    private CharacterController motor;
    private PlayerVitals player;
    private Vector3 home, patrolGoal;
    private float gravitySpeed, nextDecision, nextAttack, attackStarted, lastSeen;
    private bool delivered;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private bool Ranged => projectilePrefab != null;
    private Vector3 Eye => transform.position + Vector3.up * motor.height * 0.65f;

    private void Awake()
    {
        motor = GetComponent<CharacterController>();
        home = transform.position; Health = maximumHealth;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        State = Behaviour.Idle; nextDecision = Time.time + Random.Range(1f, 3f);
    }
    private void Start() { player = FindFirstObjectByType<PlayerVitals>(); }
    private void Update()
    {
        if (State == Behaviour.Dead) return;
        bool available = player != null && !player.IsDead;
        float distance = available ? Vector3.Distance(transform.position, player.transform.position) : float.PositiveInfinity;
        bool visible = available && distance < detectionRange && CanSeePlayer();
        if (visible) lastSeen = Time.time;
        Vector3 direction = Vector3.zero;
        float speed = moveSpeed;
        if (State == Behaviour.Attack)
        {
            if (available) Face(player.transform.position - transform.position);
            float elapsed = Time.time - attackStarted;
            if (!delivered && elapsed >= windup)
            {
                delivered = true;
                if (available && distance <= attackRange + 0.3f && CanSeePlayer()) DeliverAttack();
            }
            if (elapsed >= attackDuration) State = Behaviour.Chase;
        }
        else if (State == Behaviour.Return)
        {
            direction = home - transform.position;
            if (Flat(direction).magnitude < 0.5f) { State = Behaviour.Idle; nextDecision = Time.time + 2f; }
        }
        else if (available && (visible || (State == Behaviour.Chase && Time.time - lastSeen < 3f)))
        {
            if (Vector3.Distance(home, transform.position) > leashRange || Vector3.Distance(home, player.transform.position) > leashRange + 5f)
                State = Behaviour.Return;
            else
            {
                State = Behaviour.Chase;
                Vector3 toPlayer = Flat(player.transform.position - transform.position);
                Face(toPlayer);
                speed = chaseSpeed;
                if (visible && distance <= attackRange && Time.time >= nextAttack)
                {
                    State = Behaviour.Attack; attackStarted = Time.time; delivered = false;
                    nextAttack = Time.time + attackDuration + cooldown;
                    if (animator != null) animator.SetTrigger(Attack);
                }
                else if (!visible || distance > (Ranged ? preferredRange : attackRange * 0.8f)) direction = toPlayer;
                else if (Ranged && distance < preferredRange * 0.65f) direction = -toPlayer;
            }
        }
        else
        {
            if (State == Behaviour.Chase) State = Behaviour.Return;
            else if (Time.time >= nextDecision)
            {
                State = State == Behaviour.Patrol ? Behaviour.Idle : Behaviour.Patrol;
                Vector2 offset = Random.insideUnitCircle * patrolRadius;
                patrolGoal = home + new Vector3(offset.x, 0f, offset.y);
                nextDecision = Time.time + Random.Range(2f, 4f);
            }
            if (State == Behaviour.Patrol) direction = patrolGoal - transform.position;
        }
        direction = Flat(direction);
        if (direction.magnitude < 0.2f) direction = Vector3.zero;
        if (State != Behaviour.Attack && direction != Vector3.zero)
        {
            direction = Steer(direction.normalized);
            if (State != Behaviour.Chase) Face(direction);
        }
        else direction = Vector3.zero;
        if (motor.isGrounded && gravitySpeed < 0f) gravitySpeed = -2f;
        gravitySpeed = Mathf.Max(-25f, gravitySpeed - 20f * Time.deltaTime);
        motor.Move((direction * speed + Vector3.up * gravitySpeed) * Time.deltaTime);
        if (animator != null)
            animator.SetFloat(Speed, Mathf.Clamp01(Flat(motor.velocity).magnitude / Mathf.Max(0.01f, chaseSpeed)), 0.15f, Time.deltaTime);
    }
    private Vector3 Steer(Vector3 direction)
    {
        // Collision-aware local steering works on the current unbaked test scene.
        for (int i = 0; i < 5; i++)
        {
            float angle = i == 0 ? 0 : (i % 2 == 0 ? -1 : 1) * ((i + 1) / 2) * 45f;
            Vector3 candidate = Quaternion.Euler(0, angle, 0) * direction;
            bool blocked = false;
            foreach (var hit in Physics.SphereCastAll(Eye, motor.radius, candidate, motor.radius + 0.5f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) { blocked = true; break; }
            if (blocked) continue;
            Vector3 ahead = transform.position + candidate * (motor.radius + 0.5f) + Vector3.up * 0.5f;
            if (Physics.Raycast(ahead, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return candidate;
        }
        return Vector3.zero;
    }
    private bool CanSeePlayer()
    {
        Vector3 delta = player.AimPoint - Eye;
        foreach (var hit in Physics.RaycastAll(Eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(player.transform)) return false;
        return true;
    }
    private void Face(Vector3 direction)
    {
        direction = Flat(direction);
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 180f * Time.deltaTime);
    }
    private void DeliverAttack()
    {
        if (Ranged)
        {
            Vector3 origin = Eye + transform.forward * (motor.radius + 0.15f);
            var shot = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(player.AimPoint - origin));
            shot.GetComponent<EnemyProjectile>().Launch(transform, player.AimPoint - origin, projectileSpeed, damage, species == Species.Acid ? 2f : 0f);
        }
        else player.ReceiveHit(species == Species.Smoke ? 0f : damage, species == Species.Slime ? 1.5f : 0f, species == Species.Smoke ? 2.5f : 0f);
    }
    public void TakeDamage(float amount)
    {
        if (State == Behaviour.Dead || amount <= 0f) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Health > 0f)
        {
            // Continuous weapon damage must not cancel and restart the attack windup every frame.
            if (State != Behaviour.Attack) State = Behaviour.Chase;
            lastSeen = Time.time;
            return;
        }
        State = Behaviour.Dead; motor.enabled = false;
        if (animator != null) animator.enabled = false;
        Destroy(gameObject, 0.2f); // No death clip was supplied.
    }
    private static Vector3 Flat(Vector3 value) { value.y = 0f; return value; }
}
