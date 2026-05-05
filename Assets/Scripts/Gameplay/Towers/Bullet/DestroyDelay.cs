using UnityEngine;

public class DestroyDelay : MonoBehaviour
{
    [SerializeField] private float delay = 1f;

    private void Start()
    {
        Destroy(gameObject, delay);
    }
}
