using System;
using UnityEngine;

public class AmbientMusicController : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] AudioClip m_DayMusic;
    [SerializeField] AudioClip m_NightMusic;

    [Header("Umbrales horarios")]
    [SerializeField, Range(0f, 24f)] float m_DayStartHour = 6f;
    [SerializeField, Range(0f, 24f)] float m_NightStartHour = 20f;

    [Header("Transicion")]
    [SerializeField, Min(0f)] float m_CrossfadeDuration = 2f;
    [SerializeField, Range(0f, 1f)] float m_DayVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] float m_NightVolume = 0.4f;

    public event Action OnDayStarted;
    public event Action OnNightStarted;

    AudioSource m_SourceA;
    AudioSource m_SourceB;
    bool m_IsDay;
    bool m_Initialized;
    TimeOfDayService m_TimeService;

    float m_FadeTimer;
    bool m_IsFading;

    public void Initialize(TimeOfDayService timeService)
    {
        m_TimeService = timeService;
        m_TimeService.OnTimeChanged += HandleTimeChanged;

        EnsureAudioSources();
        ApplyImmediate(IsDay(m_TimeService.CurrentTime));
    }

    void EnsureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();

        m_SourceA = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        m_SourceB = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

        foreach (AudioSource src in new[] { m_SourceA, m_SourceB })
        {
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
        }
    }

    float TargetVolume(bool day) => day ? m_DayVolume : m_NightVolume;

    void ApplyImmediate(bool day)
    {
        m_IsDay = day;
        m_Initialized = true;

        AudioClip clip = day ? m_DayMusic : m_NightMusic;
        m_SourceA.clip = clip;
        m_SourceA.volume = TargetVolume(day);

        m_SourceB.Stop();
        m_SourceB.volume = 0f;

        if (clip != null)
            m_SourceA.Play();
    }

    void HandleTimeChanged(float hour)
    {
        bool day = IsDay(hour);

        if (!m_Initialized)
        {
            ApplyImmediate(day);
            return;
        }

        if (day == m_IsDay)
            return;

        m_IsDay = day;

        if (day)
            OnDayStarted?.Invoke();
        else
            OnNightStarted?.Invoke();

        StartCrossfade(day ? m_DayMusic : m_NightMusic);
    }

    void StartCrossfade(AudioClip nextClip)
    {
        // Swap: B se convierte en el canal saliente, A en el entrante
        (m_SourceA, m_SourceB) = (m_SourceB, m_SourceA);

        m_SourceA.clip = nextClip;
        m_SourceA.volume = 0f;

        if (nextClip != null)
            m_SourceA.Play();

        m_FadeTimer = 0f;
        m_IsFading = true;
    }

    void Update()
    {
        if (!m_IsFading)
            return;

        m_FadeTimer += Time.deltaTime;
        float t = m_CrossfadeDuration > 0f
            ? Mathf.Clamp01(m_FadeTimer / m_CrossfadeDuration)
            : 1f;

        m_SourceA.volume = Mathf.Lerp(0f, TargetVolume(m_IsDay), t);
        m_SourceB.volume = Mathf.Lerp(TargetVolume(!m_IsDay), 0f, t);

        if (t >= 1f)
        {
            m_SourceB.Stop();
            m_IsFading = false;
        }
    }

    bool IsDay(float hour) => hour >= m_DayStartHour && hour < m_NightStartHour;

    void OnDestroy()
    {
        if (m_TimeService != null)
            m_TimeService.OnTimeChanged -= HandleTimeChanged;
    }
}
