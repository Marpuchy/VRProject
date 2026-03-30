using UnityEngine;

public class FastSkyBridge : MonoBehaviour
{
    [Header("Referencias FastSky")]
    [SerializeField] private Material skyMaterial; // El material RealisticSky
    [SerializeField] private Light sunLight;
    [SerializeField] private TimeOfDayConfig config;

    [Header("Ajustes de Estrellas")]
    [Range(0, 5)] [SerializeField] private float maxStarBrightness = 2.0f;

    private TimeOfDayService _timeService;

    public void Initialize(TimeOfDayService timeService)
    {
        _timeService = timeService;
        _timeService.OnTimeChanged += UpdateSky;
        
        // Estado inicial
        UpdateSky(_timeService.CurrentTime);
    }

    private void UpdateSky(float time)
    {
        float normalizedTime = time / 24f;

        // 1. Rotación del Sol (Tu lógica original)
        float angle = normalizedTime * 360f - 90f;
        sunLight.transform.rotation = Quaternion.Euler(angle, 170f, 0);

        // 2. Intensidad y Color desde tu ScriptableObject
        float intensity = config.lightIntensityCurve.Evaluate(normalizedTime);
        sunLight.intensity = intensity;
        sunLight.color = config.lightColorGradient.Evaluate(normalizedTime);

        // 3. Control de Estrellas y Atmosfera de FastSky
        // Las estrellas aparecen cuando la intensidad de la luz baja de cierto umbral
        float starAlpha = 1f - Mathf.Clamp01(intensity * 2f); 
        
        // Actualizamos los parámetros del Shader directamente
        if (skyMaterial != null)
        {
            skyMaterial.SetVector("_SunDirection", -sunLight.transform.forward);
            skyMaterial.SetFloat("_StarBrightness", starAlpha * maxStarBrightness);
            
            // Opcional: Oscurecer nubes de noche para que no brillen en la oscuridad
            skyMaterial.SetFloat("_CloudBrightness", Mathf.Max(0.1f, intensity));
        }

        // 4. Iluminación Ambiental (Importante para VR)
        RenderSettings.ambientIntensity = Mathf.Lerp(0.5f, 1.2f, starAlpha);
    }

    private void OnDestroy()
    {
        if (_timeService != null)
            _timeService.OnTimeChanged -= UpdateSky;
    }
}