# Usunięte funkcje

Lista rzeczy, które zostały usunięte z aplikacji, razem z opisem i kodem, żeby można było do nich wrócić.
Kod żyje też w historii gita: ostatni commit, w którym wszystko jeszcze było, to `ef90c69`.

Jak odzyskać cały plik z tamtej wersji:

```bash
git show ef90c69:src/PieceOfCake/Services/VolumeService.cs
git show ef90c69:src/PieceOfCake/Services/PowerPlanService.cs
```

Przywracanie zrobi Claude: wystarczy napisać „przywróć suwak głośności” lub „przywróć plan zasilania”.
Poniżej jest wszystko, co do tego potrzeba.

---

## 1. Suwak głośności (i wyciszanie)

**Co robił:** na samej górze panelu był suwak głównej głośności domyślnego urządzenia odtwarzającego, przycisk
wyciszenia (ikona głośnika zmieniała się po wyciszeniu) i napis z procentem. Wartość odświeżała się przy otwarciu
panelu i co sekundę, gdy panel był otwarty (z pominięciem chwili, gdy użytkownik przeciąga suwak).

**Zależność, którą ta funkcja wymagała:** paczka NuGet `NAudio.Wasapi` 2.2.1 (licencja MIT). Po jej usunięciu
aplikacja nie ma żadnych paczek zewnętrznych. Przy przywracaniu trzeba dodać do `PieceOfCake.csproj`:

```xml
<PackageReference Include="NAudio.Wasapi" Version="2.2.1" />
```

oraz wpis w `THIRD_PARTY_NOTICES.md` w sekcji „NuGet packages”:
`- [NAudio](https://github.com/naudio/NAudio): MIT License`.

**Wpływ na zasoby:** dodatkowa biblioteka (NAudio.Core + NAudio.Wasapi, ok. 360 KB) i obiekt `MMDeviceEnumerator`
przez cały czas życia okna.

### Plik `Services/VolumeService.cs`

```csharp
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
```

### Fragment `MainWindow.xaml`

Sekcja interfejsu (na samej górze `StackPanel`). Ikona przycisku `&#xE767;` to znak z czcionki Segoe Fluent Icons:

```xml
                <!-- Głośność -->
                <Grid Margin="0,0,0,16">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="44"/>
                    </Grid.ColumnDefinitions>
                    <Button x:Name="MuteButton" Style="{StaticResource IconButton}"
                            Content="&#xE767;" ToolTip="Wycisz / włącz dźwięk" Click="MuteButton_Click"/>
                    <Slider x:Name="VolumeSlider" Grid.Column="1" Style="{StaticResource FlatSlider}"
                            Minimum="0" Maximum="100" Margin="8,0" VerticalAlignment="Center"
                            ValueChanged="VolumeSlider_ValueChanged"/>
                    <TextBlock x:Name="VolumeText" Grid.Column="2" Text="0%" HorizontalAlignment="Right"
                               VerticalAlignment="Center" Foreground="{StaticResource SubTextBrush}"/>
                </Grid>
```

### Fragment `MainWindow.xaml.cs`

Pola i stałe klasy do przywrócenia:

- `private const string VolumeGlyph` = znak U+E767 (głośnik), `private const string MutedGlyph` = znak U+E74F (głośnik wyciszony); oba z czcionki Segoe Fluent Icons,
- `private readonly VolumeService _volume = new();` oraz `private bool _updatingVolume;`,
- w obsłudze `Closed`: `_volume.Dispose();`,
- w `RefreshAllAsync`: `RefreshVolume();`.

Metody:

```csharp
    // ---------- Volume ----------

    private void RefreshVolume()
    {
        if (!_volume.TryGet(out var volume, out var muted))
        {
            VolumeSlider.IsEnabled = false;
            MuteButton.IsEnabled = false;
            return;
        }

        VolumeSlider.IsEnabled = true;
        MuteButton.IsEnabled = true;

        _updatingVolume = true;
        try
        {
            VolumeSlider.Value = volume * 100.0;
        }
        finally
        {
            _updatingVolume = false;
        }

        VolumeText.Text = $"{Math.Round(volume * 100.0):0}%";
        MuteButton.Content = muted ? MutedGlyph : VolumeGlyph;
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || _updatingVolume)
        {
            return;
        }

        _volume.SetVolume(e.NewValue / 100.0);
        VolumeText.Text = $"{Math.Round(e.NewValue):0}%";
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_volume.TryGet(out _, out var muted))
        {
            _volume.SetMuted(!muted);
        }

        RefreshVolume();
    }
```

Wariant `OnMetricsTick`, który odświeżał też głośność (dziś zegar woła od razu `UpdateMetrics`):

```csharp
    private void OnMetricsTick()
    {
        UpdateMetrics();

        // Pick up volume changes made elsewhere, but never fight the user's drag.
        if (!VolumeSlider.IsMouseCaptureWithin)
        {
            RefreshVolume();
        }
    }
```

---

## 2. Plan zasilania

**Co robił:** pod suwakiem były trzy przyciski do przełączania planu zasilania Windows (Oszczędny, Zrównoważony,
Wydajny). Aktywny plan był podświetlony. Działało przez wywołania `powercfg.exe` (`/getactivescheme` i
`/setactive`), uruchamiane w tle, z limitem czasu 4 s.

**Uwagi:** plany „Oszczędny” i „Wydajny” bywają niedostępne na komputerach z nowoczesnym trybem czuwania; wtedy
przełączenie kończyło się komunikatem w stopce. Identyfikatory planów to standardowe GUID-y Windows (są w kodzie
poniżej). Funkcja nie ma żadnych zależności zewnętrznych.

### Plik `Services/PowerPlanService.cs`

```csharp
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PieceOfCake.Services;

public static class PowerPlanService
{
    public static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static Guid? GetActive()
    {
        var output = Run("/getactivescheme", out var exitCode);
        if (exitCode != 0 || output == null)
        {
            return null;
        }

        var match = GuidPattern.Match(output);
        return match.Success && Guid.TryParse(match.Value, out var id) ? id : null;
    }

    public static bool SetActive(Guid id)
    {
        Run("/setactive " + id.ToString("D"), out var exitCode);
        return exitCode == 0;
    }

    private static string? Run(string arguments, out int exitCode)
    {
        exitCode = -1;

        try
        {
            var startInfo = new ProcessStartInfo("powercfg.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            if (!process.WaitForExit(4000))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Process already finished.
                }

                return null;
            }

            exitCode = process.ExitCode;
            return process.StandardOutput.ReadToEnd();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Logger.Log(ex);
            return null;
        }
    }
}
```

### Fragment `MainWindow.xaml`

```xml
                <!-- Plan zasilania -->
                <TextBlock Text="Plan zasilania" Style="{StaticResource SectionHeader}"/>
                <UniformGrid Columns="3" Margin="0,0,0,16">
                    <ToggleButton x:Name="PowerSaverButton" Style="{StaticResource ChipToggle}"
                                  Content="Oszczędny" Tag="a1841308-3541-4fab-bc81-f71556f20b4a"
                                  Click="PowerPlan_Click"/>
                    <ToggleButton x:Name="PowerBalancedButton" Style="{StaticResource ChipToggle}"
                                  Content="Zrównoważony" Tag="381b4222-f694-41f0-9685-ff5bb260df2e"
                                  Click="PowerPlan_Click"/>
                    <ToggleButton x:Name="PowerHighButton" Style="{StaticResource ChipToggle}"
                                  Content="Wydajny" Tag="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
                                  Margin="0" Click="PowerPlan_Click"/>
                </UniformGrid>
```

### Fragment `MainWindow.xaml.cs`

W `RefreshAllAsync` było wywołanie `await RefreshPowerPlanAsync();`. Metody:

```csharp
    // ---------- Power plan ----------

    private async Task RefreshPowerPlanAsync()
    {
        var active = await Task.Run(PowerPlanService.GetActive);
        PowerSaverButton.IsChecked = active == PowerPlanService.PowerSaver;
        PowerBalancedButton.IsChecked = active == PowerPlanService.Balanced;
        PowerHighButton.IsChecked = active == PowerPlanService.HighPerformance;
    }

    private async void PowerPlan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string tag } || !Guid.TryParse(tag, out var plan))
        {
            return;
        }

        var ok = await Task.Run(() => PowerPlanService.SetActive(plan));
        if (!ok)
        {
            ShowFooterStatus("Nie udało się zmienić planu zasilania (ten plan może być niedostępny na tym komputerze).");
        }

        await RefreshPowerPlanAsync();
    }
```

### Styl `ChipToggle` z `App.xaml`

Przyciski planu zasilania używały stylu przycisku przełączanego (podświetlany na niebiesko, gdy aktywny).
Po usunięciu planu zasilania nic go już nie używa, więc też został skasowany z `App.xaml`:

```xml
        <Style x:Key="ChipToggle" TargetType="ToggleButton">
            <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
            <Setter Property="Background" Value="{StaticResource TileBrush}"/>
            <Setter Property="Padding" Value="8,8"/>
            <Setter Property="Margin" Value="0,0,6,0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                        <Border x:Name="Bd" Background="{TemplateBinding Background}" CornerRadius="6"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bd" Property="Background" Value="{StaticResource TileHoverBrush}"/>
                            </Trigger>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="Bd" Property="Background" Value="{StaticResource AccentBrush}"/>
                                <Setter Property="Foreground" Value="{StaticResource OnAccentBrush}"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter Property="Opacity" Value="0.5"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```


---

## 3. Paski przy procesorze i pamięci

Ostatni commit z paskami: `6fba1b8`.

**Co robiły:** pod wierszami „Procesor” i „Pamięć” w sekcji System był cienki pasek postępu (niebieski, na szarym tle)
pokazujący obciążenie procesora i zajęcie pamięci w procentach. Obok nadal są wartości liczbowe, więc zostały
tylko one, w tym samym stylu co wiersz „Sieć”. Paski były zwykłą ozdobą bez wpływu na zasoby.

### Fragment `MainWindow.xaml`

Cały blok sekcji System z paskami (`CpuBar`, `RamBar`):

```xml
                <!-- Procesor i pamięć -->
                <TextBlock Text="System" Style="{StaticResource SectionHeader}"/>
                <Grid Margin="0,0,0,4">
                    <TextBlock Text="Procesor"/>
                    <TextBlock x:Name="CpuText" Text="0%" HorizontalAlignment="Right"
                               Foreground="{StaticResource SubTextBrush}"/>
                </Grid>
                <ProgressBar x:Name="CpuBar" Style="{StaticResource MeterBar}" Margin="0,0,0,10"/>
                <Grid Margin="0,0,0,4">
                    <TextBlock Text="Pamięć"/>
                    <TextBlock x:Name="RamText" Text="-" HorizontalAlignment="Right"
                               Foreground="{StaticResource SubTextBrush}"/>
                </Grid>
                <ProgressBar x:Name="RamBar" Style="{StaticResource MeterBar}" Margin="0,0,0,10"/>
```

### Styl `MeterBar` z `App.xaml`

```xml
        <Style x:Key="MeterBar" TargetType="ProgressBar">
            <Setter Property="Height" Value="6"/>
            <Setter Property="Minimum" Value="0"/>
            <Setter Property="Maximum" Value="100"/>
            <Setter Property="Foreground" Value="{StaticResource AccentBrush}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ProgressBar">
                        <Grid>
                            <Border x:Name="PART_Track" Background="{StaticResource TrackBrush}" CornerRadius="3"/>
                            <Border x:Name="PART_Indicator" Background="{TemplateBinding Foreground}"
                                    CornerRadius="3" HorizontalAlignment="Left"/>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

### Fragment `MainWindow.xaml.cs`

W `UpdateMetrics` były dwie linie ustawiające paski: `CpuBar.Value = cpu;` oraz
`RamBar.Value = used / total * 100.0;`. Cała metoda w wersji z paskami:

```csharp
    private void UpdateMetrics()
    {
        var cpu = _cpu.Sample();
        CpuBar.Value = cpu;
        CpuText.Text = $"{cpu:0}%";

        if (MemoryMeter.TryGet(out var total, out var used) && total > 0)
        {
            const double gigabyte = 1024.0 * 1024.0 * 1024.0;
            RamBar.Value = used / total * 100.0;
            RamText.Text = $"{used / gigabyte:0.0} / {total / gigabyte:0.0} GB";
        }
    }
```

---

## Pomysły na przyszłość

- Głośność bez NAudio: wywołania COM do `IAudioEndpointVolume` bezpośrednio (bez biblioteki), albo skróty
  klawiszowe zamiast suwaka.
- Plan zasilania przez natywne `PowerSetActiveScheme` zamiast uruchamiania `powercfg.exe`.
