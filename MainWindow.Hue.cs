using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PieceOfCake.Services;
using PieceOfCake.ViewModels;

namespace PieceOfCake;

[SuppressMessage("Design", "CA1001", Justification = "Resources are released when the window closes.")]
public partial class MainWindow
{
    private readonly ObservableCollection<HueRoomVm> _rooms = new();

    private HueClient? _hue;
    private CancellationTokenSource? _pairCts;
    private bool _hueBusy;

    private async Task RefreshHueAsync()
    {
        if (_hueBusy)
        {
            return;
        }

        _hueBusy = true;
        try
        {
            EnsureHueClient();
            if (_hue == null)
            {
                ShowHuePairing(null);
                return;
            }

            var rooms = await _hue.GetRoomsAsync();
            ApplyRooms(rooms);
            HuePairPanel.Visibility = Visibility.Collapsed;
            HueRoomsControl.Visibility = Visibility.Visible;
            SetHueStatus(rooms.Count == 0 ? "W mostku nie ma żadnych pokoi ani stref." : null);
        }
        catch (HueException ex) when (ex.Unauthorized)
        {
            _hue?.Dispose();
            _hue = null;
            _settings.SetAppKey(null);
            TrySaveSettings();
            ShowHuePairing(ex.Message);
        }
        catch (HueException ex)
        {
            SetHueStatus(ex.Message);
        }
        finally
        {
            _hueBusy = false;
        }
    }

    private void EnsureHueClient()
    {
        if (_hue != null)
        {
            return;
        }

        var key = _settings.GetAppKey();
        var ip = _settings.Current.BridgeIp;
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(ip) || !HueClient.IsValidBridgeAddress(ip))
        {
            return;
        }

        _hue = new HueClient(ip, _settings.Current.BridgeCertThumbprint);
        _hue.SetAppKey(key);
    }

    private void ShowHuePairing(string? message)
    {
        _rooms.Clear();
        HueRoomsControl.Visibility = Visibility.Collapsed;
        HuePairPanel.Visibility = Visibility.Visible;
        if (string.IsNullOrWhiteSpace(BridgeIpBox.Text))
        {
            BridgeIpBox.Text = _settings.Current.BridgeIp;
        }

        SetHueStatus(message);
    }

    private void SetHueStatus(string? text)
    {
        HueStatusText.Text = text ?? "";
        HueStatusText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyRooms(IReadOnlyList<HueRoomInfo> rooms)
    {
        var sameLayout = _rooms.Count == rooms.Count
                         && _rooms.Select(r => r.Id).SequenceEqual(rooms.Select(r => r.Id), StringComparer.Ordinal);

        if (sameLayout)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                _rooms[i].SetState(rooms[i].IsOn, rooms[i].Brightness);
            }

            return;
        }

        _rooms.Clear();
        foreach (var info in rooms)
        {
            var room = new HueRoomVm(
                info.Id,
                info.Name,
                info.GroupedLightId,
                info.IsOn,
                info.Brightness,
                info.Scenes.Select(s => new HueSceneVm(s.Id, s.Name)).ToList());
            room.PowerChanged += OnRoomPowerChanged;
            room.BrightnessCommitted += OnRoomBrightnessCommitted;
            _rooms.Add(room);
        }
    }

    private async void OnRoomPowerChanged(HueRoomVm room)
    {
        var hue = _hue;
        if (hue == null)
        {
            return;
        }

        try
        {
            await hue.SetPowerAsync(room.GroupedLightId, room.IsOn);
        }
        catch (HueException ex)
        {
            SetHueStatus(ex.Message);
            room.SetState(!room.IsOn, room.Brightness);
        }
    }

    private async void OnRoomBrightnessCommitted(HueRoomVm room)
    {
        var hue = _hue;
        if (hue == null)
        {
            return;
        }

        try
        {
            await hue.SetBrightnessAsync(room.GroupedLightId, room.Brightness);
            room.SetState(true, room.Brightness);
        }
        catch (HueException ex)
        {
            SetHueStatus(ex.Message);
        }
    }

    private async void SceneChip_Click(object sender, RoutedEventArgs e)
    {
        var hue = _hue;
        if (hue == null || (sender as FrameworkElement)?.DataContext is not HueSceneVm scene)
        {
            return;
        }

        try
        {
            await hue.RecallSceneAsync(scene.Id);
            await Task.Delay(600);
            await RefreshHueAsync();
        }
        catch (HueException ex)
        {
            SetHueStatus(ex.Message);
        }
    }

    private async void HueRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshHueAsync();
    }

    private async void DetectBridgeButton_Click(object sender, RoutedEventArgs e)
    {
        DetectBridgeButton.IsEnabled = false;
        SetHueStatus("Szukam mostka…");

        try
        {
            var ip = await HueClient.DiscoverBridgeAsync(CancellationToken.None);
            if (ip == null)
            {
                SetHueStatus("Nie znaleziono mostka. Wpisz jego adres IP ręcznie (aplikacja Hue → Ustawienia → Mostki Hue).");
            }
            else
            {
                BridgeIpBox.Text = ip;
                SetHueStatus(null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Logger.Log(ex);
            SetHueStatus("Wykrywanie nie powiodło się. Wpisz adres IP mostka ręcznie.");
        }
        finally
        {
            DetectBridgeButton.IsEnabled = true;
        }
    }

    private async void PairButton_Click(object sender, RoutedEventArgs e)
    {
        var ip = BridgeIpBox.Text.Trim();
        if (!HueClient.IsValidBridgeAddress(ip))
        {
            SetHueStatus("Podaj adres IPv4 z sieci lokalnej, np. 192.168.1.10.");
            return;
        }

        _pairCts?.Cancel();
        _pairCts = new CancellationTokenSource();
        var token = _pairCts.Token;

        PairButton.IsEnabled = false;
        SetHueStatus("Naciśnij teraz przycisk na mostku Hue… (czekam do 30 s)");

        var client = new HueClient(ip, null);
        try
        {
            var key = await client.PairAsync(token);

            _settings.Current.BridgeIp = ip;
            _settings.Current.BridgeCertThumbprint = client.SeenThumbprint ?? "";
            _settings.SetAppKey(key);
            _settings.Save();

            _hue?.Dispose();
            _hue = null;
            SetHueStatus(null);
            await RefreshHueAsync();
        }
        catch (HueException ex)
        {
            SetHueStatus(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // A newer pairing attempt replaced this one.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Log(ex);
            SetHueStatus("Sparowano, ale nie udało się zapisać ustawień: " + ex.Message);
        }
        finally
        {
            client.Dispose();
            PairButton.IsEnabled = true;
        }
    }

    private void TrySaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Log(ex);
        }
    }
}
