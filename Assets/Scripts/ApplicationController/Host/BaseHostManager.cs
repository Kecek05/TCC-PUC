using System.Threading.Tasks;

using UnityEngine;

public abstract class BaseHostManager : MonoBehaviour
{
    public HostConnectionData CurrentHostConnectionData { get; protected set; }
    
    public abstract Task StartHostAsync();
    
    public abstract void ShutdownHostAsync();
}
