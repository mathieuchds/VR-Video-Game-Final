using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Light))]
public class Ghost : Enemy
{
    [Header("Référence Cible")]
    [SerializeField] private GameObject targetObject;
    private Transform target;

    [Header("Paramètres de Mouvement")]
    [SerializeField] private float floatHeight = 1.5f;
    [SerializeField] private float floatAmplitude = 0.3f;
    [SerializeField] private float floatFrequency = 1f;

    [Header("Orbital")]
    [Tooltip("Rayon du cercle d'orbite autour du player")]
    [SerializeField] private float orbitRadius = 2.0f;
    [Tooltip("Vitesse angulaire (radians/s)")]
    [SerializeField] private float orbitSpeed = 1.5f;
    [SerializeField] private bool orbitClockwise = true;
    private float orbitAngle;
    [Tooltip("Distance minimale entre le fantôme et le joueur (évite qu'il 'colle')")]
    [SerializeField] private float minSeparation = 1.0f;

    [Header("Paramètres de Dégâts")]
    [SerializeField] private float damageRadius = 2.5f;
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float damageInterval = 1f;
    private float nextDamageTime;
    [Tooltip("Tolérance verticale (m) pour considérer le joueur dans la zone")]
    [SerializeField] private float verticalTolerance = 1.2f;

    [Header("Zone de Dégâts Visuelle")]
    [SerializeField] private Color damageZoneColor = new Color(1f, 0.3f, 0.1f);
    [SerializeField] private float lightIntensity = 3f;
    [SerializeField] private GameObject burnZonePrefab; 
    private GameObject burnZoneInstance;

    private Light damageLight;
    private float floatOffset;

    private Renderer rendLocal;
    private Color baseColorLocal;

    [Header("Debug")]
    [SerializeField] private bool debugShowImpactRect = true; // affiche le burnZone + logs
    [SerializeField] private bool debugVerbose = false; // logs détaillés

    void Start()
    {
        base.Start();
        // On ne laisse pas le NavMeshAgent déplacer le Ghost (on gère la trajectoire manuellement)
        agent = GetComponent<NavMeshAgent>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (agent != null)
            agent.enabled = false;

        // Santé initiale (hérité)
        health = maxHealth;
        if (healthBar != null) healthBar.SetHealth(1f);

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            target = playerGO.transform;
            if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Player trouvé par tag: {playerGO.name}");
        }
        else
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null)
            {
                target = pc.transform;
                if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Player trouvé par PlayerController: {pc.name}");
            }
            else if (targetObject != null)  
            {
                target = targetObject.transform;
                if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Player pris depuis targetObject: {targetObject.name}");
            }
            
        }

        damageLight = GetComponent<Light>();
        if (damageLight != null)
        {
            damageLight.type = LightType.Point;
            damageLight.color = damageZoneColor;
            damageLight.range = damageRadius;
            damageLight.intensity = lightIntensity;
            damageLight.shadows = LightShadows.None;
        }

        rendLocal = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (rendLocal != null) baseColorLocal = rendLocal.material.color;

        Collider col = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
        if (col != null)
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Collider mis en isTrigger pour éviter poussée physique du joueur.");
            }

            if (GetComponent<Rigidbody>() == null)
            {
                var rbNew = gameObject.AddComponent<Rigidbody>();
                rbNew.isKinematic = true;
            }
            else
            {
                rb.isKinematic = true;
            }
        }

        floatOffset = Random.Range(0f, Mathf.PI * 2f);

        if (target != null)
        {
            Vector3 toGhost = transform.position - target.position;
            orbitAngle = Mathf.Atan2(toGhost.z, toGhost.x);
        }
        else
        {
            orbitAngle = Random.Range(0f, Mathf.PI * 2f);
        }
        
    }

    void Update()
    {
        // si on n'a pas encore de target, tenter de la trouver chaque frame
        if (target == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null && playerGO.activeInHierarchy)
            {
                target = playerGO.transform;
                if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Player trouvé tardivement par tag : {target.name}");
            }
            else
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null && pc.gameObject.activeInHierarchy)
                {
                    target = pc.transform;
                    if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Player trouvé tardivement par PlayerController : {target.name}");
                }
                else if (targetObject != null && targetObject.activeInHierarchy)
                {
                    target = targetObject.transform;
                    if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Player pris depuis targetObject tardivement : {target.name}");
                }
            }

            if (target == null)
                return; // on attend la prochaine frame
        }

        float dir = orbitClockwise ? -1f : 1f;
        orbitAngle += dir * orbitSpeed * Time.deltaTime;

        Vector3 orbitOffset = new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * orbitRadius;
        Vector3 targetGround = new Vector3(target.position.x, target.position.y, target.position.z);

        float floatY = Mathf.Sin(Time.time * floatFrequency + floatOffset) * floatAmplitude;
        Vector3 desiredPos = targetGround + orbitOffset + Vector3.up * (floatHeight + floatY);

        Vector3 horizontalDesired = new Vector3(desiredPos.x, 0f, desiredPos.z);
        Vector3 horizontalTarget = new Vector3(target.position.x, 0f, target.position.z);
        float horizDist = Vector3.Distance(horizontalDesired, horizontalTarget);
        if (horizDist < minSeparation)
        {
            Vector3 dirOut = (horizontalDesired - horizontalTarget).normalized;
            if (dirOut.sqrMagnitude < 0.0001f) dirOut = Vector3.right;
            horizontalDesired = horizontalTarget + dirOut * minSeparation;
            desiredPos = new Vector3(horizontalDesired.x, desiredPos.y, horizontalDesired.z);
        }

        transform.position = Vector3.Lerp(transform.position, desiredPos, Mathf.Clamp01(speed * Time.deltaTime));

        transform.LookAt(new Vector3(target.position.x, transform.position.y, target.position.z));

        CheckDamageZone();

        if (debugVerbose)
        {
            Debug.Log($"[Ghost:{name}] pos={transform.position:F3} desired={desiredPos:F3} target={target.position:F3} horizDist={horizDist:F2}");
        }
    }


    private void CheckDamageZone()
    {
        if (Time.time < nextDamageTime) return;

        Vector3 center = transform.position;

        if (target != null)
        {
            Vector3 delta = target.position - center;
            float distXZ = new Vector2(delta.x, delta.z).magnitude;

            if (debugShowImpactRect)
                Debug.Log($"[Ghost:{name}] Horizontal distance to target = {distXZ:F3} (radius={damageRadius:F3})");

            if (distXZ <= damageRadius)
            {
                PlayerStats ps = null;
                
                ps = target.GetComponent<PlayerStats>();
                
                if (ps == null)
                    ps = target.GetComponentInParent<PlayerStats>();
                
                if (ps == null)
                    ps = target.GetComponentInChildren<PlayerStats>();
                
                if (ps == null)
                {
                    GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
                    if (playerGO != null)
                        ps = playerGO.GetComponent<PlayerStats>();
                }
                
                if (ps == null)
                {
                    ps = FindObjectOfType<PlayerStats>();
                    if (ps != null && debugShowImpactRect)
                        Debug.LogWarning($"[Ghost:{name}] PlayerStats trouvé via FindObjectOfType (fallback) sur {ps.gameObject.name}");
                }

                if (ps != null)
                {
                    ps.ApplyRawDamage(damageAmount);
                    nextDamageTime = Time.time + damageInterval;

                    if (debugShowImpactRect)
                        Debug.Log($"[Ghost:{name}] Applied RAW {damageAmount:F2} damage to {ps.gameObject.name} (distXZ={distXZ:F2})");
                }
                else
                {
                    if (debugShowImpactRect)
                        Debug.LogError($"[Ghost:{name}] PlayerStats introuvable sur target '{target.name}' ni ailleurs ! Vérifiez que PlayerStats est bien attaché au joueur.");
                }

                return;
            }
            else
            {
                if (debugShowImpactRect)
                    Debug.Log($"[Ghost:{name}] Player out of radius (distXZ={distXZ:F2}). No damage applied.");
            }
        }

        Collider[] hitColliders = Physics.OverlapSphere(center, damageRadius);
        if (debugShowImpactRect)
        {
            Debug.Log($"[Ghost:{name}] Fallback OverlapSphere center={center:F3} radius={damageRadius:F2} found={hitColliders.Length}");
            foreach (var h in hitColliders) Debug.Log($"  -> collider: {h.gameObject.name} layer={LayerMask.LayerToName(h.gameObject.layer)} tag={h.gameObject.tag}");
        }

        bool damaged = false;
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                PlayerStats ps = hitCollider.GetComponent<PlayerStats>() 
                              ?? hitCollider.GetComponentInParent<PlayerStats>() 
                              ?? hitCollider.GetComponentInChildren<PlayerStats>();
                
                if (ps != null)
                {
                    Vector3 d = ps.transform.position - center;
                    float distXZ = new Vector2(d.x, d.z).magnitude;
                    if (distXZ <= damageRadius)
                    {
                        ps.ApplyRawDamage(damageAmount);
                        damaged = true;
                        if (debugShowImpactRect) Debug.Log($"[Ghost:{name}] Fallback applied RAW {damageAmount:F2} to {ps.name} (distXZ={distXZ:F2})");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Ghost:{name}] collider tagged Player mais PlayerStats introuvable sur {hitCollider.gameObject.name}");
                }
            }
        }

        if (damaged)
            nextDamageTime = Time.time + damageInterval;
        else
        {
            if (debugShowImpactRect)
                Debug.Log($"[Ghost:{name}] Aucun joueur touché vérifie Tag 'Player', Collider et Layer Collision Matrix.");
        }
    }

    private new void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (debugShowImpactRect)
                Debug.Log($"[Ghost:{name}] Player entered trigger zone!");
            
            // On ne fait rien ici car CheckDamageZone() dans Update() gère déjà les dégâts périodiques
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (debugShowImpactRect)
                Debug.Log($"[Ghost:{name}] Player left trigger zone");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Vector3 center = transform.position; 
        Gizmos.DrawSphere(center, damageRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, damageRadius);
    }
}