using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class Boss : Enemy
{
    private int mode = 0;
    private Transform player;

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
        else if (health > maxHealth * 0.25f)
            mode = 1;
        else
            mode = 2;
    }


    private System.Collections.IEnumerator SweepingLaser()
    {
        isSweeping = true;
        Vector3 targetPos = player.position;

        agent.isStopped = true;
        yield return new WaitForSeconds(sweepingDelay);

        float t = 0f;

        while (t < 1f)
        {
            Vector3 origin = transform.position;
            Vector3 targetDir = (targetPos - origin).normalized;
            Vector3 currentDir = Vector3.Lerp(startDirection, targetDir, t);
            Vector3 endPos = origin + currentDir * laserDistance;

            line.SetPosition(0, origin);
            line.SetPosition(1, endPos);

            if (Physics.SphereCast(origin, laserWidth, currentDir, out RaycastHit hit, laserDistance))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    hit.collider.GetComponent<PlayerStats>()?.TakeDamage(laserDamage);
                }
            }

            t += Time.deltaTime * sweepSpeed;
            yield return null;
        }

        agent.isStopped = false;
        isSweeping = false;
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

        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = laserWidth;
        line.endWidth = laserWidth;
        line.enabled = true;

        gameStateManager = FindObjectOfType<GameStateManager>(true);
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


 
}
