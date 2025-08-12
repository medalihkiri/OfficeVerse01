using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AvatarSelection : MonoBehaviour
{
    public List<Sprite> Characters;

    [SerializeField] private Button changeAvatarButton;
    [SerializeField] private Image selectedAvatarImage;
    [SerializeField] private GameObject avatarSelectionPanel;
    [SerializeField] private Transform avatarTransform;
    [SerializeField] private GameObject avatarItem;
    
    public int selectedAvatarIndex;

    private void Start()
    {
        Characters = new List<Sprite>();

        for (int i = 0; i < CharacterManager.Instance.CharacterSpriteLibraries.Count; i++)
        {
            Characters.Add(CharacterManager.Instance.CharacterSpriteLibraries[i].GetSprite("Idle", "front"));
        }

        changeAvatarButton.onClick.RemoveAllListeners();
        changeAvatarButton.onClick.AddListener(OpenAvatarSelection);

        GameObject tempAvatarObject;

        for (int i = 0; i < Characters.Count; i++)
        {
            int i1 = i;
            tempAvatarObject = Instantiate(avatarItem, avatarTransform);
            tempAvatarObject.GetComponent<Image>().sprite = Characters[i];
            tempAvatarObject.GetComponent<Button>().onClick.AddListener(() => OnSelectAvatar(i1));
        }

        OnSelectAvatar(PlayerDataManager.PlayerAvatar == -1 ? Random.Range(0, Characters.Count) : PlayerDataManager.PlayerAvatar);
    }

    private void OpenAvatarSelection()
    {
        avatarSelectionPanel.SetActive(true);
    }

    private void OnSelectAvatar(int index)
    {
        selectedAvatarIndex = index;

        selectedAvatarImage.sprite = Characters[selectedAvatarIndex];

        avatarSelectionPanel.SetActive(false);
    }
}
