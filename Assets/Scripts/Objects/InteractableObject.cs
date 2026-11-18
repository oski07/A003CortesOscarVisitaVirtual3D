using UnityEngine;

/// <summary>
/// Classe base de tots els objectes amb els quals el jugador pot interactuar (tecla E).
/// El PlayerController fa un raycast des de la camera i crida Interact().
/// </summary>
public class InteractableObject : MonoBehaviour
{
    public virtual void Interact()
    {
        Debug.Log("This objects is a son of the InteractableObject");
    }

    /// <summary>Text que es mostra al HUD quan el jugador mira l'objecte.</summary>
    public virtual string GetPrompt()
    {
        return "Interactuar";
    }
}
