#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Text;

public class SpeechToText_WindowsDictation : MonoBehaviour
{
    [Header("Link")]
    public VoiceRecorder recorder;

    [Header("Output")]
    [TextArea(2, 6)] public string recognizedText;     // 確定した文字列（=欲しいstring）
    [TextArea(2, 6)] public string liveHypothesis;     // 認識途中の文字列（任意）

    private DictationRecognizer _dictation;
    private StringBuilder _buffer = new StringBuilder();

    void Awake()
    {
        if (recorder == null) recorder = GetComponent<VoiceRecorder>();

        _dictation = new DictationRecognizer(ConfidenceLevel.Medium);

        _dictation.DictationResult += (text, confidence) =>
        {
            // ここが「確定」結果
            if (!string.IsNullOrEmpty(text))
            {
                if (_buffer.Length > 0) _buffer.Append(" ");
                _buffer.Append(text);
                recognizedText = _buffer.ToString();
            }
        };

        _dictation.DictationHypothesis += (text) =>
        {
            // 認識途中（UI表示用）
            liveHypothesis = text;
        };

        _dictation.DictationComplete += (cause) =>
        {
            // Stop() でも呼ばれる。エラー理由の確認に便利。
            // Debug.Log($"DictationComplete: {cause}");
        };

        _dictation.DictationError += (error, hresult) =>
        {
            Debug.LogError($"DictationError: {error} (0x{hresult:X})");
        };
    }

    void OnEnable()
    {
        if (recorder == null)
        {
            Debug.LogError("SpeechToText: recorder が未設定です");
            enabled = false;
            return;
        }

        recorder.OnVoiceStart += HandleVoiceStart;
        recorder.OnVoiceEnd += HandleVoiceEnd;
    }

    void OnDisable()
    {
        if (recorder != null)
        {
            recorder.OnVoiceStart -= HandleVoiceStart;
            recorder.OnVoiceEnd -= HandleVoiceEnd;
        }

        StopDictation();
    }

    private void HandleVoiceStart()
    {
        // 発話ごとにバッファをリセットしたいならここでClear
        _buffer.Clear();
        recognizedText = "";
        liveHypothesis = "";

        StartDictation();
    }

    private void HandleVoiceEnd(AudioClip _)
    {
        // ここで recognizedText に「その発話の結果」が残る
        StopDictation();
    }

    private void StartDictation()
    {
        if (_dictation == null) return;
        if (_dictation.Status == SpeechSystemStatus.Running) return;

        _dictation.Start();
    }

    private void StopDictation()
    {
        if (_dictation == null) return;
        if (_dictation.Status != SpeechSystemStatus.Running) return;

        _dictation.Stop();
    }

    void OnDestroy()
    {
        if (_dictation != null)
        {
            if (_dictation.Status == SpeechSystemStatus.Running) _dictation.Stop();
            _dictation.Dispose();
            _dictation = null;
        }
    }
}
#else
using UnityEngine;

public class SpeechToText_WindowsDictation : MonoBehaviour
{
    [TextArea(2, 6)] public string recognizedText;

    void Start()
    {
        Debug.LogError("DictationRecognizer は Windows向け(API)です。非Windows環境では動きません。");
    }
}
#endif
