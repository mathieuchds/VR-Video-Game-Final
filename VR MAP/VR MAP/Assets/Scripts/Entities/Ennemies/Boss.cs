using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class Boss : Enemy
{
    private int mode = 0;
    private Transform player;

    public Transform laserSourceLeft; 
    public Transform laserSourceRight; 
    private LineRenderer lineLeft; 
    private LineRenderer lineRight; 
    private Vector3 currentLaserDirLeft; 
    private Vector3 currentLaserDirRight; 
    private Vector3 lockedDirLeft; 
    private Vector3 lockedDirRight;

    [Header("Movement")]
    public float chaseSpeed = 3.5f;
    public float dashSpeed = 20f;
    public float dashCooldown = 5f;
    public float dashDuration = 2f;
    public float dashDelay = 0.5f;
    public float stopDistance = 0.2f;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public float shootForce = 10f;
    public int bulletsPerBurst = 10;
    public int shootCooldown = 2;
    private bool isShooting = false;

    [Header("Laser")]
    public float laserDistance = 30f;
    public float laserDamage = 11f;
    public float sweepSpeed = 5f;
    public float laserWidth = 0.1f;

    [Header("Laser Phases")]
    public float warningTime = 1.5f; 
    public float laserDuration = 3f; 
    public Color warningColor = Color.yellow; 
    public Color activeColor = Color.red;


    public float sweepingDelay = 1f;
    private bool isSweeping = false;

    private Vector3 startDirection = Vector3.up;
    private LineRenderer line;

    private float dashTimer;
    private bool isDashing = false;

    private GameStateManager gameStateManager;
    private bool bossDead = false;


    private void PhaseSelect()
    {
        if (health > maxHealth * 0.60f)
            mode = 0;
        else if (health > maxHealth * 0.40f)
            mode = 1;
        else
            mode = 2;
    }


    private System.Collections.IEnumerator ShootRandomBullets()
    {
        isShooting = true;

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y = 0;
            randomDir.Normalize();

            GameObject b = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            Destroy(b, 3f);

            Rigidbody rb = b.GetComponent<Rigidbody>();
            rb.AddForce(randomDir * shootForce, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(shootCooldown);
        isShooting = false;
    }


    private System.Collections.IEnumerator Dash()
    {
        isDashing = true;
        Vector3 targetPos = player.position;

        agent.isStopped = true;
        yield return new WaitForSeconds(dashDelay);

        Vector3 direction = (targetPos - transform.position).normalized;
        float t = dashDuration;

        while (t > 0)
        {
            t -= Time.deltaTime;
            transform.position += direction * dashSpeed * Time.deltaTime;
            yield return null;
        }

        agent.isStopped = false;
        dashTimer = dashCooldown;
        isDashing = false;
    }


    protected void Start()
    {
        base.Start(); 

        agent = GetComponent<NavMeshAgent>();

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            player = playerGO.transform;

        dashTimer = dashCooldown;



        gameStateManager = FindObjectOfType<GameStateManager>(true);

        lineLeft = laserSourceLeft.GetComponent<LineRenderer>();
        lineRight = laserSourceRight.GetComponent<LineRenderer>();
        SetupLine(lineLeft);
        SetupLine(lineRight);
    }

    void SetupLine(LineRenderer line)
    {
        line.positionCount = 2;
        line.startWidth = laserWidth;
        line.endWidth = laserWidth;
        line.enabled = false;

        line.material = new Material(Shader.Find("Unlit/Color"));
        line.material.color = warningColor;
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else return;
        }

        PhaseSelect();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
        }

        if (mode >= 1)
        {
            dashTimer -= Time.deltaTime;

            if (!isDashing && !isSweeping && dashTimer <= 0f)
                StartCoroutine(Dash());
        }

        if (mode == 2)
        {
            if (!isShooting)
                StartCoroutine(ShootRandomBullets());
            else if (!isSweeping)
                StartCoroutine(SweepingLaser());
        }
    }

    private System.Collections.IEnumerator SweepingLaser()
    {
        isSweeping = true;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        lineLeft.enabled = true;
        lineRight.enabled = true;

        lineLeft.material.color = warningColor;
        lineRight.material.color = warningColor;

        float t = 0f;

        while (t < warningTime)
        {
            UpdateLaserTracking();
            t += Time.deltaTime;
            yield return null;
        }

        lockedDirLeft = (player.position - laserSourceLeft.position).normalized;
        lockedDirRight = (player.position - laserSourceRight.position).normalized;

        lineLeft.material.color = activeColor;
        lineRight.material.color = activeColor;

        t = 0f;

        while (t < laserDuration)
        {
            DrawLaser(laserSourceLeft, lockedDirLeft, lineLeft, true);
            DrawLaser(laserSourceRight, lockedDirRight, lineRight, true);
            t += Time.deltaTime;
            yield return null;
        }

        lineLeft.enabled = false;
        lineRight.enabled = false;

        agent.isStopped = false;
        isSweeping = false;
    }


    void UpdateLaserTracking()
    {
        currentLaserDirLeft =
            (player.position - laserSourceLeft.position).normalized;

        currentLaserDirRight =
            (player.position - laserSourceRight.position).normalized;

        DrawLaser(laserSourceLeft, currentLaserDirLeft, lineLeft, false);
        DrawLaser(laserSourceRight, currentLaserDirRight, lineRight, false);
    }


    void DrawLaser(
    Transform source,
    Vector3 dir,
    LineRenderer line,
    bool dealDamage
)
    {
        Vector3 origin = source.position + dir * 0.1f;
        Vector3 end = origin + dir * laserDistance;

        line.SetPosition(0, origin);
        line.SetPosition(1, end);

        if (!dealDamage)
            return;

        if (Physics.SphereCast(
            origin,
            laserWidth,
            dir,
            out RaycastHit hit,
            laserDistance,
            ~0,
            QueryTriggerInteraction.Collide
        ))
        {
            PlayerStats ps = hit.collider.GetComponentInParent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(laserDamage );
            }
        }
    }







}
