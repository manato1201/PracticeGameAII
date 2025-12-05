using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyGroup : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        int numChildren = this.transform.childCount;
        if(numChildren == 0){
            Destroy(gameObject);
        }
    }
}
