using System.Threading.Tasks;

using UnityEngine;

public abstract class BaseHostManager : MonoBehaviour
{
    public string JoinCode { get; protected set; }
    
    public abstract Task StartHostAsync();
    
    public abstract void ShutdownHostAsync();
}
