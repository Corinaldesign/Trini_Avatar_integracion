using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] List<GameObject> characters;
    [SerializeField] TMP_InputField inputField;

    public void SwapCharacters()
    {
        if (characters[0].activeInHierarchy)
        {
            characters[0].SetActive(false);
            characters[1].SetActive(true);
            StreamingCatcher.Instance.activeAvatar = 1;
        }
        else
        {
            characters[0].SetActive(true);
            characters[1].SetActive(false);
            StreamingCatcher.Instance.activeAvatar = 0;
        }
    }

    public void OnURLChanged()
    {
        StreamingCatcher.Instance.URL = inputField.text;
        StreamingCatcher.Instance.ReloadURL();
    }
}
