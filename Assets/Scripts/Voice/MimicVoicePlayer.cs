using UnityEngine;

public class MimicVoicePlayer : MonoBehaviour
{
    public VoiceRecorder recorder;   // プレイヤー側の VoiceRecorder をインスペクタで参照
    public AudioSource audioSource;  // ミミック用 AudioSource
    public Transform targetPlayer;   // 近づいたら喋る対象

    public float minDistanceToSpeak = 15f;
    public float speakCooldown = 5f;

    private float _elapsedTime = 0f;
    private float _cooldownTimer = 0f;
    public SimpleVoiceChanger voiceChanger;

    void Update()
    {
        _elapsedTime += Time.deltaTime;
        _cooldownTimer -= Time.deltaTime;

        if (_cooldownTimer > 0f) return;
        if (recorder == null || recorder.recordedSegments.Count == 0) return;

        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        if (distance > minDistanceToSpeak) return;

        // フェーズ判定
        if (_elapsedTime < 300f) // 〜5分
        {
            PlayRandomSegment(); // 完全ランダム
        }
        else if (_elapsedTime < 900f) // 5〜15分
        {
            PlayRecentSegment(0.3f); // 新しめ優先
        }
        else // 15分以降
        {
            StartCoroutine(PlayConversationLike());
        }

        _cooldownTimer = speakCooldown;
    }

    private void PlayRandomSegment()
    {
        int idx = Random.Range(0, recorder.recordedSegments.Count);
        var clip = recorder.recordedSegments[idx];

        if (voiceChanger != null)
        {
            voiceChanger.PlayWithRandomPitch(clip);
        }
        else
        {
            audioSource.clip = clip;
            audioSource.pitch = 1f;
            audioSource.Play();
        }
    }

    // 最新の n% を優先する
    private void PlayRecentSegment(float recentRatio)
    {
        int count = recorder.recordedSegments.Count;
        int startIndex = Mathf.Max(0, count - Mathf.CeilToInt(count * recentRatio));
        int idx = Random.Range(startIndex, count);
        audioSource.clip = recorder.recordedSegments[idx];
        audioSource.Play();
    }

    private System.Collections.IEnumerator PlayConversationLike()
    {
        // 連続で2〜3個再生して「会話風」にする
        int chain = Random.Range(2, 4);
        for (int i = 0; i < chain; i++)
        {
            PlayRecentSegment(0.5f);
            yield return new WaitForSeconds(audioSource.clip.length + 0.5f);
        }
    }
}
