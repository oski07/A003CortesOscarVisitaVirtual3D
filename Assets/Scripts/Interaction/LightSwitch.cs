using UnityEngine;

/// <summary>
/// Interruptor de paret: encen i apaga un grup de llums (i el material emissiu dels llums de sostre).
/// </summary>
public class LightSwitch : InteractableObject
{
    [Header("Llums controlats")]
    [SerializeField] private Light[] controlledLights;
    [SerializeField] private Renderer[] lampRenderers;
    [SerializeField] private Color lampEmissionColor = new Color(1f, 0.85f, 0.6f) * 3f;

    [Header("Interruptor")]
    [SerializeField] private Transform switchToggle;
    [SerializeField] private float toggleAngle = 18f;
    [SerializeField] private string roomName = "Habitacio";
    [SerializeField] private bool startOn = true;

    [Header("So")]
    [SerializeField] private AudioClip clickSfx;
    [SerializeField] private float sfxVolume = 0.8f;

    private bool isOn;
    private float[] baseIntensities;

    public bool IsOn => isOn;

    private void Start()
    {
        baseIntensities = new float[controlledLights.Length];
        for (int i = 0; i < controlledLights.Length; i++)
            baseIntensities[i] = controlledLights[i] != null ? controlledLights[i].intensity : 0f;
        SetState(startOn, false);
    }

    public void Configure(Light[] lights, Renderer[] lamps, Transform toggle, string room, AudioClip click)
    {
        controlledLights = lights;
        lampRenderers = lamps;
        switchToggle = toggle;
        roomName = room;
        clickSfx = click;
    }

    public override void Interact()
    {
        SetState(!isOn, true);
    }

    public override string GetPrompt()
    {
        return (isOn ? "Apagar el llum" : "Encendre el llum") + " (" + roomName + ")";
    }

    private void SetState(bool on, bool playSound)
    {
        isOn = on;
        foreach (Light l in controlledLights)
            if (l != null) l.enabled = on;

        foreach (Renderer r in lampRenderers)
        {
            if (r == null) continue;
            Material m = r.material;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", on ? lampEmissionColor : Color.black);
        }

        if (switchToggle != null)
            switchToggle.localRotation = Quaternion.Euler(on ? -toggleAngle : toggleAngle, 0f, 0f);

        if (playSound && clickSfx != null)
            AudioSource.PlayClipAtPoint(clickSfx, transform.position, sfxVolume);
    }
}
