using UnityEngine;

public class BreakableWall : MonoBehaviour
{
    public float health = 5f;

    public void TakeDamage(float damage)
    {
        health -= damage;

        if (health <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
