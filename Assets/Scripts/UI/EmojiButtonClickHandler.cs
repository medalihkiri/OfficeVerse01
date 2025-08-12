using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.EventSystems;

public class EmojiButtonClickHandler : MonoBehaviour
{
    public TMP_InputField inputField;
    public TMP_InputField searchInputField;
    public GameObject emojiContent;
    public GameObject searchContent;
    public GameObject[] objectsToDisableOnSearch;
    public float hoverDuration = 0.2f;


    private void Start()
    {
        // Cache the initial state of searchContent (disabled).
        searchContent.SetActive(false);

        searchInputField.onSelect.AddListener(OnSearchInputFieldSelect);
        searchInputField.onDeselect.AddListener(OnSearchInputFieldDeselect);

        // Add listeners for search input changes
        searchInputField.onValueChanged.AddListener(OnSearchValueChanged);
        searchInputField.onEndEdit.AddListener(OnSearchEndEdit);

        // Add listeners to emoji buttons
        Button[] emojiButtons = emojiContent.GetComponentsInChildren<Button>(true);
        foreach (Button button in emojiButtons)
        {
            button.onClick.AddListener(() => HandleEmojiButtonClick(button));
        }

        // Add listeners to search result buttons (assuming they have the same structure)
        Button[] searchButtons = searchContent.GetComponentsInChildren<Button>(true);
        foreach (Button button in searchButtons)
        {
            button.onClick.AddListener(() => HandleEmojiButtonClick(button));
        }
    }

    public void OnSearchInputFieldSelect(string text)
    {
        searchInputField.placeholder.gameObject.SetActive(false);

        Debug.Log("Input field selected");
    }

    public void OnSearchInputFieldDeselect(string text)
    {
        Debug.Log("Input field deselected");

        searchInputField.placeholder.gameObject.SetActive(true);
    }

    private void OnSearchValueChanged(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            // Restore original state
            emojiContent.SetActive(true);
            searchContent.SetActive(false);
            foreach (GameObject obj in objectsToDisableOnSearch)
            {
                obj.SetActive(true);
            }
            return;
        }

        // Show search results and hide other elements
        emojiContent.SetActive(false);
        searchContent.SetActive(true);
        foreach (GameObject obj in objectsToDisableOnSearch)
        {
            obj.SetActive(false);
        }

        Button[] buttons = searchContent.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            bool matchesSearch = button.name.ToLower().Contains(searchText.ToLower());
            StartCoroutine(SmoothSetActive(button.gameObject, matchesSearch));
        }
    }


    private void OnSearchEndEdit(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            // Restore original state if search is empty after EndEdit
            emojiContent.SetActive(true);
            searchContent.SetActive(false);
            foreach (GameObject obj in objectsToDisableOnSearch)
            {
                obj.SetActive(true);
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log("Enter key pressed!");
            int currentCaretPosition = searchInputField.caretPosition;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(searchInputField.gameObject);

            StartCoroutine(SetPosition());

            IEnumerator SetPosition()
            {
                int width = searchInputField.caretWidth;
                searchInputField.caretWidth = 0;

                yield return new WaitForEndOfFrame();

                searchInputField.caretWidth = width;
                searchInputField.caretPosition = currentCaretPosition;
            }
        }
    }

    private void HandleEmojiButtonClick(Button button)
    {
        if (inputField == null)
        {
            Debug.LogError("InputField not assigned!");
            return;
        }

        Transform emojiTransform = button.transform.Find("Emoji"); // Adjust path if needed
        if (emojiTransform == null)
        {
            Debug.LogError("Emoji image not found under the button!");
            return;
        }

        Image emojiImage = emojiTransform.GetComponent<Image>();
        if (emojiImage == null || emojiImage.sprite == null)
        {
            Debug.LogError("Emoji image component or sprite is missing!");
            return;
        }

        string spriteName = emojiImage.sprite.name;
        inputField.text += $"<sprite name=\"{spriteName}\">";

        // Close search after adding emoji
        //searchInputField.text = "";
        //OnSearchValueChanged(""); // Manually trigger to restore the original state
        //searchInputField.DeactivateInputField(); // Remove focus from the search field
    }


    private IEnumerator SmoothSetActive(GameObject obj, bool active)
    {
        CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = obj.AddComponent<CanvasGroup>();
        }

        float targetAlpha = active ? 1f : 0f;
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < hoverDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / hoverDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        obj.SetActive(active);
    }
}