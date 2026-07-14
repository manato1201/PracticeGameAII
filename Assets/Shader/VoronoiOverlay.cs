using UnityEngine;
#if UNITY_UGUI_EXISTS || UNITY_EDITOR
using UnityEngine.UI; // RawImage/Graphic 用
#endif
using System.Collections;
public class VoronoiOverlay : MonoBehaviour
{
    public Renderer targetRenderer;
#if UNITY_UGUI_EXISTS || UNITY_EDITOR
    public Graphic targetGraphic;
#endif
    

    Material _mat;

    void Awake()
    {
        if (targetRenderer) _mat = Instantiate(targetRenderer.sharedMaterial);
#if UNITY_UGUI_EXISTS || UNITY_EDITOR
        else if (targetGraphic) _mat = Instantiate(targetGraphic.material);
#endif
        if (targetRenderer) targetRenderer.material = _mat;
#if UNITY_UGUI_EXISTS || UNITY_EDITOR
        if (targetGraphic) targetGraphic.material = _mat;
#endif

        // 推奨の静的パラメータ（上の表と合わせる）
        //_mat.SetFloat("_AlphaBlend", 1);
        //_mat.SetColor("_BGColor", new Color(0, 0, 0, 0.8f));
        //_mat.SetFloat("_OverlayAlpha", 1f);
        //_mat.SetColor("_BokehColor", Color.white);
        //_mat.SetFloat("_Chromatic", 0.35f);

        _mat.SetFloat("_Intensity", 1.2f);
        //_mat.SetFloat("_Threshold", 1.10f);
        //_mat.SetFloat("_BlurRadius", 6.0f);
        //_mat.SetFloat("_BlurIter", 4f);
        //_mat.SetFloat("_CellScale", 2f);
        _mat.SetFloat("_MoveSpeed", 0.35f);
        _mat.SetFloat("_Jitter", 0.33f);
        _mat.SetFloat("_Softness", 0.5f);
        _mat.SetFloat("_Seed", Random.Range(0f, 1000f));
    }

    void OnEnable() {
        StartCoroutine(FadeIntensity());
    }

    IEnumerator FadeIntensity()
    {
        float t = 0f, dur = 1.7f;
        float start = 1.2f, end = 0f;
        while (t < dur)
        {
            float u = t / dur;
            _mat.SetFloat("_Intensity", Mathf.Lerp(start, end, u));
            t += Time.deltaTime;
            yield return null;
        }
        _mat.SetFloat("_Intensity", 0f);

    }
}
