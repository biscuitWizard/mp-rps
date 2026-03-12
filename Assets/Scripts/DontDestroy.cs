using UnityEngine;

/// <summary>
/// Marks this GameObject to persist across scene loads.
/// Attach to any object that needs to survive scene transitions
/// (e.g. the Main Camera, EventSystem).
/// </summary>
public class DontDestroy : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
