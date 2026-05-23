using System.Collections;
using UnityEngine;

public class LoaderCallback : MonoBehaviour
{
    private bool isFirstUpdate = false;

    private void Update()
    {
        if (!isFirstUpdate)
        {
            isFirstUpdate = true;
            StartCoroutine(LoadCallbackWait());
        }
    }

    private IEnumerator LoadCallbackWait()
    {
        yield return new WaitForSeconds(3f);
        Loader.LoadCallback();
    }
}
