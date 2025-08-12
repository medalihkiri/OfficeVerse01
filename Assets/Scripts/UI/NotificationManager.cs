using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance;
    
    [Header("Audio")]
    public AudioClip privateMessageSound;
    public AudioClip waveSound;
    public AudioClip publicMessageSound;
    private AudioSource audioSource;

    [Header("Visual")]
    public GameObject notificationBadge;
    public TextMeshProUGUI notificationCountText;
    private int notificationCount = 0;

    private void Awake()
    {
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void PlayPrivateMessageSound()
    {
        audioSource.PlayOneShot(privateMessageSound);
    }

    public void PlayWaveSound()
    {
        audioSource.PlayOneShot(waveSound);
    }

    public void PlayPublicMessageSound()
    {
        audioSource.PlayOneShot(publicMessageSound);
    }

    public void IncrementNotification()
    {
        if (!PrivateChat.Instance.isOpen)
        {
            notificationCount++;
            UpdateNotificationBadge();
        }
    }

    public void ClearNotifications()
    {
        notificationCount = 0;
        UpdateNotificationBadge();
    }

    private void UpdateNotificationBadge()
    {
        if (notificationCount > 0)
        {
            notificationBadge.SetActive(true);
            notificationCountText.text = notificationCount > 9 ? "9+" : notificationCount.ToString();
        }
        else
        {
            notificationBadge.SetActive(false);
        }
    }
}
