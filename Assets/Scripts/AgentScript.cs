using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class AgentScript : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("Patrullaje")]
    [SerializeField] private List<Transform> targets = new List<Transform>();
    private int currentTargetIndex = 0;
    [SerializeField] private float reachThreshold = 0.5f;
    private bool isChasing = false;
    private bool finishedPatrol = false;

    [Header("Animación")]
    [SerializeField] private Animator anim;

    [Header("Detección del jugador")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField, Range(0f, 180f)] private float detectionAngle = 40f;
    [SerializeField] private LayerMask detectionMask = ~0;
    [SerializeField] private float eyeHeight = 1.6f;

    [Header("Captura")]
    [SerializeField] private float catchDistance = 1f;
    private bool gameOverTriggered = false;

    [Header("Persecución")]
    [SerializeField] private float loseSightTime = 2f;
    private float lastSeenTime = Mathf.NegativeInfinity;

    private Rigidbody rb;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        if (agent == null) Debug.LogError($"{name} no tiene NavMeshAgent!");
        agent.updateRotation = true;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (targets != null && targets.Count > 0)
        {
            finishedPatrol = false;
            agent.isStopped = false;
            agent.SetDestination(targets[currentTargetIndex].position);
        }
        else
        {
            finishedPatrol = true;
            agent.isStopped = true;
        }
    }

    private void Update()
    {
        if (gameOverTriggered) return;

        // DISTANCIA DE ATRAPAR JUGADOR
        if (player != null && Vector3.Distance(transform.position, player.position) <= catchDistance)
        {
            GameOver();
            return;
        }

        // LOGICA PRINCIPAL
        if (!isChasing)
        {
            DetectPlayer();
            Patrol();
        }
        else
        {
            HandleChaseBehavior();
        }

        if (anim != null)
            anim.SetFloat("Speed", agent.velocity.magnitude);
    }

    // -----------------------------
    //  LÓGICA PERSECUCIÓN CORREGIDA
    // -----------------------------
    private void HandleChaseBehavior()
    {
        if (player == null) return;

        if (CanSeePlayer())
        {
            lastSeenTime = Time.time;
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            // SI NO LO VE → FULL RESET
            if (Time.time - lastSeenTime > loseSightTime)
            {
                FullReset();
            }
        }
    }

    // -----------------------------
    //  DETECCIÓN REALISTA
    // -----------------------------
    private bool CanSeePlayer()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = player.position - origin;

        if (toPlayer.magnitude > detectionRange)
            return false;

        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
        if (angle > detectionAngle)
            return false;

        if (Physics.Raycast(origin, toPlayer.normalized, out RaycastHit hit, detectionRange, detectionMask))
        {
            return hit.transform == player || hit.collider.CompareTag("Player");
        }

        return false;
    }

    // -----------------------------
    //  PATRULLA
    // -----------------------------
    private void Patrol()
    {
        if (finishedPatrol) return;
        if (targets == null || targets.Count == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= reachThreshold)
        {
            currentTargetIndex++;

            if (currentTargetIndex >= targets.Count)
                currentTargetIndex = 0;

            agent.SetDestination(targets[currentTargetIndex].position);
        }
    }

    // -----------------------------
    //  FULL RESET COMPLETO
    // -----------------------------
    public void FullReset()
    {
        // Debug.Log("FULL RESET ENEMIGO");

        // RESET VARIABLES
        isChasing = false;
        finishedPatrol = false;
        gameOverTriggered = false;

        // RESET RIGIDBODY
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // RESET NAVMESHAGENT
        if (agent != null)
        {
            agent.enabled = false;
            agent.ResetPath();

            // ALINEAR AL NAVMESH
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }

            agent.enabled = true;
            agent.isStopped = false;
        }

        // RESET ANIMACION
        if (anim != null)
        {
            anim.SetFloat("Speed", 0);
        }

        // RESET PATRULLA
        currentTargetIndex = Random.Range(0, targets.Count);
        agent.SetDestination(targets[currentTargetIndex].position);
    }

    // -----------------------------
    //  DETECTAR JUGADOR
    // -----------------------------
    private void DetectPlayer()
    {
        if (player == null) return;
        if (CanSeePlayer())
        {
            isChasing = true;
            finishedPatrol = true;
            agent.isStopped = false;
            agent.SetDestination(player.position);
            lastSeenTime = Time.time;
        }
    }

    // -----------------------------
    //  GAME OVER
    // -----------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (gameOverTriggered) return;
        if (other.CompareTag("Player"))
        {
            GameOver();
        }
    }

    private void GameOver()
    {
        if (gameOverTriggered) return;
        gameOverTriggered = true;
        SceneManager.LoadScene("GameOverScene");
    }

    // -----------------------------
    //  GIZMOS
    // -----------------------------
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, detectionRange);

        Vector3 forward = transform.forward;
        Quaternion leftRot = Quaternion.Euler(0, -detectionAngle, 0);
        Quaternion rightRot = Quaternion.Euler(0, detectionAngle, 0);
        Vector3 leftDir = leftRot * forward;
        Vector3 rightDir = rightRot * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, leftDir * detectionRange);
        Gizmos.DrawRay(origin, rightDir * detectionRange);
    }
}