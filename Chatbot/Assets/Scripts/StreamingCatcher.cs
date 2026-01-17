using System.Collections;
using UnityEngine.Networking;
using UnityEngine;
using System.Collections.Generic;

public class StreamingCatcher : MonoBehaviour
{
    public static StreamingCatcher Instance { get; private set; }

    [Header("Avatar Audio")]
    public int activeAvatar = 0; //Avatar activo
    public List<AudioSource> audioSources; //Audiosource del avatar. Donde se asignará el audio para reproducir

    [Header("Streaming Settings")]
    [SerializeField] public string URL; //URL Fija, de momento puede ser LOCALHOST:PUERTO


    public void ReloadURL()
    {
        audioSources[activeAvatar].Stop();
        audioSources[activeAvatar].clip = null;
        StartCoroutine(GetClipFromURL());
    }

    private void Awake()
    {
        
        //Singleton (Revisar si es necesario.)
        if (Instance != null && Instance != this) 
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    void Start()
    {
        StartCoroutine(GetClipFromURL());
    }

    IEnumerator  GetClipFromURL()
    {
        Debug.Log("Descargando audio desde: " + URL);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(URL, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error al descargar audio: " + www.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

            if (clip == null)
            {
                Debug.LogError("AudioClip es NULL");
                yield break;
            }

            SetClip(clip);
        }
    }

    void SetClip(AudioClip clip)
    {
        audioSources[activeAvatar].clip = clip;
        audioSources[activeAvatar].Play();

        
        Debug.Log("Audio reproducido correctamente");
    }
}
