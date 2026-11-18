using System.Collections;
using UnityEngine;

/// <summary>
/// Porta batent: gira al voltant de la frontissa (pivot) amb una animacio suau i SFX.
/// Aquest component va sobre el full de la porta (que te el collider); el pivot es el pare.
/// </summary>
public class DoorController : InteractableObject
{
    [SerializeField] private Transform hingePivot;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float openSpeed = 2.5f;
    [SerializeField] private string doorName = "Porta";

    [Header("So")]
    [SerializeField] private AudioClip openSfx;
    [SerializeField] private AudioClip closeSfx;

    private bool isOpen;
    private Quaternion closedRotation;
    private Coroutine moveRoutine;

    private void Start()
    {
        if (hingePivot == null) hingePivot = transform.parent;
        closedRotation = hingePivot.localRotation;
    }

    public void Configure(Transform pivot, float angle, string nameForPrompt, AudioClip open, AudioClip close)
    {
        hingePivot = pivot;
        openAngle = angle;
        doorName = nameForPrompt;
        openSfx = open;
        closeSfx = close;
    }

    public override void Interact()
    {
        isOpen = !isOpen;
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveDoor(isOpen));
        if (isOpen && openSfx != null) AudioSource.PlayClipAtPoint(openSfx, transform.position, 0.9f);
    }

    public override string GetPrompt()
    {
        return (isOpen ? "Tancar " : "Obrir ") + doorName.ToLower();
    }

    private IEnumerator MoveDoor(bool open)
    {
        Quaternion target = open ? closedRotation * Quaternion.Euler(0f, openAngle, 0f) : closedRotation;
        while (Quaternion.Angle(hingePivot.localRotation, target) > 0.5f)
        {
            hingePivot.localRotation = Quaternion.Slerp(hingePivot.localRotation, target, Time.deltaTime * openSpeed * 2f);
            yield return null;
        }
        hingePivot.localRotation = target;
        if (!open && closeSfx != null) AudioSource.PlayClipAtPoint(closeSfx, transform.position, 1f);
        moveRoutine = null;
    }
}
