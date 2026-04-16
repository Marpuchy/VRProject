using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private TimeOfDayConfig config;
    [SerializeField] private FastSkyBridge controller; // Cambiado a FastSkyBridge
    [SerializeField] private TickManager tickManager;
    [SerializeField] private AmbientMusicController ambientMusic;

    private TimeOfDayService _timeService;

    private void Awake()
    {
        // Crear servicios
        _timeService = new TimeOfDayService(config);

        // Inyectar dependencias
        if (controller != null)
            controller.Initialize(_timeService);

        if (ambientMusic != null)
            ambientMusic.Initialize(_timeService);

        // Registrar en el loop de Ticks
        if (tickManager != null)
        {
            tickManager.Register(_timeService);
        }
    }

    private void OnDestroy()
    {
        if (tickManager != null)
        {
            tickManager.Unregister(_timeService);
        }
    }
}