using UnityEngine;

public class PersistentSfx : MonoBehaviour
{
    private static PersistentSfx instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // prevent duplicates
        }
    }

    public void PlaySound(AudioClip clip)
    {
        var audioSource = GetComponent<AudioSource>();
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
