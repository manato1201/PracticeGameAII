using UnityEngine;
using System.Collections.Generic;

public class VoiceRecorder : MonoBehaviour
{
   [Header("Mic Settings")]
    public string microphoneDevice = null;
    public int sampleRate = 16000;
    public int bufferLengthSec = 10; // マイク用リングバッファ長さ

    [Header("VAD Settings")]
    [Range(0f, 1f)]
    public float startThreshold = 0.03f;   // これ以上で「声が出た」と判断
    [Range(0f, 1f)]
    public float stopThreshold = 0.015f;   // これ未満が続くと「無音」と判断
    public float minVoiceDuration = 0.3f;  // これ未満の短すぎる音は捨ててもいい
    public float minSilenceDuration = 0.2f;// これだけ無音が続いたら終了判定

    [Header("Output Clips")]
    public int maxSegments = 50;
    public List<AudioClip> recordedSegments = new List<AudioClip>();

    private AudioClip _micClip;
    private int _lastSamplePos = 0;
    private int _channels = 1;

    private bool _inVoice = false;
    private List<float> _currentSegment = new List<float>();
    private float _currentVoiceTime = 0f;
    private float _silenceTime = 0f;

    // 一時バッファ再利用用
    private float[] _tempBuffer;

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("マイクが見つかりません");
            enabled = false;
            return;
        }

        if (microphoneDevice == null)
        {
            microphoneDevice = Microphone.devices[0];
        }

        _micClip = Microphone.Start(microphoneDevice, true, bufferLengthSec, sampleRate);
        _lastSamplePos = 0;

        // 後でわかるけど、ここでは仮に1ch扱い（ステレオのときもLeftだけ見る想定）
        _channels = 1;

        // 最大バッファ長 = 全バッファぶん
        _tempBuffer = new float[bufferLengthSec * sampleRate * _channels];
    }

    void Update()
    {
        if (_micClip == null) return;

        int micPos = Microphone.GetPosition(microphoneDevice);
        if (micPos < 0) return;

        int totalSamples = _micClip.samples;
        int samplesAvailable = micPos - _lastSamplePos;
        if (samplesAvailable < 0)
        {
            // ループ跨ぎ
            samplesAvailable += totalSamples;
        }

        if (samplesAvailable <= 0) return;

        // 一度に処理するサンプル数を制限（安全策）
        if (samplesAvailable > _tempBuffer.Length)
        {
            samplesAvailable = _tempBuffer.Length;
        }

        // マイクバッファから新規サンプル取得
        _micClip.GetData(_tempBuffer, _lastSamplePos);

        // 各サンプルを順にVAD処理
        float sampleDeltaTime = 1f / sampleRate;

        for (int i = 0; i < samplesAvailable; i += _channels)
        {
            // チャンネル1個だけ観測（ステレオなら左ch）
            float s = _tempBuffer[i];
            float level = Mathf.Abs(s);

            if (_inVoice)
            {
                // 発話中
                _currentSegment.Add(s);
                _currentVoiceTime += sampleDeltaTime;

                if (level < stopThreshold)
                {
                    _silenceTime += sampleDeltaTime;
                }
                else
                {
                    _silenceTime = 0f;
                }

                // 一定時間無音が続いたら終了
                if (_silenceTime >= minSilenceDuration)
                {
                    EndSegment();
                }
            }
            else
            {
                // 無音状態 → 声が出たか確認
                if (level >= startThreshold)
                {
                    StartSegment(s);
                }
                // 何もしないときはスルー
            }
        }

        // 読み取ったぶんだけ進める
        _lastSamplePos += samplesAvailable;
        if (_lastSamplePos >= totalSamples)
        {
            _lastSamplePos -= totalSamples;
        }
    }

    private void StartSegment(float firstSample)
    {
        _inVoice = true;
        _currentSegment.Clear();
        _currentVoiceTime = 0f;
        _silenceTime = 0f;

        _currentSegment.Add(firstSample);
        _currentVoiceTime += 1f / sampleRate;
        // Debug.Log("Voice Start");
    }

    private void EndSegment()
    {
        _inVoice = false;
        _silenceTime = 0f;

        if (_currentVoiceTime < minVoiceDuration)
        {
            // 短すぎるノイズは破棄
            _currentSegment.Clear();
            // Debug.Log("Voice too short, discarded");
            return;
        }

        int sampleCount = _currentSegment.Count;
        float[] data = _currentSegment.ToArray();

        AudioClip segment = AudioClip.Create(
            "VoiceSegment",
            sampleCount,
            1,
            sampleRate,
            false
        );
        segment.SetData(data, 0);

        recordedSegments.Add(segment);
        if (recordedSegments.Count > maxSegments)
        {
            Destroy(recordedSegments[0]);
            recordedSegments.RemoveAt(0);
        }

        _currentSegment.Clear();
        _currentVoiceTime = 0f;

        // Debug.Log($"Voice End. Saved segment samples: {sampleCount}");
    }

    void OnDestroy()
    {
        if (_micClip != null)
        {
            Microphone.End(microphoneDevice);
        }
    }
}
