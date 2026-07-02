// ClientNetworkTransform.cs
// Görev: Owner-authoritative hareket senkronizasyonu.
// NGO'nun varsayılan NetworkTransform'u server-authoritative'dir —
// client kendi robotunu hareket ettiremezdi. Bu alt sınıf otoriteyi
// objenin sahibine (owner) verir. (Unity Boss Room örneğindeki pattern.)

using Unity.Netcode.Components;

public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
