using UnityEngine;

// =============================================================================
// TuskAudio  (Scripts/Tusk)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Plays Tusk's sounds. Other scripts just call PlayJump(), PlayDash(),
//   PlayRunning() and so on; this script does not decide WHEN to play.
//   One-shot sounds (jump, landing, dash, attack) use the Effects source. Looping
//   sounds (running, idle, falling, flying) share ONE Loop source, so only
//   one loop plays at a time and each gets a little random pitch.
//
// PUT IT ON   Tusk_Player (or a child) with two AudioSources assigned.
// =============================================================================
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
    [SerializeField] private AudioClip fallingSound;
    [SerializeField] private AudioClip flyingSound;
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip attackSound;

    [Header("Loop Pitch Variation")]
    [SerializeField] private float minimumPitch = 0.94f;
    [SerializeField] private float maximumPitch = 1.06f;

    // Only one looping sound can play at a time (they share loopSource).
    private AudioClip currentLoop;

    // One-shot effects
    public void PlayJump() => PlayEffect(jumpSound);
    public void PlayLanding() => PlayEffect(landingSound);
    public void PlayDash() => PlayEffect(dashSound);
    public void PlayAttack() => PlayEffect(attackSound);

    // Looping sounds
    public void PlayRunning() => PlayLoop(runningSound);
    public void StopRunning() => StopLoop(runningSound);

    public void PlayIdle() => PlayLoop(idleSound);
    public void StopIdle() => StopLoop(idleSound);

    public void PlayFalling() => PlayLoop(fallingSound);
    public void StopFalling() => StopLoop(fallingSound);

    public void PlayFlying() => PlayLoop(flyingSound);
    public void StopFlying() => StopLoop(flyingSound);

    /// <summary>Stops whatever looping sound is playing (used by the respawn).</summary>
    public void StopAllLoops()
    {
        if (loopSource == null)
            return;

        loopSource.Stop();
        loopSource.clip = null;
        loopSource.loop = false;

        currentLoop = null;
    }

    private void PlayEffect(AudioClip clip)
    {
        if (effectsSource == null || clip == null)
            return;

        effectsSource.PlayOneShot(clip);
    }

    private void PlayLoop(AudioClip clip)
    {
        if (loopSource == null || clip == null)
            return;

        // Already playing this loop.
        if (currentLoop == clip && loopSource.isPlaying)
            return;

        loopSource.Stop();

        loopSource.clip = clip;
        loopSource.loop = true;
        loopSource.pitch = Random.Range(minimumPitch, maximumPitch);
        loopSource.Play();

        currentLoop = clip;
    }

    // Only stops the loop if it is the one currently playing,
    // so an old "stop" can never cut off a newer sound.
    private void StopLoop(AudioClip clip)
    {
        if (loopSource == null || currentLoop != clip)
            return;

        loopSource.Stop();
        loopSource.clip = null;
        loopSource.loop = false;

        currentLoop = null;
    }
}
