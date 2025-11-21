using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CountUpTimer : MonoBehaviour
{
    public GameObject BossBat;
    private Bat bossBatScript;   // ← life を持つスクリプト
    private float time;
    public TextMeshProUGUI TimerText;
    private bool timerRunning = true;
    public TextMeshProUGUI Rank;
    public Button Restart;

    void Start()
    {
        time = 0f;
        Rank.gameObject.SetActive(false);
        Restart.gameObject.SetActive(false);

        // BossBat のスクリプト取得
        bossBatScript = BossBat.GetComponent<Bat>();
    }

    void Update()
    {
        // ❶ life 0 でタイマー停止
        if (bossBatScript != null && bossBatScript.life <= 0f)
        {
            timerRunning = false;
            Ranks();
        }

        // ❷ 動いているときだけ時間を進める
        if (timerRunning)
        {
            time += Time.deltaTime;
        }

        // ❸ 表示は常に更新（止まった値のまま）
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int milliseconds = Mathf.FloorToInt((time - Mathf.Floor(time)) * 100f);

        TimerText.text = string.Format("{0:0}:{1:00}:{2:00}", minutes, seconds, milliseconds);
    }

    void Ranks()
    {
        Rank.gameObject.SetActive(true);

        if (time < 30f)
            Rank.text = "Rank S";
        else if (time < 60f)
            Rank.text = "Rank A";
        else if (time < 90f)
            Rank.text = "Rank B";
        else
            Rank.text = "Rank C";

        Restart.gameObject.SetActive(true);
    }
}
