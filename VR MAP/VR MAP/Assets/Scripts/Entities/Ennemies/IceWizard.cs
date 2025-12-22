using System.Collections;
using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IceWizard : mêmes déplacements que Wizard, attaque via Animation Event.
/// Le clip d'attaque doit contenir un Animation Event appelant exactement : ShootAtTarget()
/// </summary>
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

    [Header("Attaque (animation -> ShootAtTarget)")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackDelayAfterTrigger = 0.15f;

    [Header("Impact (sol)")]
    [SerializeField] private GameObject impactPrefab;
    [SerializeField] private float impactDelay = 0.5f;
    [SerializeField] private float impactRadius = 1f; // conservé si besoin futur
    [SerializeField] private float impactDamage = 25f;

    [Header("Visuel du raycast")]
    [SerializeField] private float abovePlayerHeight = 10f;
    [SerializeField] private Color aimColor = Color.cyan;
    [SerializeField] private float aimWidth = 0.06f;

    [Header("Aim settings")]
    [Tooltip("Distance in meters from the player towards the wizard where the wizard will aim (1..2 recommended)")]
    [SerializeField] private float aimForwardDistance = 1.5f;

    [Header("Impact area settings")]
    [Tooltip("Length of the impact rectangle measured away from the impact point (meters)")]
    [SerializeField] private float impactLength = 2.0f;
    [Tooltip("Width of the impact rectangle (meters) — TOTAL width")]
    [SerializeField] private float impactWidth = 1.0f;

    [Header("Optional override")]
    [Tooltip("If set, IceWizard will use this Transform as the player reference (drag your Player GameObject here).")]
    [SerializeField] private Transform playerTransformOverride;

    // slow settings
    [Header("Slow settings")]
    [Tooltip("Multiplier applied to player moveSpeed (0.5 = half speed)")]
    [SerializeField] private float slowFactor = 0.5f;
    [Tooltip("Duration of the slow in seconds")]
    [SerializeField] private float slowDuration = 2f;

    [Header("Debug")]
    [Tooltip("Draw impact rectangle (visible LineRenderer) when attacking")]
    [SerializeField] private bool debugShowImpactRect = true;
    [Tooltip("Vertical tolerance (meters) to consider player within rectangle in Y axis")]
    [SerializeField] private float verticalTolerance = 1.0f;

    private static readonly int ParamIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int ParamShoot = Animator.StringToHash("Shoot");

    private Coroutine attackCoroutine;
    private bool isAttacking = false;
    private const float epsilon = 0.05f;

    // Saved player position at the moment of the shot (still kept for aim calculation)
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

        // Prefer PlayerController instance (real player root). This avoids accidentally using a camera transform.
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

            // rotation douce vers le joueur
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

    // Animation Event must call exactly "ShootAtTarget"
    public void ShootAtTarget()
    {
        // Resolve player transform (override first)
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

        // SAVE player position at the moment of the shot (used for aim)
        savedPlayerPosition = playerTransform.position;

        // Compute aim point on the line between saved player position and wizard:
        Vector3 playerPos = savedPlayerPosition;
        Vector3 dirPlayerToWizard = (transform.position - playerPos);
        if (dirPlayerToWizard.sqrMagnitude < 0.0001f) dirPlayerToWizard = Vector3.forward;
        dirPlayerToWizard = Vector3.ProjectOnPlane(dirPlayerToWizard.normalized, Vector3.up);

        // aim point = playerPos + dirPlayerToWizard * aimForwardDistance
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
        // Visual aim (temporary LineRenderer)
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

        // Orient particle to the shot direction (from firePoint to impactPoint)
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 shotDir = Vector3.ProjectOnPlane((impactPoint - origin).normalized, Vector3.up);
        Quaternion particleRot = shotDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(shotDir, Vector3.up) : Quaternion.identity;

        if (impactPrefab != null)
            Instantiate(impactPrefab, impactPoint, particleRot);

        // Resolve player object (use override if provided)
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

        // ---------- orientation for rectangle uses SAVED player position ----------
        Vector3 playerPosSaved = savedPlayerPosition;
        Vector3 toSavedPlayer = Vector3.ProjectOnPlane(playerPosSaved - impactPoint, Vector3.up);
        float distToSavedPlayer = toSavedPlayer.magnitude;
        Vector3 dirSaved = distToSavedPlayer > 0.0001f ? (toSavedPlayer / distToSavedPlayer) : Vector3.forward;

        // right axis perpendicular on XZ based on saved player direction
        Vector3 rightSaved = new Vector3(-dirSaved.z, 0f, dirSaved.x);

        // compute rectangle corners (impactPoint is near edge) using SAVED player orientation
        float halfWidth = impactWidth * 0.5f;
        Vector3 nearLeft = impactPoint - rightSaved * halfWidth;
        Vector3 nearRight = impactPoint + rightSaved * halfWidth;
        Vector3 farLeft = impactPoint + dirSaved * impactLength - rightSaved * halfWidth;
        Vector3 farRight = impactPoint + dirSaved * impactLength + rightSaved * halfWidth;

        // debug draw rectangle using LineRenderer (red, visible)
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

        // ---------- hit check uses PLAYER CURRENT position ----------
        Vector3 playerPosCurrent = playerObj.transform.position;
        Vector3 toPlayerCurrent = Vector3.ProjectOnPlane(playerPosCurrent - impactPoint, Vector3.up);
        float distToPlayerCurrent = toPlayerCurrent.magnitude;
        Vector3 dirCurrent = distToPlayerCurrent > 0.0001f ? (toPlayerCurrent / distToPlayerCurrent) : Vector3.forward;

        // lateral axis based on saved orientation (so rectangle stays aligned with saved player->wizard line)
        // but we project current player into that frame:
        float forwardCoord = Vector3.Dot(dirSaved, toPlayerCurrent);    // distance along saved forward
        float lateralCoord = Vector3.Dot(rightSaved, toPlayerCurrent);  // lateral against saved right

        // player vertical tolerance check (use current Y vs impactPoint Y)
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

            // immediate damage and slow: apply to the actual player object (if present)
            var ps = playerObj.GetComponent<PlayerStats>();
            if (ps != null)
            {
                // Apply slow (PlayerStats handles non-stacking)
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