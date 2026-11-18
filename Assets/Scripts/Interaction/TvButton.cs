using UnityEngine;

/// <summary>
/// Boto fisic del panell de la televisio (Power, Canal +, Canal -).
/// </summary>
public class TvButton : InteractableObject
{
    public enum TvButtonAction
    {
        Power,
        NextChannel,
        PreviousChannel
    }

    [SerializeField] private TvController television;
    [SerializeField] private TvButtonAction action;
    [SerializeField] private AudioClip pressSfx;

    private Vector3 restPosition;

    private void Start()
    {
        restPosition = transform.position;
    }

    public void Configure(TvController tv, TvButtonAction buttonAction, AudioClip press)
    {
        television = tv;
        action = buttonAction;
        pressSfx = press;
    }

    public override void Interact()
    {
        if (television == null) return;
        if (pressSfx != null) AudioSource.PlayClipAtPoint(pressSfx, transform.position, 0.6f);
        StopAllCoroutines();
        StartCoroutine(PressAnimation());

        switch (action)
        {
            case TvButtonAction.Power: television.TogglePower(); break;
            case TvButtonAction.NextChannel: television.NextChannel(); break;
            case TvButtonAction.PreviousChannel: television.PreviousChannel(); break;
        }
    }

    public override string GetPrompt()
    {
        switch (action)
        {
            case TvButtonAction.Power: return television != null && television.IsOn ? "Apagar la TV" : "Encendre la TV";
            case TvButtonAction.NextChannel: return "Canal seguent";
            default: return "Canal anterior";
        }
    }

    private System.Collections.IEnumerator PressAnimation()
    {
        transform.position = restPosition + transform.forward * 0.008f;
        yield return new WaitForSeconds(0.12f);
        transform.position = restPosition;
    }
}
