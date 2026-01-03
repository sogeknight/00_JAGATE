using UnityEngine;

public class VFXKillProbe : MonoBehaviour
{
    void OnDisable()
    {
        Debug.LogError("[VFXKillProbe] DISABLED: " + name);
        Debug.LogError(System.Environment.StackTrace);
    }

    void OnDestroy()
    {
        Debug.LogError("[VFXKillProbe] DESTROYED: " + name);
        Debug.LogError(System.Environment.StackTrace);
    }
}
