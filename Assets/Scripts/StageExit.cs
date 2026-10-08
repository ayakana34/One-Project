using UnityEngine;

// The door at the end of a stage: touching it moves the player on to the next stage.
[RequireComponent(typeof(BoxCollider2D))]
public class StageExit : MonoBehaviour
{
    void OnTriggerStay2D(Collider2D other)
    {
        if (GameManager.Instance != null && other.GetComponentInParent<Player>() != null)
            GameManager.Instance.OnExitReached();
    }
}
