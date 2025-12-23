using UnityEngine;

public class GunShooter : MonoBehaviour
{
    public GameObject projectilePrefab;
    public GameObject bombaPrefab;

    public Transform muzzle;
    public float shootForce = 500f;

    private bool isPoisonous = false;

    public void Shoot(float dmg)
    {

        GameObject bullet = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);

        // met les dégats du joueur
        Projectile p = bullet.GetComponent<Projectile>();
        if (p != null)
        {
            p.damage = dmg;
            p.isPoisonous = isPoisonous;
        }
            

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.AddForce(muzzle.forward * shootForce);

        Destroy(bullet, 5f);
        QuestManager.Instance?.AddProgress(QuestObjectiveType.ShootBullets);

    }

    public void Throw()
    {
        GameObject bomba = Instantiate(bombaPrefab, muzzle.position, muzzle.rotation);

        Rigidbody rb = bomba.GetComponent<Rigidbody>();
        rb.AddForce(muzzle.forward * shootForce);

        Destroy(bomba, 10f);
    }

    public void AddModule(string moduleName)
    {
        Transform module = transform.Find("gun_base/" + moduleName);

        if (module == null)
        {
            return;
        }

        module.gameObject.SetActive(true);
    }

    public void FlameThrowerEnable()
    {
        Transform fire1 = transform.Find("gun_base/gun_module_fire/fire1");

        if (fire1 != null)
        {
            fire1.gameObject.SetActive(true);  
        }
    }

    public void FlameThrowerDisable()
    {
        Transform fire1 = transform.Find("gun_base/gun_module_fire/fire1");

        if (fire1 != null)
        {
            fire1.gameObject.SetActive(false);
        }
    }

    public void IceRayEnable()
    {
        Transform t = transform.Find("gun_base/gun_module_laser/laser_module/laser_beam");
        if (t != null)
            t.gameObject.SetActive(true);


    }

    public void IceRayDisable()
    {
        Transform t = transform.Find("gun_base/gun_module_laser/laser_module/laser_beam");
        if (t != null)
            t.gameObject.SetActive(false);
    }


    public void PoisonBulletsEnable()
    {
        isPoisonous = true;
    }

}
