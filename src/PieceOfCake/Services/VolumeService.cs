using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PieceOfCake.Services;

/// <summary>Reads and controls the master volume of the default playback device.</summary>
public sealed class VolumeService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public bool TryGet(out double volume, out bool muted)
    {
        volume = 0;
        muted = false;

        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            volume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
            muted = device.AudioEndpointVolume.Mute;
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    public void SetVolume(double volume)
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(volume, 0.0, 1.0);
        }
        catch (COMException ex)
        {
            Logger.Log(ex);
        }
    }

    public void SetMuted(bool muted)
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.Mute = muted;
        }
        catch (COMException ex)
        {
            Logger.Log(ex);
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
