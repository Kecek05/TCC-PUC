using System;
using System.Collections.Generic;
using UnityEngine;

public static class ServiceLocator
{
    private static Dictionary<Type, object> services = new Dictionary<Type, object>();

    public static void Register<T>(T service) where T : class
    {
        services[typeof(T)] = service;
        GameLog.Info($"Registered: {service}");
    }
    

    public static T Get<T>() where T : class
    {
        if (services.TryGetValue(typeof(T), out object service))
        {
            return service as T;
        }
        else
        {
            throw new Exception($"Service of type {typeof(T).Name} not found");
        }
    }

    /// <summary>
    /// Non-throwing lookup for optional services (e.g. the bot controller, absent in some scenes).
    /// </summary>
    public static bool TryGet<T>(out T service) where T : class
    {
        if (services.TryGetValue(typeof(T), out object found))
        {
            service = found as T;
            return service != null;
        }

        service = null;
        return false;
    }

    public static void Unregister<T> () where T : class
    {
        services.Remove(typeof(T));
    }
}
