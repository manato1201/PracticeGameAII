using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SimpleVoiceChanger : MonoBehaviour
{
    public AudioSource audioSource;

    // ピッチのランダム幅
    public float minPitch = 0.85f;
    public float maxPitch = 1.2f;

    void Reset()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayWithRandomPitch(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.Play();
    }
}
