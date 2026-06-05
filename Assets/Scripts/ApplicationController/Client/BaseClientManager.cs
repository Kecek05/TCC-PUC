using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseClientManager : MonoBehaviour
{
    public ClientAuth ClientAuth { get; protected set; }
    
    public UserData UserData { get; protected set; }
    
    public abstract void ConnectClient();

    public abstract void DisconnectClient();

    public abstract Task<bool> JoinHost(string joinCode);

    /// <summary>
    /// Tears down the current match connection (host or client), waits until
    /// Netcode has fully stopped, then returns to the Main Menu. Safe to call
    /// from either side and re-entrant (a second call while leaving is a no-op).
    /// </summary>
    public abstract Task LeaveMatchAsync();
}
