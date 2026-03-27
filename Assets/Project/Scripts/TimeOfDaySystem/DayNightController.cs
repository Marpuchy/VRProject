using UnityEngine;

public class DayNightController : MonoBehaviour
{
    [SerializeField] private Light directionalLight;
    [SerializeField] private TimeOfDayConfig config;

    private TimeOfDayService _timeService;

    public void Initialize(TimeOfDayService timeService)
    {
        _timeService = timeService;
        _timeService.OnTimeChanged += UpdateVisuals;
    }

    private void UpdateVisuals(float time)
    {
        float normalizedTime = time / 24f;

        float angle = normalizedTime * 360f - 90f;
        directionalLight.transform.rotation = Quaternion.Euler(angle, 170f, 0);

        directionalLight.intensity = config.lightIntensityCurve.Evaluate(normalizedTime);
        directionalLight.color = config.lightColorGradient.Evaluate(normalizedTime);
        
    }

    private void OnDestroy()
    {
        if (_timeService != null)
            _timeService.OnTimeChanged -= UpdateVisuals;
    }
}