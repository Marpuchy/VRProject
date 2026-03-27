using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private TimeOfDayConfig config;
    [SerializeField] private DayNightController controller;
    [SerializeField] private TickManager tickManager;

    private TimeOfDayService _timeService;

    private void Awake()
    {
        // Crear servicios
        _timeService = new TimeOfDayService(config);

        // Inyectar dependencias
        controller.Initialize(_timeService);

        // Registrar en el loop
        tickManager.Register(_timeService);
    }

    private void OnDestroy()
    {
        tickManager.Unregister(_timeService);
    }
}