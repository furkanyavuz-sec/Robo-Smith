// ChassisSync.cs — MP Faz 2B: Şasi durumunun client aynası
// Görev: Şasinin tüm görünür durumu (statlar, silahlar+seviyeler, zırh,
//   modül, sinerji, parça sayaçları) server'da toplanıp NetworkVariable ile
//   yayınlanır; client kendi RobotChassis kopyasına uygular. Böylece
//   RobotStatusUI, ChassisPreviewBuilder hologramı, ArmorSelectUI ve tüm
//   ipuçları client'ta DOĞRU veriyle, değişiklik gerektirmeden çalışır.
// Kurulum: MapGenerator şasi üretirken ekler. Offline: tamamen pasif.

using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(RobotChassis))]
public class ChassisSync : NetworkBehaviour
{
    /// <summary>Şasinin ağa yazılan kompakt görünümü.</summary>
    public struct State : INetworkSerializable, System.IEquatable<State>
    {
        public int  hp, atk, spd, def;
        public byte armor, module, synergy;
        public byte plates, plasmas, chips;

        // Silah yuvaları: tip (-1 = boş), seviye, upgrade ilerlemesi
        public int  w0Type, w1Type, w2Type;
        public byte w0Lvl,  w1Lvl,  w2Lvl;
        public byte w0Prog, w1Prog, w2Prog;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref hp);
            serializer.SerializeValue(ref atk);
            serializer.SerializeValue(ref spd);
            serializer.SerializeValue(ref def);
            serializer.SerializeValue(ref armor);
            serializer.SerializeValue(ref module);
            serializer.SerializeValue(ref synergy);
            serializer.SerializeValue(ref plates);
            serializer.SerializeValue(ref plasmas);
            serializer.SerializeValue(ref chips);
            serializer.SerializeValue(ref w0Type);
            serializer.SerializeValue(ref w1Type);
            serializer.SerializeValue(ref w2Type);
            serializer.SerializeValue(ref w0Lvl);
            serializer.SerializeValue(ref w1Lvl);
            serializer.SerializeValue(ref w2Lvl);
            serializer.SerializeValue(ref w0Prog);
            serializer.SerializeValue(ref w1Prog);
            serializer.SerializeValue(ref w2Prog);
        }

        public bool Equals(State o) =>
            hp == o.hp && atk == o.atk && spd == o.spd && def == o.def &&
            armor == o.armor && module == o.module && synergy == o.synergy &&
            plates == o.plates && plasmas == o.plasmas && chips == o.chips &&
            w0Type == o.w0Type && w1Type == o.w1Type && w2Type == o.w2Type &&
            w0Lvl == o.w0Lvl && w1Lvl == o.w1Lvl && w2Lvl == o.w2Lvl &&
            w0Prog == o.w0Prog && w1Prog == o.w1Prog && w2Prog == o.w2Prog;
    }

    private readonly NetworkVariable<State> stateNv =
        new(default, NetworkVariableReadPermission.Everyone,
                     NetworkVariableWritePermission.Server);

    private RobotChassis chassis;
    private float        pollTimer;

    private const float POLL_INTERVAL = 0.25f;

    private void Awake() => chassis = GetComponent<RobotChassis>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            stateNv.OnValueChanged += OnStateChanged;
            chassis.ApplyNetworkMirror(stateNv.Value);   // İlk senkron
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) stateNv.OnValueChanged -= OnStateChanged;
        base.OnNetworkDespawn();
    }

    private void OnStateChanged(State oldState, State newState) =>
        chassis.ApplyNetworkMirror(newState);

    private void Update()
    {
        // Server: şasiyi düşük frekansta örnekle, değiştiyse yayınla
        if (!IsSpawned || !IsServer) return;

        pollTimer -= Time.deltaTime;
        if (pollTimer > 0f) return;
        pollTimer = POLL_INTERVAL;

        State s = Capture();
        if (!s.Equals(stateNv.Value)) stateNv.Value = s;
    }

    private State Capture()
    {
        RobotStatSheet sheet = chassis.StatSheet;
        var counts = chassis.PartCounts;

        State s = new State
        {
            hp      = sheet.HP,
            atk     = sheet.ATK,
            spd     = sheet.SPD,
            def     = sheet.DEF,
            armor   = (byte)chassis.EquippedArmor,
            module  = (byte)sheet.equippedModule,
            synergy = (byte)sheet.activeSynergy,
            plates  = (byte)counts.plates,
            plasmas = (byte)counts.plasmas,
            chips   = (byte)counts.chips,
            w0Type  = -1, w1Type = -1, w2Type = -1
        };

        for (int i = 0; i < sheet.weaponCount && i < 3; i++)
        {
            WeaponData w = sheet.equippedWeapons[i];
            if (w == null) continue;

            switch (i)
            {
                case 0: s.w0Type = (int)w.sourceItem;
                        s.w0Lvl  = (byte)w.upgradeLevel;
                        s.w0Prog = (byte)w.upgradeProgress; break;
                case 1: s.w1Type = (int)w.sourceItem;
                        s.w1Lvl  = (byte)w.upgradeLevel;
                        s.w1Prog = (byte)w.upgradeProgress; break;
                case 2: s.w2Type = (int)w.sourceItem;
                        s.w2Lvl  = (byte)w.upgradeLevel;
                        s.w2Prog = (byte)w.upgradeProgress; break;
            }
        }

        return s;
    }
}
