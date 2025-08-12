using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using agora_gaming_rtc;
using Photon.Realtime;

public class SpatialAudio : MonoBehaviourPunCallbacks
{
    [SerializeField] private float radius;
    private PhotonView PV;
    private IRtcEngine agoraRtcEngine;
    private static readonly Dictionary<Player, SpatialAudio> spatialAudioFromPlayers = new Dictionary<Player, SpatialAudio>();
    private bool isInRoom = false;
    private int currentRoomId = 0;

    void Awake()
    {
        PV = GetComponent<PhotonView>();
        spatialAudioFromPlayers[PV.Owner] = this;
    }

    void OnDestroy()
    {
        spatialAudioFromPlayers.Remove(PV.Owner);
    }

    void Update()
    {
        if (!PV.IsMine) return; // Only run on the local player

        if (agoraRtcEngine == null)
        {
            agoraRtcEngine = GameManager.Instance.agoraClientManager.mRtcEngine;
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player == PV.Owner) continue; // Skip local player
            if (!player.CustomProperties.TryGetValue("agoraID", out object agoraIDObj)) continue;
            uint agoraID = uint.Parse(agoraIDObj.ToString());

            int volume;
            if (isInRoom)
            {
                // If local player is in a room, only allow audio from players in the same room
                if (SpatialRoom.PlayersInRooms.TryGetValue(player.ActorNumber, out int playerRoomId))
                {
                    volume = (playerRoomId == currentRoomId) ? 100 : 0;
                }
                else
                {
                    volume = 0;
                }
            }
            else
            {
                // If local player is not in a room, mute players who are in rooms
                if (SpatialRoom.PlayersInRooms.ContainsKey(player.ActorNumber))
                {
                    volume = 0;
                }
                else
                {
                    // Apply spatial audio for players outside rooms
                    if (!player.CustomProperties.TryGetValue("Name", out object NameObj)) continue;
                    string objName = NameObj.ToString();
                    GameObject otherPlayerObj = GameObject.Find(objName);

                    if (otherPlayerObj != null)
                    {
                        Vector3 otherPosition = otherPlayerObj.transform.position;
                        float distance = Vector3.Distance(transform.position, otherPosition);
                        volume = GetVolume(distance);
                    }
                    else
                    {
                        volume = 0;
                    }
                }
            }
            agoraRtcEngine = GameManager.Instance.agoraClientManager.mRtcEngine;
            agoraRtcEngine.AdjustUserPlaybackSignalVolume(agoraID, volume);
        }
    }

    private int GetVolume(float distance)
    {
        if (distance <= 0)
        {
            return 100;
        }
        else if (distance >= radius)
        {
            return 0;
        }
        else
        {
            return (int)(100 * (1 - distance / radius));
        }
    }

    public void SetInRoom(bool inRoom, int roomId)
    {
        isInRoom = inRoom;
        currentRoomId = roomId;
    }
}