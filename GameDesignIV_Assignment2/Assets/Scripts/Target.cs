using UnityEngine;

public class Target : MonoBehaviour
{
    [SerializeField] private int pointValue = 1;

    public void GetHit(int playerNumber)
    {
        ScoreManager.Instance.AddScore(playerNumber, pointValue);
        Destroy(gameObject);
    }
}