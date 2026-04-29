using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseClientManager : MonoBehaviour
{
    public ClientAuth ClientAuth { get; protected set; }
    
    public UserData UserData { get; protected set; }
    
    public abstract void ConnectClient();
    
    public abstract Task<bool> JoinHost(string joinCode);
}
