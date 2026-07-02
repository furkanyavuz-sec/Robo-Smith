// DontDestroyHelper.cs
// Görev: NetworkManager sahne geçişlerinde hayatta kalsın.

using UnityEngine;

public class DontDestroyHelper : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}