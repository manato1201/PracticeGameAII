using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public float FollowSpeed = 2f;

    // ★ Target を private にして Inspector に出さない
    [SerializeField] private Transform Target;

    private Transform camTransform;

    // Camera shake params
    public float shakeDuration = 0f;
    public float shakeAmount = 0.1f;
    public float decreaseFactor = 1.0f;

    Vector3 originalPos;

    void Awake()
    {
        Cursor.visible = false;

        if (camTransform == null)
            camTransform = GetComponent(typeof(Transform)) as Transform;
    }

    void Start()
    {
        // ★ Target が未設定の場合、自動で Player を探す
        if (Target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                Target = p.transform;
            }
        }
    }

    void OnEnable()
    {
        originalPos = camTransform.localPosition;
    }

    private void Update()
    {
        // ★ Target が存在しない場合は毎フレーム探索（エラー防止）
        if (Target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                Target = p.transform;
            }
            else
            {
                // Player がまだ生成されていない場合は何もせず return
                return;
            }
        }

        // ===== カメラ追従 =====
        Vector3 newPosition = Target.position;
        newPosition.z = -10;

        transform.position = Vector3.Slerp(
            transform.position,
            newPosition,
            FollowSpeed * Time.deltaTime
        );

        // ===== カメラシェイク =====
        if (shakeDuration > 0)
        {
            camTransform.localPosition = originalPos + Random.insideUnitSphere * shakeAmount;
            shakeDuration -= Time.deltaTime * decreaseFactor;
        }
    }

    // 外部から ShakeCamera() を呼べるように維持
    public void ShakeCamera()
    {
        originalPos = camTransform.localPosition;
        shakeDuration = 0.2f;
    }
}
