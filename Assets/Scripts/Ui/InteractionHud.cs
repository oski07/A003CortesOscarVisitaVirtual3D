using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de la visita: punt de mira i missatge "[E] ..." quan el jugador mira un objecte interactuable.
/// </summary>
public class InteractionHud : MonoBehaviour
{
    [SerializeField] private Text promptText;
    [SerializeField] private Image crosshair;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color activeColor = new Color(0.55f, 0.95f, 0.65f, 1f);

    private Camera playerCamera;

    public void Configure(Text prompt, Image cross)
    {
        promptText = prompt;
        crosshair = cross;
    }

    private void Update()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        InteractableObject target = null;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Ignore))
            target = hit.collider.GetComponent<InteractableObject>();

        if (promptText != null) promptText.text = target != null ? "[E]  " + target.GetPrompt() : "";
        if (crosshair != null)
        {
            crosshair.color = target != null ? activeColor : idleColor;
            crosshair.rectTransform.localScale = Vector3.one * (target != null ? 1.4f : 1f);
        }
    }
}
