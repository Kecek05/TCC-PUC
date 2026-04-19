using UnityEngine;

public abstract class BaseClientManager : MonoBehaviour
{
    public ClientAuth ClientAuth { get; protected set; }
    
    public UserData UserData { get; protected set; }
}
