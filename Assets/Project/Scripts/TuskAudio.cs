using UnityEngine;

public class TuskAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioSource loopSource;

    [Header("Movement Sounds")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landingSound;
    [SerializeField] private AudioClip runningSound;
    [SerializeField] private AudioClip idleSound;
    [SerializeField] private AudioClip afkSound;
    [SerializeField] private AudioClip fallingSound;
    [SerializeField] private AudioClip flyingSound;
    [SerializeField] private AudioClip dashSound;

    [Header("Loop Pitch Variation")]
    [SerializeField] private float minimumPitch = 0.94f;
    [SerializeField] private float maximumPitch = 1.06f;

    private AudioClip currentLoop;

    public void PlayJump()
    {
        PlayEffect(jumpSound);
    }

    public void PlayLanding()
    {
        PlayEffect(landingSound);
    }

    public void PlayDash()
    {
        PlayEffect(dashSound);
    }

    public void PlayRunning()
    {
        PlayLoop(runningSound);
    }

    public void StopRunning()
    {
        StopLoop(runningSound);
    }

    public void PlayIdle()
    {
        PlayLoop(idleSound);
    }

    public void StopIdle()
    {
        StopLoop(idleSound);
    }

    public void PlayAFK()
    {
        PlayLoop(afkSound);
    }

    public void StopAFK()
    {
        StopLoop(afkSound);
    }

    public void PlayFalling()
    {
        PlayLoop(fallingSound);
    }

    public void StopFalling()
    {
        StopLoop(fallingSound);
    }

    public void PlayFlying()
    {
        PlayLoop(flyingSound);
    }

    public void StopFlying()
    {
        StopLoop(flyingSound);
    }

    private void PlayEffect(AudioClip clip)
    {
        if (effectsSource == null || clip == null)
        {
            return;
        }

        effectsSource.PlayOneShot(clip);
    }

    private void PlayLoop(AudioClip clip)
    {
        if (loopSource == null || clip == null)
        {
            return;
        }

        if (currentLoop == clip &&
            loopSource.isPlaying)
        {
            return;
        }

        loopSource.Stop();

        loopSource.clip = clip;
        loopSource.loop = true;

        loopSource.pitch = Random.Range(
            minimumPitch,
            maximumPitch
        );

        loopSource.Play();

        currentLoop = clip;
    }

    private void StopLoop(AudioClip clip)
    {
        if (loopSource == null)
        {
            return;
        }

        if (currentLoop != clip)
        {
            return;
        }

        loopSource.Stop();
        loopSource.clip = null;
        loopSource.loop = false;

        currentLoop = null;
    }
}