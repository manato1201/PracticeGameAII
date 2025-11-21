using UnityEngine;

public class LevelSystem : MonoBehaviour
{
    private int Level = 1;
    private int levelAmount = 0;

    void Start()
    {
        Level = 1;
        levelAmount = 0;
    }

    public void LevelAdd(int amount)
    {
        levelAmount += amount;
        LevelUpCheck();
    }

    private void LevelUpCheck()
    {
        if(levelAmount >= 100)
        {
            Level++;
            levelAmount = 0;
        }
    }
}
