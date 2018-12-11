using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Net : MonoBehaviour {

    public bool Server = false;
    public string Address = "127.0.0.1";
    public int Port = 8000;
    public int MaxConnection = 10;

    private List<int> connections = new List<int>();
    private Dictionary<Packets, Action<int, NetworkReader>> handlers = new Dictionary<Packets, Action<int, NetworkReader>>();
    private int hostId = -1;

    private static ConnectionConfig config;
    private static byte reliable;
    private static byte unreliable;
    private byte[] buffer = new byte[1024];

    private void Start() {

        //paylaşılan ayarlar
        if (config == null) {

            var globalConfig = new GlobalConfig();
            NetworkTransport.Init(globalConfig);

            config = new ConnectionConfig();
            reliable = config.AddChannel(QosType.ReliableFragmented);
            unreliable = config.AddChannel(QosType.UnreliableSequenced);

        }

        //soket oluştur
        var hostTopology = new HostTopology(config, MaxConnection);
        if (Server) {
            hostId = NetworkTransport.AddHost(hostTopology, Port);
        }
        else {
            hostId = NetworkTransport.AddHost(hostTopology);
        }

        //soket açıldı mı?
        if (hostId < 0) {
            Debug.LogError("Socket creation failed!");
            return;
        }

        //client ise sokete bağlan
        if (!Server) {
            byte error;
            NetworkTransport.Connect(hostId, Address, Port, 0, out error);
            LogError(error);
        }

    }

    //paket okuyucular
    public void AddHandler(Packets packetId, Action<int, NetworkReader> handler) {
        handlers[packetId] = handler;
    }

    //hata log
    private void LogError(byte error) {
        if ((NetworkError)error != NetworkError.Ok) {
            Debug.LogError((NetworkError)error);
        }
    }

    //send
    private void Send(int connectionId, int channelId, Packets packetId, Action<NetworkWriter> packet) {

        //paket hazır
        var w = new NetworkWriter();
        w.Write((byte)packetId);
        packet(w);
        var buffer = w.ToArray();

        //sal gitsin
        byte error;
        NetworkTransport.Send(hostId, connectionId, channelId, buffer, buffer.Length, out error);
        LogError(error);

    }

    private void SendAll(int channelId, Packets packetId, Action<NetworkWriter> packet) {

        //paket hazır
        var w = new NetworkWriter();
        w.Write((byte)packetId);
        packet(w);
        var buffer = w.ToArray();

        //tüm bağlantılara gönder
        foreach (var connectionId in connections) {
            byte error;
            NetworkTransport.Send(hostId, connectionId, channelId, buffer, buffer.Length, out error);
            LogError(error);
        }

    }

    //send reliable

    public void SendReliable(int connectionId, Packets packetId, Action<NetworkWriter> writer) {
        Send(connectionId, reliable, packetId, writer);
    }

    public void SendReliableAll(Packets packetId, Action<NetworkWriter> writer) {
        SendAll(reliable, packetId, writer);
    }

    //send unreliable

    public void SendUnreliable(int connectionId, Packets packetId, Action<NetworkWriter> writer) {
        Send(connectionId, unreliable, packetId, writer);
    }

    public void SendUnreliableAll(Packets packetId, Action<NetworkWriter> writer) {
        SendAll(unreliable, packetId, writer);
    }

    //listen

    private void Update() {

        //başlatılmadı
        if (hostId == - 1) return;

        //paket al
        Action<int, NetworkReader> handler;
        int recConnectionId;
        int recChannelId;
        int receivedSize;
        byte error;
        NetworkEventType type;

        //mesajları al
        do {

            type = NetworkTransport.ReceiveFromHost(hostId, out recConnectionId, out recChannelId, buffer, buffer.Length, out receivedSize, out error);
            LogError(error);

            //connect
            if (type == NetworkEventType.ConnectEvent) {
                connections.Add(recConnectionId);
                if (handlers.TryGetValue(Packets.Connected, out handler)) {
                    handler(recConnectionId, new NetworkReader());
                }
            }

            //disconnect
            else if (type == NetworkEventType.DisconnectEvent) {
                connections.Remove(recConnectionId);
                if (handlers.TryGetValue(Packets.Disconnected, out handler)) {
                    handler(recConnectionId, new NetworkReader());
                }
            }

            //data
            else if (type == NetworkEventType.DataEvent) {

                //paket handler bul
                var reader = new NetworkReader(buffer);
                var packetType = (Packets)reader.ReadByte();
                if (handlers.TryGetValue(packetType, out handler)) {
                    handler(recConnectionId, reader);
                }

                //götünüzden paket uydurmayın
                else {
                    Debug.LogError("Paket handler bulunamadı PacketId: " + packetType);
                }

            }

        }
        while (type != NetworkEventType.Nothing);

    }

}
