using UnityEngine;

/// <summary>
/// Manages audio playback (footsteps and background music).
/// </summary>
public class AudioManager : MonoBehaviour
{
    private GameConfig _config;
    private AudioSource _footstepsSource;
    private AudioSource _musicSource;
    private bool _soundEnabled = true;
    private bool _isMoving = false;

    /// <summary>
    /// Initialize the audio manager.
    /// </summary>
    public void Initialize(GameConfig config)
    {
        _config = config;

        if (_config == null)
        {
            Debug.LogError("AudioManager: GameConfig is null!");
            return;
        }

        // Find audio sources
        Transform audioRoot = GameObject.Find("Audio")?.transform;
        if (audioRoot == null)
        {
            Debug.LogError("AudioManager: Audio GameObject not found!");
            return;
        }

        Transform footstepsTransform = audioRoot.Find("Footsteps");
        if (footstepsTransform != null)
        {
            _footstepsSource = footstepsTransform.GetComponent<AudioSource>();
            if (_footstepsSource == null)
            {
                Debug.LogWarning("AudioManager: Footsteps AudioSource not found!");
            }
        }
        else
        {
            Debug.LogWarning("AudioManager: Footsteps GameObject not found!");
        }

        Transform musicTransform = audioRoot.Find("Music");
        if (musicTransform != null)
        {
            _musicSource = musicTransform.GetComponent<AudioSource>();
            if (_musicSource == null)
            {
                Debug.LogWarning("AudioManager: Music AudioSource not found!");
            }
        }
        else
        {
            Debug.LogWarning("AudioManager: Music GameObject not found!");
        }

        SetSoundEnabled(true);
    }

    /// <summary>
    /// Updates footstep audio based on movement state.
    /// </summary>
    public void UpdateFootsteps(bool moving)
    {
        _isMoving = moving;

        if (_footstepsSource == null) return;
        if (!_soundEnabled)
        {
            _footstepsSource.Stop();
            return;
        }

        if (_config == null || _config.footstepLoop == null)
        {
            if (_footstepsSource.isPlaying)
                _footstepsSource.Stop();
            return;
        }

        _footstepsSource.clip = _config.footstepLoop;
        _footstepsSource.loop = true;
        _footstepsSource.volume = _config.footstepVolume;

        if (moving)
        {
            if (!_footstepsSource.isPlaying)
                _footstepsSource.Play();
        }
        else
        {
            if (_footstepsSource.isPlaying)
                _footstepsSource.Stop();
        }
    }

    /// <summary>
    /// Enables or disables sound globally.
    /// </summary>
    public void SetSoundEnabled(bool enabled)
    {
        _soundEnabled = enabled;
        AudioListener.volume = enabled ? 1f : 0f;

        if (_musicSource != null)
        {
            _musicSource.volume = _config != null ? _config.musicVolume : 0.45f;
            if (enabled)
            {
                if (_config != null && _config.backgroundMusic != null)
                {
                    _musicSource.clip = _config.backgroundMusic;
                    _musicSource.loop = true;
                    if (!_musicSource.isPlaying)
                        _musicSource.Play();
                }
            }
            else
            {
                _musicSource.Stop();
            }
        }

        if (!enabled)
        {
            UpdateFootsteps(false);
        }
    }

    /// <summary>
    /// Gets the current sound enabled state.
    /// </summary>
    public bool IsSoundEnabled()
    {
        return _soundEnabled;
    }
}
