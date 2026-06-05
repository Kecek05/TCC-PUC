using System.Threading.Tasks;

using UnityEngine;

public abstract class BaseHostManager : MonoBehaviour
{
    public HostConnectionData CurrentHostConnectionData { get; protected set; }
    
    public abstract Task<bool> StartHostAsync();

    /// <summary>
    /// Deletes the lobby, shuts down the host's NetworkManager and completes only
    /// once Netcode has fully stopped, so a subsequent StartHost (replay) begins
    /// from a clean state. Does not change scene.
    /// </summary>
    public abstract Task ShutdownHostAsync();
}
