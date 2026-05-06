using Sirenix.OdinInspector;
using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private MenuNavController navController;

    public MenuNavController NavController => navController;

    private void Reset()
    {
        navController = GetComponentInChildren<MenuNavController>(includeInactive: true);
    }

    private void Awake()
    {
        if (navController == null)
        {
            navController = GetComponentInChildren<MenuNavController>(includeInactive: true);
        }
    }
}
