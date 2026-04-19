using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    [Title("Singletons")]
    [SerializeField] private ClientManager clientPrefab;

    private void Start()
    {
        DontDestroyOnLoad(gameObject);

        Instantiate(clientPrefab);
    }
}
