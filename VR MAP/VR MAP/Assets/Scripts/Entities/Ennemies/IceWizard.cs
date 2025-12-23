using System.Collections;
using System;
using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(NavMeshAgent))]
public class IceWizard : Enemy
{
    [Header("Références")]
    [SerializeField] private GameObject targetObject;
    private Transform target;

    [SerializeField] private Transform firePoint;
    [SerializeField] private Animator animator;

    [Header("Distances / mouvement")]
    [SerializeField] private float stopDistance = 12f;

    [Header("Attaque")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackDelayAfterTrigger = 0.15f;

    [Header("Impact (sol)")]
    [SerializeField] private GameObject impactPrefab;
    [SerializeField] private float impactDelay = 0.5f;
    [SerializeField] private float impactRadius = 1f; 
    [SerializeField] private float impactDamage = 25f;

    [Header("Visuel du raycast")]
    [SerializeField] private float abovePlayerHeight = 10f;
    [SerializeField] private Color aimColor = Color.cyan;
    [SerializeField] private float aimWidth = 0.06f;

    [Header("Aim settings")]
    [SerializeField] private float aimForwardDistance = 1.5f;

    [Header("Impact area settings")]
    [SerializeField] private float impactLength = 2.0f;
    [SerializeField] private float impactWidth = 1.0f;

    [Header("Optional override")]
    [SerializeField] private Transform playerTransformOverride;

    // slow settings
    [Header("Slow settings")]
    [Tooltip("Multiplier applied to player moveSpeed (0.5 = half speed)")]
    [SerializeField] private float slowFactor = 0.5f;
    [Tooltip("Duration of the slow in seconds")]
    [SerializeField] private float slowDuration = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugShowImpactRect = true;
    [SerializeField] private float verticalTolerance = 1.0f;

    private static readonly int ParamIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int ParamShoot = Animator.StringToHash("Shoot");

    private Coroutine attackCoroutine;
    private bool isAttacking = false;
    private const float epsilon = 0.05f;

    private Vector3 savedPlayerPosition;

    void Start()
    {
        base.Start();

        agent = GetComponent<NavMeshAgent>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (agent != null)
        {
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (rb != null) rb.isKinematic = true;
        }

        health = maxHealth;
        if (healthBar != null) healthBar.SetHealth(1f);

        var pc = FindObjectOfType<PlayerController>();
        if (pc != null)
            target = pc.transform;
        else if (targetObject != null)
            target = targetObject.transform;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (animator == null) animator = GetComponent<Animator>();
        if (firePoint == null) firePoint = transform;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
        }
    }

    void Update()
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        bool shouldRun = dist > (stopDistance + epsilon);

        if (!isStunned)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (shouldRun)
                {
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                }
                else
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
            }

            if (animator != null)
                animator.SetBool(ParamIsRunning, shouldRun);

            bool canAttack = dist <= (stopDistance + epsilon);
            if (canAttack && !isAttacking)
            {
                attackCoroutine = StartCoroutine(AttackLoop());
            }
            else if (!canAttack && isAttacking)
            {
                if (attackCoroutine != null) StopCoroutine(attackCoroutine);
                attackCoroutine = null;
                isAttacking = false;
            }

            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
        }
    }

    private IEnumerator AttackLoop()
    {
        isAttacking = true;
        while (true)
        {
            if (isStunned)
            {
                yield return null;
            }
            else if (animator != null)
            {
                animator.SetTrigger(ParamShoot);
                yield return new WaitForSeconds(attackDelayAfterTrigger);
            }
            else
            {
                ShootAtTarget();
            }

            yield return new WaitForSeconds(attackCooldown);
        }
    }

    public void ShootAtTarget()
    {
        Transform playerTransform = playerTransformOverride;
        if (playerTransform == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;
            else if (targetObject != null && targetObject.CompareTag("Player"))
                playerTransform = targetObject.transform;
            else
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }
        }

        if (playerTransform == null) return;

        savedPlayerPosition = playerTransform.position;

        Vector3 playerPos = savedPlayerPosition;
        Vector3 dirPlayerToWizard = (transform.position - playerPos);
        if (dirPlayerToWizard.sqrMagnitude < 0.0001f) dirPlayerToWizard = Vector3.forward;
        dirPlayerToWizard = Vector3.ProjectOnPlane(dirPlayerToWizard.normalized, Vector3.up);

        Vector3 aimXZ = playerPos + dirPlayerToWizard * aimForwardDistance;
        Vector3 rayOrigin = new Vector3(aimXZ.x, playerPos.y + abovePlayerHeight, aimXZ.z);

        RaycastHit hit;
        Vector3 impactPoint;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, abovePlayerHeight * 2f))
        {
            impactPoint = hit.point;
        }
        else if (Terrain.activeTerrain != null)
        {
            float terrainY = Terrain.activeTerrain.SampleHeight(aimXZ) + Terrain.activeTerrain.GetPosition().y;
            impactPoint = new Vector3(aimXZ.x, terrainY, aimXZ.z);
        }
        else
        {
            impactPoint = new Vector3(aimXZ.x, playerPos.y, aimXZ.z);
        }

        StartCoroutine(ShowAimAndImpact(impactPoint));
    }

    private IEnumerator ShowAimAndImpact(Vector3 impactPoint)
    {
        GameObject go = new GameObject("IceAimLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = aimWidth;
        lr.endWidth = aimWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = aimColor;
        lr.endColor = aimColor;
        lr.SetPosition(0, firePoint != null ? firePoint.position : transform.position);
        lr.SetPosition(1, impactPoint);

        float timer = 0f;
        while (timer < impactDelay)
        {
            if (firePoint != null) lr.SetPosition(0, firePoint.position);
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(go);

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 shotDir = Vector3.ProjectOnPlane((impactPoint - origin).normalized, Vector3.up);
        Quaternion particleRot = shotDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(shotDir, Vector3.up) : Quaternion.identity;

        if (impactPrefab != null)
            Instantiate(impactPrefab, impactPoint, particleRot);

        GameObject playerObj = playerTransformOverride != null ? playerTransformOverride.gameObject : null;
        if (playerObj == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            playerObj = pc != null ? pc.gameObject : GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObj == null) yield break;
        if (!playerObj.activeInHierarchy)
        {
            Debug.Log($"[IceWizard:{name}] Player inactive, skipping slow/damage application.");
            yield break;
        }

        Vector3 playerPosSaved = savedPlayerPosition;
        Vector3 toSavedPlayer = Vector3.ProjectOnPlane(playerPosSaved - impactPoint, Vector3.up);
        float distToSavedPlayer = toSavedPlayer.magnitude;
        Vector3 dirSaved = distToSavedPlayer > 0.0001f ? (toSavedPlayer / distToSavedPlayer) : Vector3.forward;

        Vector3 rightSaved = new Vector3(-dirSaved.z, 0f, dirSaved.x);

        float halfWidth = impactWidth * 0.5f;
        Vector3 nearLeft = impactPoint - rightSaved * halfWidth;
        Vector3 nearRight = impactPoint + rightSaved * halfWidth;
        Vector3 farLeft = impactPoint + dirSaved * impactLength - rightSaved * halfWidth;
        Vector3 farRight = impactPoint + dirSaved * impactLength + rightSaved * halfWidth;

        if (debugShowImpactRect)
        {
            GameObject preview = new GameObject("ImpactRectPreview");
            var lrRect = preview.AddComponent<LineRenderer>();
            lrRect.positionCount = 5;
            lrRect.useWorldSpace = true;
            lrRect.loop = false;
            lrRect.material = new Material(Shader.Find("Sprites/Default"));
            lrRect.startColor = Color.red;
            lrRect.endColor = Color.red;
            lrRect.startWidth = Mathf.Max(0.05f, impactWidth * 0.15f); // visible thickness
            lrRect.endWidth = lrRect.startWidth;
            lrRect.SetPosition(0, nearLeft + Vector3.up * 0.02f);
            lrRect.SetPosition(1, nearRight + Vector3.up * 0.02f);
            lrRect.SetPosition(2, farRight + Vector3.up * 0.02f);
            lrRect.SetPosition(3, farLeft + Vector3.up * 0.02f);
            lrRect.SetPosition(4, nearLeft + Vector3.up * 0.02f);

            Destroy(preview, impactDelay + 1f);
        }

        Vector3 playerPosCurrent = playerObj.transform.position;
        Vector3 toPlayerCurrent = Vector3.ProjectOnPlane(playerPosCurrent - impactPoint, Vector3.up);
        float distToPlayerCurrent = toPlayerCurrent.magnitude;
        Vector3 dirCurrent = distToPlayerCurrent > 0.0001f ? (toPlayerCurrent / distToPlayerCurrent) : Vector3.forward;

        float forwardCoord = Vector3.Dot(dirSaved, toPlayerCurrent);    
        float lateralCoord = Vector3.Dot(rightSaved, toPlayerCurrent);  

        float yDiff = Mathf.Abs(playerPosCurrent.y - impactPoint.y);
        if (yDiff > verticalTolerance)
        {
            Debug.Log($"[IceWizard:{name}] MISS vertical (yDiff={yDiff:F2} > tol={verticalTolerance:F2})");
            yield break;
        }

        Debug.Log($"[IceWizard:{name}] debug coords savedForward={dirSaved} forwardCoord={forwardCoord:F2} lateralCoord={lateralCoord:F2}");

        if (forwardCoord >= 0f && forwardCoord <= impactLength && Math.Abs(lateralCoord) <= halfWidth)
        {
            Debug.Log($"[IceWizard:{name}] HIT player at currentPos {playerPosCurrent} impactPoint {impactPoint} (fwd={forwardCoord:F2}, lat={lateralCoord:F2}, yDiff={yDiff:F2})");

            var ps = playerObj.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.ApplySlow(slowFactor, slowDuration);
                ps.TakeDamage(impactDamage);
            }
        }
        else
        {
            Debug.Log($"[IceWizard:{name}] MISS player (fwd={forwardCoord:F2}, lat={lateralCoord:F2}, yDiff={yDiff:F2})");
        }
    }

    private void OnDisable()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
            isAttacking = false;
        }
    }
}