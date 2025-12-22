using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Camera")][SerializeField] private Camera cam;
    [Header("Movement")]
    [SerializeField] private float camSensitivity = 20;
    [SerializeField] private float moveSensitivity = 3;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpForce = 5f;


    [Header("Inputs")]
    [SerializeField] private InputActionReference zqsd;
    [SerializeField] private InputActionReference powerUp;
    [SerializeField] private InputActionReference mouseMovement;
    [SerializeField] private InputActionReference fire;
    [SerializeField] private InputActionReference jump;

    [Header("GroundCheck")]
    [SerializeField] private float groundCheckDistance;
    [SerializeField] private LayerMask groundCheckMask;

    [Header("Weapons")]
    [SerializeField] private GunShooter gun;


    [SerializeField] public PlayerStats stats;


    private CharacterController controller;
    private float rotationX = 0.0f;
    private bool isGrounded = false;
    private Vector3 velocity = Vector3.zero;

    private bool isSpeedBoostActive = false;

    private bool stunEnable = false;
    private bool speedBoostEnable = false;
    private bool shockwaveEnable = false;
    private bool bombaEnable = false;
    private bool flameThrowerEnable = false;
    private bool poisonBulletEnable = false;
    private bool iceRayEnable = false;

    [Header("Cooldowns")]
    [SerializeField] private float stunCooldown = 8f;
    [SerializeField] private float speedBoostCooldown = 5f;
    [SerializeField] private float bombaCooldown = 10f;
    [SerializeField] private float flameThrowerCooldown = 10f;
    [SerializeField] private float iceRayCooldown = 10f;

    [SerializeField] private float flameThrowerDuration = 5f;
    [SerializeField] private float iceRayDuration = 5f;

    private float stunTimer = 0f;
    private float speedBoostTimer = 0f;
    private float bombaTimer = 0f;
    private float flameThrowerTimer = 0f;
    private float iceRayTimer = 0f;

    private AbilitySlotUI a1;
    private AbilitySlotUI a2;
    private AbilitySlotUI a3;
    private AbilitySlotUI a4;

    [Header("VFX")]
    [SerializeField] private ShockwaveVFX shockwavePrefab;

    // Slow state
    private bool isSlowed = false;
    private Coroutine slowCoroutine = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        stats = GetComponent<PlayerStats>();
        a1 = GameObject.Find("Slot_1").GetComponent<AbilitySlotUI>();
        a2 = GameObject.Find("Slot_2").GetComponent<AbilitySlotUI>();
        a3 = GameObject.Find("Slot_3").GetComponent<AbilitySlotUI>();
        a4 = GameObject.Find("Slot_4").GetComponent<AbilitySlotUI>();

        if (zqsd)
        {
            zqsd.action.Enable();
        }

        if (powerUp)
        {
            powerUp.action.performed += PowerUpPressed;
            powerUp.action.Enable();
        }

        if (mouseMovement)
        {
            mouseMovement.action.Enable();
        }

        if (jump)
        {
            jump.action.performed += JumpPressed;
            jump.action.Enable();
        }

        if (fire)
        {
            fire.action.performed += FirePressed;
            fire.action.Enable();
        }

        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ApplyPowerUp(string powerName)
    {
        if (powerName == "Stun")
        {
            if (!stunEnable)
            {
                stunEnable = true;
                a1.Unlock();
            }
            else
            {
                stats.stunDuration += 1f;
            }
        }
        else if (powerName == "SpeedBoost")
        {
            if (!speedBoostEnable)
            {
                speedBoostEnable = true;
                a2.Unlock();
            }
            else
            {
                stats.speedBoostMultiplier += 0.5f;
                stats.speedBoostDuration += 1f;
            }
        }
        else if (powerName == "Shockwave")
        {
            if (!shockwaveEnable)
            {
                shockwaveEnable = true;
            }
            else
            {
                stats.shockwaveDamage += 10f;
                stats.shockwaveRadius += 1f;
            }
        }
        else if (powerName == "Bomba")
        {
            if (!bombaEnable)
            {
                gun.AddModule("gun_module_rocket");
                bombaEnable = true;
                a3.Unlock();
            }
            else
            {
                stats.explosionDamage += 15f;
                stats.explosionRadius += 1f;
            }
        }
        else if (powerName == "FlameThrower")
        {
            if (!flameThrowerEnable)
            {
                gun.AddModule("gun_module_fire");
                flameThrowerEnable = true;
                a4.Unlock();
            }
            else
            {
                flameThrowerDuration += 2;
            }
        }
        else if (powerName == "PoisonBullets")
        {
            if (!poisonBulletEnable)
            {
                gun.AddModule("gun_module_poison");
                gun.PoisonBulletsEnable();
            }
            else
            {
                stats.poisonDamage += 3f;
                stats.poisonDuration += 1f;
            }
        }
        else if (powerName == "IceRay")
        {
            if (!iceRayEnable)
            {
                gun.AddModule("gun_module_laser");
                iceRayEnable = true;
            }
            else
            {
                stats.iceDuration += 1f;
            }
        }
    }

    private void PowerUpPressed(InputAction.CallbackContext obj)
    {
        var control = obj.control;

        if (stunEnable && control.name == "1" && stunTimer <= 0f)
        {
            StunAround();
            stunTimer = stunCooldown;
        }
        else if (speedBoostEnable && control.name == "2" && speedBoostTimer <= 0f)
        {
            SpeedBoost();
            speedBoostTimer = speedBoostCooldown;
        }
        else if (bombaEnable && control.name == "3" && bombaTimer <= 0f)
        {
            ThrowBomba();
            bombaTimer = bombaCooldown;
        }
        else if (flameThrowerEnable && control.name == "4" && flameThrowerTimer <= 0f)
        {
            FlameThrower();
            flameThrowerTimer = flameThrowerCooldown;
        }
        else if (control.name == "x")
        {
            PowerSelectionManager psm = FindObjectOfType<PowerSelectionManager>();
            if (psm != null)
            {
                psm.ShowPowerSelection();
            }
        }
    }

    public void IceRay()
    {
        StartCoroutine(IceRayRoutine());
    }

    private IEnumerator IceRayRoutine()
    {
        gun.IceRayEnable();

        yield return new WaitForSeconds(iceRayDuration);

        gun.IceRayDisable();

        yield return new WaitForSeconds(iceRayCooldown);
    }

    public void FlameThrower()
    {
        StartCoroutine(FlameRoutine());
    }

    private IEnumerator FlameRoutine()
    {
        gun.FlameThrowerEnable();

        yield return new WaitForSeconds(flameThrowerDuration);

        gun.FlameThrowerDisable();

        yield return new WaitForSeconds(flameThrowerCooldown);
    }

    private void ThrowBomba()
    {
        gun.Throw();
    }

    private void StunAround()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.shockwaveRadius);

        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.Stun(stats.stunDuration);
            }
        }
    }

    private void SpeedBoost()
    {
        if (!isSpeedBoostActive)
        {
            StartCoroutine(SpeedBoostRoutine());
            isSpeedBoostActive = true;
        }
    }

    private IEnumerator SpeedBoostRoutine()
    {
        float baseSpeed = stats.moveSpeed;
        stats.moveSpeed *= stats.speedBoostMultiplier;

        yield return new WaitForSeconds(stats.speedBoostDuration);

        stats.moveSpeed = baseSpeed;
        isSpeedBoostActive = false;
    }

    // ApplySlow: reduce player's moveSpeed by factor for duration (non-stacking)
    public void ApplySlow(float factor, float duration)
    {
        if (isSlowed)
            return;

        if (slowCoroutine != null) StopCoroutine(slowCoroutine);
        slowCoroutine = StartCoroutine(SlowRoutine(factor, duration));
    }

    private IEnumerator SlowRoutine(float factor, float duration)
    {
        isSlowed = true;
        float baseSpeed = stats.moveSpeed;
        stats.moveSpeed = baseSpeed * factor;

        yield return new WaitForSeconds(duration);

        stats.moveSpeed = baseSpeed;
        isSlowed = false;
        slowCoroutine = null;
    }

    private void DoShockwave()
    {
        //VFX
        if (shockwavePrefab != null)
        {
            ShockwaveVFX vfx = Instantiate(
                shockwavePrefab,
                new Vector3(transform.position.x, transform.position.y + 0.05f, transform.position.z),
                Quaternion.Euler(90, 0, 0)
            );

            vfx.Play(stats.shockwaveRadius);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, stats.shockwaveRadius);

        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(stats.shockwaveDamage);

                Vector3 dir = (enemy.transform.position - transform.position).normalized;

                enemy.Knockback(dir, 10f, 1f);
            }
        }
    }

    private void JumpPressed(InputAction.CallbackContext obj)
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            if (shockwaveEnable)
            {
                DoShockwave();
            }
        }
    }

    private void FirePressed(InputAction.CallbackContext obj)
    {
        gun.Shoot(stats.attackDamage);
    }

    // Update is called once per frame
    void Update()
    {

        isGrounded = Physics.CheckSphere(transform.position, groundCheckDistance, groundCheckMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float mouseX = mouseMovement.action.ReadValue<Vector2>().x * camSensitivity * Time.deltaTime;
        float mouseY = mouseMovement.action.ReadValue<Vector2>().y * camSensitivity * Time.deltaTime;

        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);
        cam.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.Rotate(Vector3.up * mouseX);

        Vector2 zqsdValue = zqsd.action.ReadValue<Vector2>();
        controller.Move(transform.TransformDirection(new Vector3(zqsdValue.x, 0, zqsdValue.y)).normalized * moveSensitivity * stats.moveSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime * 2f;
        controller.Move(velocity * Time.deltaTime);

        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            a1.UpdateCooldown(stunTimer, stunCooldown);
        }


        if (speedBoostTimer > 0)
        {
            speedBoostTimer -= Time.deltaTime;
            a2.UpdateCooldown(speedBoostTimer, speedBoostCooldown);
        }

        if (bombaTimer > 0)
        {
            bombaTimer -= Time.deltaTime;
            a3.UpdateCooldown(bombaTimer, bombaCooldown);
        }

        if (flameThrowerTimer > 0)
        {
            flameThrowerTimer -= Time.deltaTime;
            a4.UpdateCooldown(flameThrowerTimer, flameThrowerCooldown);
        }

        if (iceRayTimer > 0)
        {
            iceRayTimer -= Time.deltaTime;
        }

        if (iceRayEnable && iceRayTimer <= 0f)
        {
            IceRay();
            iceRayTimer = iceRayCooldown;
        }

    }

    public void Respawn(Vector3 respawnPosition)
    {
        controller.enabled = false;
        controller.transform.position = respawnPosition;
        controller.enabled = true;
    }
}