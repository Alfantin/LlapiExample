using UnityEngine;

public class Client : MonoBehaviour {

    void Start() {

        var net = GetComponent<Net>();

        net.AddHandler(Packets.Connected, (connetionId, reader) => {
            net.SendReliable(
                connetionId,
                Packets.Log,
                writer => writer.Write("Hello")
            );
        });

        net.AddHandler(Packets.Log, (connetionId, reader) => {
            Debug.Log(gameObject.name + " receive message: " + reader.ReadString());
        });

    }

}