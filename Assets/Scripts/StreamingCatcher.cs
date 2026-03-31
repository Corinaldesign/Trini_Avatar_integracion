using System.Collections;
using UnityEngine.Networking;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// Controlador principal del avatar para integracion con React.
/// Expone funciones callable desde JavaScript via SendMessage.
///
/// Uso desde React:
///   sendMessage("StreamingCatcher", "PlayAudio", audioUrl);
///   sendMessage("StreamingCatcher", "SetState", "thinking");
///   sendMessage("StreamingCatcher", "SetAgentMode", "dark");
///   sendMessage("StreamingCatcher", "StopAudio");
/// </summary>
public class StreamingCatcher : MonoBehaviour
{
    public static StreamingCatcher Instance { get; private set; }

    [Header("Avatar Audio")]
    public int activeAvatar = 0;
    public List<AudioSource> audioSources;
    public List<GameObject> avatarObjects; // Referencias a los GameObjects de los avatares

    [Header("Estado")]
    [SerializeField] private string currentState = "idle";
    [SerializeField] private string currentMode = "light";

    [Header("Animaciones")]
    [Tooltip("Controlador de animaciones del avatar activo. Se busca automaticamente si no se asigna.")]
    [SerializeField] private AvatarAnimationController animationController;

    [Header("Streaming Settings (Legacy)")]
    [SerializeField] public string URL;

    // Importar funcion del puente JavaScript
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendUnityEvent(string eventName, string eventData);
    #endif

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton
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
        // Desactivar captura de teclado para que React reciba los eventos de input
        #if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = false;
        #endif

        // Buscar controlador de animaciones si no esta asignado
        if (animationController == null && avatarObjects != null && avatarObjects.Count > 0)
        {
            // Buscar en el avatar activo
            if (avatarObjects[activeAvatar] != null)
            {
                animationController = avatarObjects[activeAvatar].GetComponent<AvatarAnimationController>();
                if (animationController == null)
                {
                    animationController = avatarObjects[activeAvatar].GetComponentInChildren<AvatarAnimationController>();
                }
            }
        }

        // Notificar a React que Unity esta listo
        StartCoroutine(NotifyReady());
    }

    IEnumerator NotifyReady()
    {
        // Esperar un frame para asegurar que todo esta inicializado
        yield return new WaitForSeconds(0.5f);

        SendEvent("UnityReady", "{}");
        SetState("idle");

        Debug.Log("[StreamingCatcher] Unity listo y notificado a React");
    }

    #endregion

    #region Funciones Expuestas a JavaScript

    /// <summary>
    /// Reproduce audio desde una URL con lip-sync.
    /// Llamado desde React: sendMessage("StreamingCatcher", "PlayAudio", url)
    /// </summary>
    /// <param name="url">URL del archivo de audio (MP3)</param>
    public void PlayAudio(string url)
    {
        Debug.Log("[StreamingCatcher] PlayAudio llamado con URL: " + url);

        if (string.IsNullOrEmpty(url))
        {
            SendEvent("AudioError", "{\"error\":\"URL de audio vacia\"}");
            return;
        }

        // Detener audio anterior si existe
        if (audioSources[activeAvatar].isPlaying)
        {
            audioSources[activeAvatar].Stop();
        }
        audioSources[activeAvatar].clip = null;

        // Cambiar estado a speaking
        SetState("speaking");

        // Iniciar carga y reproduccion
        StartCoroutine(LoadAndPlayAudio(url));
    }

    /// <summary>
    /// Establece el estado del avatar.
    /// Estados validos: idle, listening, thinking, speaking
    /// Llamado desde React: sendMessage("StreamingCatcher", "SetState", "thinking")
    /// </summary>
    public void SetState(string state)
    {
        string[] validStates = { "idle", "listening", "thinking", "speaking" };

        if (System.Array.IndexOf(validStates, state) == -1)
        {
            Debug.LogWarning("[StreamingCatcher] Estado invalido: " + state);
            return;
        }

        currentState = state;

        // Notificar al controlador de animaciones
        if (animationController != null)
        {
            animationController.OnStreamingCatcherStateChanged(state);
        }

        // Notificar a React
        SendEvent("StateChanged", "{\"state\":\"" + state + "\"}");

        Debug.Log("[StreamingCatcher] Estado cambiado a: " + state);
    }

    /// <summary>
    /// Cambia el modo visual del avatar (tema claro/oscuro).
    /// Llamado desde React: sendMessage("StreamingCatcher", "SetAgentMode", "dark")
    /// </summary>
    public void SetAgentMode(string mode)
    {
        if (mode != "light" && mode != "dark")
        {
            Debug.LogWarning("[StreamingCatcher] Modo invalido: " + mode);
            return;
        }

        currentMode = mode;

        // Cambiar avatar activo segun el modo
        if (mode == "light" && activeAvatar != 0)
        {
            SwapToAvatar(0);
        }
        else if (mode == "dark" && activeAvatar != 1)
        {
            SwapToAvatar(1);
        }

        // Notificar a React
        SendEvent("AgentModeChanged", "{\"mode\":\"" + mode + "\"}");

        Debug.Log("[StreamingCatcher] Modo cambiado a: " + mode);
    }

    /// <summary>
    /// Detiene la reproduccion de audio actual.
    /// Llamado desde React: sendMessage("StreamingCatcher", "StopAudio")
    /// </summary>
    public void StopAudio()
    {
        if (audioSources[activeAvatar] != null && audioSources[activeAvatar].isPlaying)
        {
            audioSources[activeAvatar].Stop();
        }

        SetState("idle");

        Debug.Log("[StreamingCatcher] Audio detenido");
    }

    /// <summary>
    /// Configura el avatar mediante JSON.
    /// Llamado desde React: sendMessage("StreamingCatcher", "Configure", jsonConfig)
    /// </summary>
    public void Configure(string jsonConfig)
    {
        try
        {
            AvatarConfig config = JsonUtility.FromJson<AvatarConfig>(jsonConfig);

            if (!string.IsNullOrEmpty(config.initialState))
            {
                SetState(config.initialState);
            }

            if (!string.IsNullOrEmpty(config.agentMode))
            {
                SetAgentMode(config.agentMode);
            }

            if (config.volume >= 0 && config.volume <= 1)
            {
                foreach (var source in audioSources)
                {
                    source.volume = config.volume;
                }
            }

            Debug.Log("[StreamingCatcher] Configuracion aplicada");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[StreamingCatcher] Error parseando config: " + e.Message);
            SendEvent("AudioError", "{\"error\":\"Error de configuracion: " + e.Message + "\"}");
        }
    }

    #endregion

    #region Metodos Privados

    private void SwapToAvatar(int index)
    {
        if (avatarObjects != null && avatarObjects.Count > index)
        {
            // Desactivar avatar actual
            if (avatarObjects[activeAvatar] != null)
            {
                avatarObjects[activeAvatar].SetActive(false);
            }

            // Activar nuevo avatar
            activeAvatar = index;
            if (avatarObjects[activeAvatar] != null)
            {
                avatarObjects[activeAvatar].SetActive(true);

                // Actualizar referencia al controlador de animaciones del nuevo avatar
                animationController = avatarObjects[activeAvatar].GetComponent<AvatarAnimationController>();
                if (animationController == null)
                {
                    animationController = avatarObjects[activeAvatar].GetComponentInChildren<AvatarAnimationController>();
                }

                // Sincronizar estado actual con el nuevo avatar
                if (animationController != null)
                {
                    animationController.OnStreamingCatcherStateChanged(currentState);
                }
            }
        }
        else
        {
            // Fallback: solo cambiar el indice
            activeAvatar = index;
        }

        Debug.Log("[StreamingCatcher] Avatar cambiado a: " + index);
    }

    IEnumerator LoadAndPlayAudio(string url)
    {
        Debug.Log("[StreamingCatcher] Descargando audio desde: " + url);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[StreamingCatcher] Error descargando audio: " + www.error);
                SendEvent("AudioError", "{\"error\":\"" + EscapeJson(www.error) + "\"}");
                SetState("idle");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

            if (clip == null)
            {
                Debug.LogError("[StreamingCatcher] AudioClip es NULL");
                SendEvent("AudioError", "{\"error\":\"No se pudo decodificar el audio\"}");
                SetState("idle");
                yield break;
            }

            // Reproducir audio
            audioSources[activeAvatar].clip = clip;
            audioSources[activeAvatar].Play();

            Debug.Log("[StreamingCatcher] Audio reproduciendo, duracion: " + clip.length + "s");

            // Esperar a que termine el audio
            yield return new WaitWhile(() => audioSources[activeAvatar].isPlaying);

            // Audio completado
            OnAudioComplete();
        }
    }

    private void OnAudioComplete()
    {
        SetState("idle");
        SendEvent("AudioComplete", "{}");

        Debug.Log("[StreamingCatcher] Audio completado");
    }

    private void SendEvent(string eventName, string eventData)
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        SendUnityEvent(eventName, eventData);
        #else
        Debug.Log("[StreamingCatcher] SendEvent (Editor): " + eventName + " -> " + eventData);
        #endif
    }

    private string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    #endregion

    #region Legacy - Compatibilidad con UI existente

    /// <summary>
    /// Metodo legacy para compatibilidad con CanvasManager.
    /// </summary>
    public void ReloadURL()
    {
        if (!string.IsNullOrEmpty(URL))
        {
            PlayAudio(URL);
        }
    }

    #endregion

    #region Clases de Configuracion

    [System.Serializable]
    public class AvatarConfig
    {
        public string initialState;
        public string agentMode;
        public float volume = 1.0f;
    }

    #endregion
}
