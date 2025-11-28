using UnityEngine;
using System.Collections.Generic;

public class VoiceRecorder : MonoBehaviour
{
    public string microphoneDevice = null; // null ならデフォルト
    public int sampleRate = 16000;
    public float segmentLengthSec = 1.2f; // 1〜2秒くらいが扱いやすい
    public int maxSegments = 100;         // メモリ制限

    private AudioClip _recordClip;
    private int _lastSamplePos = 0;

    // ミミック用に公開する録音済みクリップ
    public List<AudioClip> recordedSegments = new List<AudioClip>();

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

        // 長めのバッファでループ録音
        int lengthSec = 300; // 5分バッファ
        _recordClip = Microphone.Start(microphoneDevice, true, lengthSec, sampleRate);
        _lastSamplePos = 0;
    }

    void Update()
    {
        if (_recordClip == null) return;

        int currentPos = Microphone.GetPosition(microphoneDevice);
        if (currentPos < 0) return;

        int samplesAvailable = currentPos - _lastSamplePos;
        if (samplesAvailable < 0)
        {
            // ループした
            samplesAvailable += _recordClip.samples;
        }

        // segmentLengthSec 分以上録音されていたら切り出す
        int segmentSamples = (int)(segmentLengthSec * sampleRate);
        while (samplesAvailable >= segmentSamples)
        {
            ExtractSegment(segmentSamples);
            samplesAvailable -= segmentSamples;
        }
    }

    private void ExtractSegment(int segmentSamples)
    {
        float[] data = new float[segmentSamples];

        // _lastSamplePos から segmentSamples 分コピー
        _recordClip.GetData(data, _lastSamplePos);

        // 新しいクリップを作ってデータをセット
        AudioClip segment = AudioClip.Create(
            "Segment",
            segmentSamples,
            _recordClip.channels,
            sampleRate,
            false
        );
        segment.SetData(data, 0);

        recordedSegments.Add(segment);
        if (recordedSegments.Count > maxSegments)
        {
            // 古いものから捨てる
            Destroy(recordedSegments[0]);
            recordedSegments.RemoveAt(0);
        }

        _lastSamplePos += segmentSamples;
        if (_lastSamplePos >= _recordClip.samples)
        {
            _lastSamplePos -= _recordClip.samples;
        }
    }
}
