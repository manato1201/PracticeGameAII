using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float damage = 3f;
    [SerializeField] private GameObject Explosion;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") || other.CompareTag("Bat"))
        {
            other.gameObject.SendMessage("ApplyDamage", damage, SendMessageOptions.DontRequireReceiver);

            // 爆発エフェクトを生成
            if (Explosion != null)
            {
                Instantiate(Explosion, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
        else if (other.CompareTag("Ground") || other.CompareTag("Wall"))
        {
            // 爆発エフェクトを生成
            if (Explosion != null)
            {
                Instantiate(Explosion, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Destroy(gameObject, 5f);
    }
}