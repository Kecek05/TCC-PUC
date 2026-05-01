using System;

public interface IOnPlayerLoaded
{
    /// <summary>
    /// Arg: AuthId
    /// </summary>
    public event Action<string> OnPlayerLoaded;
}
