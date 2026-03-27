using System;

public class TimeOfDayService : ITickable
{
    public event Action<float> OnTimeChanged;

    private readonly TimeOfDayConfig _config;

    private float _currentTime;
    private float _timeScale;

    public float CurrentTime => _currentTime;

    public TimeOfDayService(TimeOfDayConfig config)
    {
        _config = config;
        _currentTime = config.startHour;
        _timeScale = 24f / (config.dayDurationInMinutes * 60f);
    }

    public void Tick(float deltaTime)
    {
        _currentTime += deltaTime * _timeScale;

        if (_currentTime >= 24f)
            _currentTime = 0f;

        OnTimeChanged?.Invoke(_currentTime);
    }
}