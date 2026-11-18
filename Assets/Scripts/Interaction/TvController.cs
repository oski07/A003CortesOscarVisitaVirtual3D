using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Televisio amb VideoPlayer: encendre/apagar i canviar de canal (minim 2 videos).
/// Interactuar amb la pantalla encen/apaga; els botons (TvButton) canvien de canal.
/// Tecles extra quan el jugador mira la TV: no cal, tot va amb E.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class TvController : InteractableObject
{
    [Header("Canals")]
    [SerializeField] private VideoClip[] channels;
    [SerializeField] private string[] channelNames;

    [Header("Referencies")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Material screenOnMaterial;
    [SerializeField] private Material screenOffMaterial;
    [SerializeField] private AudioSource tvAudioSource;
    [SerializeField] private TextMesh channelLabel;
    [SerializeField] private Light screenGlow;

    [Header("SFX")]
    [SerializeField] private AudioClip powerOnSfx;
    [SerializeField] private AudioClip powerOffSfx;
    [SerializeField] private AudioClip channelChangeSfx;

    private VideoPlayer videoPlayer;
    private int currentChannel;
    private bool isOn;
    private Coroutine labelRoutine;

    public bool IsOn => isOn;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        if (tvAudioSource != null)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, tvAudioSource);
        }
    }

    private void Start()
    {
        ApplyPower(false, false);
    }

    public void Configure(VideoClip[] clips, string[] names, Renderer screen, Material onMat, Material offMat,
        AudioSource audioSource, TextMesh label, Light glow, AudioClip on, AudioClip off, AudioClip channelSfx)
    {
        channels = clips;
        channelNames = names;
        screenRenderer = screen;
        screenOnMaterial = onMat;
        screenOffMaterial = offMat;
        tvAudioSource = audioSource;
        channelLabel = label;
        screenGlow = glow;
        powerOnSfx = on;
        powerOffSfx = off;
        channelChangeSfx = channelSfx;
    }

    public override void Interact()
    {
        TogglePower();
    }

    public override string GetPrompt()
    {
        return isOn ? "Apagar la televisio" : "Encendre la televisio";
    }

    public void TogglePower()
    {
        ApplyPower(!isOn, true);
    }

    public void NextChannel()
    {
        ChangeChannel(1);
    }

    public void PreviousChannel()
    {
        ChangeChannel(-1);
    }

    private void ChangeChannel(int delta)
    {
        if (channels == null || channels.Length == 0) return;
        if (!isOn)
        {
            ApplyPower(true, true);
            return;
        }
        currentChannel = (currentChannel + delta + channels.Length) % channels.Length;
        if (channelChangeSfx != null) AudioSource.PlayClipAtPoint(channelChangeSfx, transform.position, 0.6f);
        PlayCurrent();
    }

    private void ApplyPower(bool on, bool playSound)
    {
        isOn = on;
        if (screenRenderer != null)
            screenRenderer.sharedMaterial = on ? screenOnMaterial : screenOffMaterial;
        if (screenGlow != null) screenGlow.enabled = on;

        if (on)
        {
            PlayCurrent();
            if (playSound && powerOnSfx != null) AudioSource.PlayClipAtPoint(powerOnSfx, transform.position, 0.7f);
        }
        else
        {
            videoPlayer.Stop();
            if (channelLabel != null) channelLabel.text = "";
            if (playSound && powerOffSfx != null) AudioSource.PlayClipAtPoint(powerOffSfx, transform.position, 0.7f);
        }
    }

    private void PlayCurrent()
    {
        if (channels == null || channels.Length == 0) return;
        videoPlayer.clip = channels[currentChannel];
        videoPlayer.Play();
        if (labelRoutine != null) StopCoroutine(labelRoutine);
        labelRoutine = StartCoroutine(ShowLabel());
    }

    private IEnumerator ShowLabel()
    {
        if (channelLabel == null) yield break;
        string channelName = channelNames != null && currentChannel < channelNames.Length ? channelNames[currentChannel] : "";
        channelLabel.text = "CH " + (currentChannel + 1) + "  " + channelName;
        yield return new WaitForSeconds(2.5f);
        channelLabel.text = "";
        labelRoutine = null;
    }
}
