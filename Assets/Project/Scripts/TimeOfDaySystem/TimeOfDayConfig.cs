using UnityEngine;

[CreateAssetMenu(menuName = "Time/Time Of Day Config")]
public class TimeOfDayConfig : ScriptableObject
{
    [Range(0, 24)] public float startHour = 12f;
    public float dayDurationInMinutes = 10f;

    public AnimationCurve lightIntensityCurve;
    public Gradient lightColorGradient;
}