using UnityEngine;
using Photon.Pun;

public class InteractionManager : MonoBehaviour
{
    public PlayerController player;

    public Sprite DoorOpen, DoorClosed;

    [HideInInspector]
    public Collider2D colliderObject;

    private PhotonView photonView;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (player.view.ViewID != GameManager.Instance.myPlayer.view.ViewID) return;

        Debug.Log("Trigger Enter: " + other.gameObject.name);

        colliderObject = other;

        if (other.gameObject.CompareTag("Door"))
        {
            if (player.isPlayerMovingUsingMouse)
            {
                photonView.RPC("SyncDoorState", RpcTarget.AllBuffered, other.gameObject.name, true);
            }
            else
            {
                GameManager.Instance.SetInteractionMessage(true, "Press X to Open/Close");
                other.gameObject.GetComponent<SpriteRenderer>().color = Color.yellow;
            }
        }
        else if (other.gameObject.CompareTag("Chair"))
        {
            GameManager.Instance.SetInteractionMessage(true, "Press X to Sit");
            other.gameObject.GetComponentInParent<SpriteRenderer>().color = Color.yellow;
        }
        else if (other.gameObject.CompareTag("Broadcast"))
        {
            GameManager.Instance.SetInteractionMessage(true, "You are broadcasting");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (player.view.ViewID != GameManager.Instance.myPlayer.view.ViewID) return;

        Debug.Log("Trigger Exit: " + other.gameObject.name);

        if (other.gameObject.CompareTag("Door"))
        {
            other.gameObject.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else if (other.gameObject.CompareTag("Chair"))
        {
            other.gameObject.GetComponentInParent<SpriteRenderer>().color = Color.white;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetInteractionMessage(false, "");
        }

        colliderObject = null;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (player.view.ViewID != GameManager.Instance.myPlayer.view.ViewID) return;
        if (other.CompareTag("Untagged")) return;

        colliderObject = other;
    }

    void Update()
    {
        if (player.view.IsMine)
        {
            if (colliderObject != null)
                OnTriggerAction(colliderObject);
        }
    }

    public void OnTriggerAction(Collider2D other)
    {
        if (player.view.ViewID != GameManager.Instance.myPlayer.view.ViewID) return;

        if (Input.GetKeyDown(KeyCode.X))
        {
            Debug.Log("Trigger Action: " + other.gameObject.name);

            if (other.gameObject.CompareTag("Door"))
            {
                bool isOpen = other.gameObject.GetComponent<SpriteRenderer>().sprite == DoorOpen;
                photonView.RPC("SyncDoorState", RpcTarget.AllBuffered, other.gameObject.name, !isOpen);
            }
            else if (other.gameObject.CompareTag("Chair"))
            {
                player.transform.position = other.transform.position;
                player.transform.eulerAngles = other.transform.eulerAngles;
                player.transform.eulerAngles = new Vector3(0f, 0f, -other.transform.eulerAngles.z);

                GameManager.Instance.SetInteractionMessage(false);

                string chairDirection = other.gameObject.transform.GetChild(0).name;
                photonView.RPC("SyncChairState", RpcTarget.AllBuffered, chairDirection);
            }
        }
    }

    [PunRPC]
    void SyncDoorState(string doorName, bool open)
    {
        GameObject door = GameObject.Find(doorName);
        if (door != null)
        {
            door.GetComponent<SpriteRenderer>().sprite = open ? DoorOpen : DoorClosed;
            door.GetComponents<BoxCollider2D>()[1].enabled = !open;
        }
    }

    [PunRPC]
    void SyncChairState(string chairDirection)
    {
        if (chairDirection == "Front")
        {
            player.ControlAnimation(0, -1, true);
            player.ControlAnimation(0, 0, false);

        }
        else if (chairDirection == "Backward")
        {
            player.ControlAnimation(0, 1, true);
            player.ControlAnimation(0, 0, false);
        }
        else if (chairDirection == "Left")
        {
            player.ControlAnimation(-1, 0, true);
            player.ControlAnimation(0, 0, false);
        }
        else if (chairDirection == "Right")
        {
            player.ControlAnimation(1, 0, true);
            player.ControlAnimation(0, 0, false);
        }
    }
}
