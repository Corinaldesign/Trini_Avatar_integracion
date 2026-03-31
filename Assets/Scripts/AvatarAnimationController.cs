using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controlador de animaciones del avatar para Trini-IA.
/// Se integra con StreamingCatcher para sincronizar el estado del avatar
/// con las animaciones correspondientes.
///
/// Estados soportados:
/// - idle: Avatar en espera, animacion relajada
/// - listening: Avatar escuchando al usuario (microfono activo)
/// - thinking: Avatar procesando/pensando la respuesta
/// - speaking: Avatar hablando (sincronizado con lip-sync)
///
/// Configuracion:
/// 1. Agregar este componente al GameObject del avatar (unitychan)
/// 2. Asignar el Animator del avatar
/// 3. El script se conecta automaticamente con StreamingCatcher
/// </summary>
[RequireComponent(typeof(Animator))]
public class AvatarAnimationController : MonoBehaviour
{
    #region Configuracion del Inspector

    [Header("Referencias")]
    [Tooltip("Animator del avatar. Se obtiene automaticamente si no se asigna.")]
    [SerializeField] private Animator avatarAnimator;

    [Header("Configuracion de Transiciones")]
    [Tooltip("Duracion de la transicion entre animaciones (segundos)")]
    [SerializeField] private float transitionDuration = 0.25f;

    [Tooltip("Usar transiciones suaves entre estados")]
    [SerializeField] private bool useSmoothTransitions = true;

    [Header("Variaciones de Idle")]
    [Tooltip("Tiempo minimo antes de cambiar a idle alternativo (segundos)")]
    [SerializeField] private float idleVariationMinTime = 5f;

    [Tooltip("Tiempo maximo antes de cambiar a idle alternativo (segundos)")]
    [SerializeField] private float idleVariationMaxTime = 15f;

    [Tooltip("Activar variaciones aleatorias de idle")]
    [SerializeField] private bool enableIdleVariations = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    #endregion

    #region Parametros del Animator (nombres)

    // Estos nombres deben coincidir con los parametros en el Animator Controller
    private static class AnimatorParams
    {
        public const string State = "State";           // Int: 0=idle, 1=listening, 2=thinking, 3=speaking
        public const string IdleVariant = "IdleVariant"; // Int: 0-4 para variantes de idle
        public const string IsSpeaking = "IsSpeaking";   // Bool: true cuando esta hablando
        public const string TriggerGesture = "TriggerGesture"; // Trigger para gestos ocasionales
    }

    // Mapeo de estados a valores del Animator
    private static readonly Dictionary<string, int> StateToAnimatorValue = new Dictionary<string, int>
    {
        { "idle", 0 },
        { "listening", 1 },
        { "thinking", 2 },
        { "speaking", 3 }
    };

    #endregion

    #region Variables Privadas

    private string currentState = "idle";
    private int currentIdleVariant = 0;
    private float idleVariationTimer = 0f;
    private float nextIdleVariationTime = 0f;
    private bool isInitialized = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Obtener Animator si no esta asignado
        if (avatarAnimator == null)
        {
            avatarAnimator = GetComponent<Animator>();
        }

        if (avatarAnimator == null)
        {
            Debug.LogError("[AvatarAnimationController] No se encontro Animator en el GameObject.");
            enabled = false;
            return;
        }

        isInitialized = true;
        ResetIdleVariationTimer();
    }

    private void Start()
    {
        // Suscribirse a eventos de StreamingCatcher si existe
        if (StreamingCatcher.Instance != null)
        {
            // El StreamingCatcher notificara cambios de estado
            LogDebug("Conectado con StreamingCatcher");
        }

        // Estado inicial
        SetState("idle");
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Manejar variaciones de idle
        if (enableIdleVariations && currentState == "idle")
        {
            UpdateIdleVariation();
        }
    }

    #endregion

    #region API Publica

    /// <summary>
    /// Establece el estado de animacion del avatar.
    /// Llamado por StreamingCatcher cuando cambia el estado.
    /// </summary>
    /// <param name="state">Estado: idle, listening, thinking, speaking</param>
    public void SetState(string state)
    {
        if (!isInitialized) return;

        string normalizedState = state.ToLower().Trim();

        if (!StateToAnimatorValue.ContainsKey(normalizedState))
        {
            Debug.LogWarning($"[AvatarAnimationController] Estado desconocido: {state}");
            return;
        }

        if (currentState == normalizedState) return;

        string previousState = currentState;
        currentState = normalizedState;

        // Actualizar parametros del Animator
        int stateValue = StateToAnimatorValue[normalizedState];

        // SIEMPRE actualizar el parametro State para que las transiciones
        // desde Any State y las salidas del Sub-State Machine funcionen
        avatarAnimator.SetInteger(AnimatorParams.State, stateValue);

        if (useSmoothTransitions)
        {
            // CrossFade adicional para transiciones mas suaves
            avatarAnimator.CrossFade(GetStateAnimationName(normalizedState), transitionDuration);
        }

        // Parametro booleano para speaking (util para blend trees)
        avatarAnimator.SetBool(AnimatorParams.IsSpeaking, normalizedState == "speaking");

        // Resetear timer de idle si cambiamos a idle
        if (normalizedState == "idle")
        {
            ResetIdleVariationTimer();
        }

        LogDebug($"Estado: {previousState} -> {normalizedState}");
    }

    /// <summary>
    /// Dispara un gesto ocasional (saludar, asentir, etc.)
    /// </summary>
    public void TriggerGesture()
    {
        if (!isInitialized) return;

        avatarAnimator.SetTrigger(AnimatorParams.TriggerGesture);
        LogDebug("Gesto disparado");
    }

    /// <summary>
    /// Cambia manualmente la variante de idle.
    /// </summary>
    /// <param name="variant">Variante 0-4</param>
    public void SetIdleVariant(int variant)
    {
        if (!isInitialized) return;

        currentIdleVariant = Mathf.Clamp(variant, 0, 4);
        avatarAnimator.SetInteger(AnimatorParams.IdleVariant, currentIdleVariant);
        LogDebug($"Idle variante: {currentIdleVariant}");
    }

    /// <summary>
    /// Obtiene el estado actual de animacion.
    /// </summary>
    public string GetCurrentState()
    {
        return currentState;
    }

    #endregion

    #region Metodos Privados

    private void UpdateIdleVariation()
    {
        idleVariationTimer += Time.deltaTime;

        if (idleVariationTimer >= nextIdleVariationTime)
        {
            // Cambiar a una variante aleatoria diferente
            int newVariant;
            do
            {
                newVariant = Random.Range(0, 5); // 0-4
            } while (newVariant == currentIdleVariant && enableIdleVariations);

            SetIdleVariant(newVariant);
            ResetIdleVariationTimer();
        }
    }

    private void ResetIdleVariationTimer()
    {
        idleVariationTimer = 0f;
        nextIdleVariationTime = Random.Range(idleVariationMinTime, idleVariationMaxTime);
    }

    private string GetStateAnimationName(string state)
    {
        // Nombres de los estados en el Animator Controller
        // Deben coincidir con los nombres de los estados creados
        switch (state)
        {
            case "idle": return "Idle";
            case "listening": return "Listening";
            case "thinking": return "Thinking";
            case "speaking": return "Speaking";
            default: return "Idle";
        }
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[AvatarAnimationController] {message}");
        }
    }

    #endregion

    #region Integracion con StreamingCatcher

    /// <summary>
    /// Llamado por StreamingCatcher para sincronizar estados.
    /// Este metodo permite la comunicacion entre los dos scripts.
    /// </summary>
    public void OnStreamingCatcherStateChanged(string newState)
    {
        SetState(newState);
    }

    #endregion
}
