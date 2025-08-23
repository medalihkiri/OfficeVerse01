using UnityEngine;

public static class PlayerDataManager
{
    private static readonly string playerNameKey = "p_name";
    private static readonly string playerRoomNameKey = "p_room_name_da3";
    private static readonly string playerAvatarKey = "p_avatar_number";
    public static bool IsAuthenticated = false; // ✅ Track backend login
   // private static readonly string CurrentRoom = "current_room_id";


    public static string PlayerName
    {
        get => PlayerPrefs.GetString(playerNameKey, "");
        set => PlayerPrefs.SetString(playerNameKey, value);
    }

  /*  public static string CurrentRoomId
    {
        get => PlayerPrefs.GetString(CurrentRoom, "");
        set => PlayerPrefs.SetString(CurrentRoom, value);
    }*/



    public static string PlayerRoomName
    {
        get => PlayerPrefs.GetString(playerRoomNameKey, "");
        set => PlayerPrefs.SetString(playerRoomNameKey, value);
    }

    public static int PlayerAvatar
    {
        get => PlayerPrefs.GetInt(playerAvatarKey, -1);
        set => PlayerPrefs.SetInt(playerAvatarKey, value);
    }

    public static void ClearAllPlayerData()
    {
        PlayerPrefs.DeleteAll();
    }
}
