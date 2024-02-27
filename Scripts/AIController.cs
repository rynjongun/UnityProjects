using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/*
 * AIController.cs
 * ----------------
 * Author: Ryan Jones
 * GitHub: https://github.com/rynjongun
 * 
 * Description:
 * This script controls the AI behavior using the NavMeshAgent for movement.
 * supports multiple states including Patrol, Chase, Attack, Death, and Idle, with customizable settings and scalability.
 *
 */


// Enum to represent the different states our AI can be in
public enum AIState { Patrol, Chase, Attack, Death, Idle }

// Requires the GameObject to have a NavMeshAgent and Animator component
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class AIController : MonoBehaviour
{
    [Header("Components")]
    // Reference to the NavMeshAgent component for movement
    public NavMeshAgent navMeshAgent;
    // Reference to the player's transform for chasing or attacking
    public Transform playerTransform;
    // Animator component for handling animations
    private Animator animator;

    [Header("Patrol Settings")]
    // Array of waypoints for patrolling
    public Transform[] waypoints;
    // Index of the current waypoint the AI is moving towards
    private int currentWaypointIndex = 0;
    // Speed of the AI while patrolling
    public float patrolSpeed = 3.5f;
    // Time to wait at a waypoint before moving to the next one
    public float waypointWaitTime = 2f;
    // Counter for the wait time at a waypoint
    private float waitTimeCounter;
    // Flag to indicate if the AI is currently walking (used for animation)
    public bool isWalking;

    [Header("Chase Settings")]
    // Speed of the AI while chasing the player
    public float chaseSpeed = 6f;
    // Time after which the AI loses interest in chasing the player if out of sight
    public float loseInterestTime = 5f;
    // Counter for the lose interest time
    private float loseInterestCounter;

    [Header("Attack Settings")]
    // Range within which the AI can attack the player
    public float attackRange = 5f;
    // Cooldown time between attacks
    public float attackCooldown = 1.5f;
    // Counter for the attack cooldown
    private float attackCooldownCounter;
    // Damage dealt to the player on each attack
    public int attackDamage = 1;
    // Flag to indicate if the AI is currently attacking (used for animation)
    public bool isAttacking;

    [Header("Sight Settings")]
    // Radius within which the AI can detect the player
    public float viewRadius = 15f;
    // Angle within which the AI can detect the player
    public float viewAngle = 90f;
    // LayerMask to detect the player
    public LayerMask playerMask;
    // LayerMask to detect obstacles blocking the view to the player
    public LayerMask obstacleMask;

    [Header("Idle Settings")]
    // Time for the AI to remain idle
    public float idleTime = 3f;
    // Counter for the idle time
    private float idleTimer;

    [Header("Animation Settings")]
    // Flag to indicate if the AI has an idle animation
    public bool hasIdleAnimation = false;

    // Current state of the AI
    private AIState currentState;
    // Flag to indicate if the player is visible to the AI
    private bool isPlayerVisible;
    // Last known position of the player
    private Vector3 m_PlayerPosition;
    // Duration of the death animation
    public float deathAnimationDuration = 2f;

    private void Start()
    {
        // Initialize components and set initial AI state to Patrol
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform; 
        ChangeState(AIState.Patrol);
    }

    private void Update()
    {
        // Handle behavior based on the current state
        switch (currentState)
        {
            case AIState.Patrol:
                PatrolBehavior();
                break;
            case AIState.Chase:
                ChaseBehavior();
                break;
            case AIState.Attack:
                AttackBehavior();
                break;
            case AIState.Idle:
                IdleBehavior();
                break;
        }

        // If AI is in the Death state, skip the rest of the update
        if (currentState == AIState.Death)
        {
            return; 
        }

        // Check the environment to potentially change state based on player visibility
        EnvironmentView();
    }

    // Changes the AI's current state and updates necessary settings
    public void ChangeState(AIState newState)
    {
        // Prevent state change to Idle if there's no idle animation
        if (newState == AIState.Idle && !hasIdleAnimation)
        {
            return;
        }

        currentState = newState;

        switch (newState)
        {
            case AIState.Patrol:
                // Set up for patrol state
                navMeshAgent.speed = patrolSpeed;
                waitTimeCounter = waypointWaitTime;
                navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
                animator.SetTrigger("WalkState");
                break;
            case AIState.Chase:
                // Set up for chase state
                navMeshAgent.speed = chaseSpeed;
                loseInterestCounter = loseInterestTime;
                animator.SetTrigger("WalkState");
                break;
            case AIState.Attack:
                // Set up for attack state
                attackCooldownCounter = 0;
                animator.SetTrigger("AttackState");
                Debug.Log("Attacking the player!");
                break;
            case AIState.Idle:
                // Set up for idle state
                navMeshAgent.speed = 0;
                animator.SetTrigger("IdleState");
                break;
            case AIState.Death:
                // Handle death state: disable NavMeshAgent and trigger death animation
                navMeshAgent.enabled = false;
                animator.SetTrigger("DeathState");
                StartCoroutine(DeactivateAfterDeath());
                break;
        }
    }

    // Patrol behavior: Move to waypoints and wait before moving to the next
    private void PatrolBehavior()
    {
        // Check if reached the current waypoint
        if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            if (waitTimeCounter <= 0)
            {
                // Move to the next waypoint
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
                waitTimeCounter = waypointWaitTime;
                animator.SetTrigger("WalkState");
            }
            else
            {
                // Wait at the current waypoint
                waitTimeCounter -= Time.deltaTime;
                if (!isWalking && hasIdleAnimation)
                {
                    ChangeState(AIState.Idle);
                }
            }
        }
    }

    // Idle behavior: Trigger idle animation if available
    private void IdleBehavior()
    {
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && hasIdleAnimation)
        {
            animator.SetTrigger("IdleState");
        }
        else if (!hasIdleAnimation)
        {
            waitTimeCounter = 0;
        }
    }

    // Chase behavior: Chase the player if visible
    private void ChaseBehavior()
    {
        if (isPlayerVisible)
        {
            navMeshAgent.SetDestination(playerTransform.position);
            loseInterestCounter = loseInterestTime;
            animator.SetTrigger("WalkState");
        }
        else
        {
            if (loseInterestCounter <= 0)
            {
                ChangeState(AIState.Patrol);
            }
            else
            {
                loseInterestCounter -= Time.deltaTime;
            }
        }
    }

    // Attack behavior: Attack the player if in range
    private void AttackBehavior()
    {
        if (attackCooldownCounter <= 0)
        {
            if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
            {
                // Attack the player
                playerTransform.GetComponent<Health>().TakeDamage(attackDamage);
                Debug.Log("Attacking the player!");
                attackCooldownCounter = attackCooldown;
                animator.SetTrigger("AttackState");
            }
            else
            {
                // Return to chase or patrol if the player is out of attack range
                ChangeState(isPlayerVisible ? AIState.Chase : AIState.Patrol);
            }
        }
        else
        {
            attackCooldownCounter -= Time.deltaTime;
        }
    }

    // Check the environment to detect the player and change state accordingly
    private void EnvironmentView()
    {
        // Detect players within view radius
        Collider[] playerInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, playerMask);

        isPlayerVisible = false;
        for (int i = 0; i < playerInViewRadius.Length; i++)
        {
            Transform player = playerInViewRadius[i].transform;
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2)
            {
                float distToPlayer = Vector3.Distance(transform.position, player.position);
                if (!Physics.Raycast(transform.position, dirToPlayer, distToPlayer, obstacleMask))
                {
                    isPlayerVisible = true;
                    m_PlayerPosition = player.transform.position;
                    if (currentState != AIState.Attack)
                    {
                        ChangeState(AIState.Chase);
                    }
                }
            }
        }

        // Change to attack state if player is within attack range
        if (isPlayerVisible && Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            ChangeState(AIState.Attack);
        }
    }

    // Coroutine to deactivate the AI object after death animation
    private IEnumerator DeactivateAfterDeath()
    {
        yield return new WaitForSeconds(deathAnimationDuration);
        gameObject.SetActive(false);
    }
}
