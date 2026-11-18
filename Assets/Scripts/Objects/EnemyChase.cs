using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Enemic cutre: una foto que sempre mira la camera i persegueix el jugador.
/// Si t'atrapa: crit + foto a la cara i es reinicia la visita.
/// </summary>
public class EnemyChase : MonoBehaviour
{
    [SerializeField] private Transform picture;
    [SerializeField] private float chaseSpeed = 1.4f;
    [SerializeField] private float startDelay = 5f;
    [SerializeField] private float killDistance = 0.45f;
    [SerializeField] private AudioClip screamSfx;

    private Transform player;
    private Camera cam;
    private NavMeshAgent agent;
    private bool caught;
    private float timer;

    public void Configure(Transform pic, AudioClip scream)
    {
        picture = pic;
        screamSfx = scream;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = chaseSpeed;
            if (!agent.isOnNavMesh) agent.enabled = false;
        }
    }

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        if (player == null || caught) return;

        // la foto sempre mira cap al jugador (billboard)
        if (cam != null && picture != null)
        {
            Vector3 look = cam.transform.position - picture.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) picture.rotation = Quaternion.LookRotation(-look);
        }

        timer += Time.deltaTime;
        if (timer < startDelay) return; // temps de marge per escapar

        if (agent != null && agent.enabled) agent.SetDestination(player.position);
        else
        {
            Vector3 target = new Vector3(player.position.x, transform.position.y, player.position.z);
            transform.position = Vector3.MoveTowards(transform.position, target, chaseSpeed * Time.deltaTime);
        }

        Vector2 a = new Vector2(transform.position.x, transform.position.z);
        Vector2 b = new Vector2(player.position.x, player.position.z);
        if (Vector2.Distance(a, b) < killDistance) StartCoroutine(Caught());
    }

    private void OnGUI()
    {
        if (!caught) return;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.RoundToInt(Screen.height * 0.12f);
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(0, Screen.height * 0.55f, Screen.width, Screen.height * 0.3f), "HAS MUERTO!!", style);
    }

    private IEnumerator Caught()
    {
        caught = true;
        if (agent != null && agent.enabled) agent.isStopped = true;
        if (screamSfx != null && cam != null) AudioSource.PlayClipAtPoint(screamSfx, cam.transform.position, 1f);
        if (cam != null && picture != null)
        {
            picture.SetParent(cam.transform, false);
            picture.localPosition = new Vector3(0f, -0.1f, 0.6f);
            picture.localRotation = Quaternion.identity;
            picture.localScale = new Vector3(0.6f, 0.9f, 1f);
        }
        yield return new WaitForSeconds(3f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
