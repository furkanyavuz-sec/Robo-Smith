// BeaconSpin.cs
// Kaynak işaret küpü — yavaşça döner, dikkat çeker.

using UnityEngine;

public class BeaconSpin : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 60f;

    public void SetSpeed(float dps) => degreesPerSecond = dps;

    private void Update() =>
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
}
