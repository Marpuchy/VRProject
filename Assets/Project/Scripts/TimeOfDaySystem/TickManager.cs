using System.Collections.Generic;
using UnityEngine;

public class TickManager : MonoBehaviour
{
    private readonly List<ITickable> _tickables = new();

    public void Register(ITickable tickable)
    {
        if (!_tickables.Contains(tickable))
            _tickables.Add(tickable);
    }

    public void Unregister(ITickable tickable)
    {
        _tickables.Remove(tickable);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < _tickables.Count; i++)
        {
            _tickables[i].Tick(deltaTime);
        }
    }
}