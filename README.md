# Piece of Cake

Mały panel w stylu szybkich ustawień Windows 11. Ikona przy zegarze wysuwa go spod paska zadań: pokazuje zużycie
procesora i pamięci oraz prędkość sieci, steruje światłami Philips Hue i uruchamia Twoje skrypty jednym kliknięciem.

![Panel Piece of Cake](docs/screenshot.png)

## Co potrafi

- **Statystyki systemu** w dolnym pasku: procesor, zajęta pamięć oraz prędkość pobierania i wysyłania.
- **Światła Philips Hue:** pokoje i strefy jako kafelki. Kliknięcie ikony włącza lub gasi światło, strzałka obok
  otwiera listę scen, a suwak ustawia jasność.
- **Grupy programów:** jeden kafelek z ikonami kilku programów (np. Chrome, Discord i Claude). Jedno kliknięcie
  uruchamia wszystkie naraz i chowa panel.
- **Własne skrypty:** każdy wpisany przez Ciebie skrypt jest kafelkiem w panelu.
- **Paszport komputera:** zapisuje opis sprzętu i systemu w pliku Markdown.
- **Autostart:** przełącznik uruchamiania razem z Windows.

## Wygląd

Panel wygląda i porusza się jak natywne okna Windows 11 i dostosowuje się do Twoich ustawień:

- jasny i ciemny motyw systemu,
- efekty przezroczystości (włączone: akryl systemowy, wyłączone: jednolite tło),
- kolor akcentu z ustawień Windows,
- efekty animacji (wysuwanie i chowanie spod paska zadań).

Aplikacja jest przeznaczona dla Windows 11 (akryl systemowy wymaga wersji 22H2 lub nowszej).

## Instalacja przez winget

Aplikację instalujesz za pomocą [winget](https://learn.microsoft.com/windows/package-manager/winget/) z manifestu z
tego repozytorium (folder `winget`). Instalacja nie wymaga uprawnień administratora, ale jednorazowo trzeba włączyć
instalowanie z lokalnych manifestów.

1. Pobierz repozytorium:

   ```powershell
   git clone https://github.com/CristianoNS/piece-of-cake.git
   cd piece-of-cake
   ```

2. Raz, w terminalu uruchomionym jako administrator, włącz lokalne manifesty:

   ```powershell
   winget settings --enable LocalManifestFiles
   ```

3. Zainstaluj aplikację:

   ```powershell
   winget install --manifest .\winget
   ```

4. Uruchom ją poleceniem `PieceOfCake`. W zasobniku pojawi się ikona, a kliknięcie jej otwiera i zamyka panel
   (`Esc` także go zamyka).

Aplikacja jest jednym samodzielnym plikiem z wbudowanym .NET (nie trzeba instalować .NET). Odinstalujesz ją
poleceniem:

```powershell
winget uninstall CristianoNS.PieceOfCake
```

## Pierwsze połączenie z Hue

1. Otwórz panel i wpisz adres IP mostka (albo kliknij „Wykryj").
2. Kliknij „Połącz" i w ciągu 30 sekund naciśnij okrągły przycisk na mostku.
3. Pokoje i strefy pojawią się w panelu razem ze scenami z aplikacji Hue.

Adres IP mostka znajdziesz w aplikacji Hue: Ustawienia → Mostki Hue → (i).

- „Wykryj" pyta usługę `discovery.meethue.com` (chmura Philips). Jeśli wolisz tego uniknąć, wpisz IP ręcznie.
- Klucz dostępu jest zapisany zaszyfrowany (DPAPI, tylko dla Twojego konta Windows).
- Certyfikat mostka jest zapamiętany przy pierwszym połączeniu. Jeśli się zmieni, panel poprosi o ponowne parowanie.

## Grupy programów

Plik ustawień: `%APPDATA%\PieceOfCake\settings.json` (przycisk z zębatką w panelu otwiera ten folder).
Dodaj wpisy do listy `AppGroups`. Każda grupa to jeden kafelek, który jednym kliknięciem uruchamia wszystkie jej
programy, np.:

```json
"AppGroups": [
  {
    "Name": "Aplikacje",
    "Apps": [
      { "Name": "Chrome", "Target": "shell:AppsFolder\\Chrome" },
      { "Name": "Discord", "Target": "shell:AppsFolder\\com.squirrel.Discord.Discord" },
      { "Name": "Claude", "Target": "shell:AppsFolder\\Claude_pzs8sxrjxfjjc!Claude" }
    ]
  }
]
```

Na kafelku widać ikony do czterech pierwszych programów grupy (jak w folderze), a podpowiedź po najechaniu
wymienia je wszystkie. Możesz mieć kilka grup, np. „Praca" i „Gry".

`Target` to identyfikator programu z menu Start w postaci `shell:AppsFolder\<AppId>` (działa także dla aplikacji
ze Sklepu), ścieżka do pliku lub folderu albo adres internetowy. Identyfikatory zainstalowanych programów wypiszesz
w PowerShellu poleceniem `Get-StartApps`. Ikony pobiera system, więc wyglądają tak samo jak w menu Start. Jeśli
któryś program się nie uruchomi, reszta i tak wystartuje, a panel pokaże, którego nie udało się otworzyć. Zmiany
w pliku są widoczne po ponownym uruchomieniu aplikacji.
## Własne skrypty

Plik ustawień: `%APPDATA%\PieceOfCake\settings.json` (przycisk z zębatką w panelu otwiera ten folder).
Dodaj wpisy do listy `Scripts`, np.:

```json
"Scripts": [
  { "Name": "Backup", "Path": "C:\\Scripts\\backup.bat", "Arguments": "" },
  { "Name": "Sortuj Pobrane", "Path": "C:\\Scripts\\sort.ps1", "Arguments": "" }
]
```

Każdy wpis staje się kafelkiem w panelu. Uruchamiane są tylko pliki, które sam wpiszesz.

## Paszport komputera

Zapisuje plik Markdown w `Dokumenty\PieceOfCake` (system, procesor, RAM, dyski, sieć, zainstalowane programy)
i pokazuje go w Eksploratorze.

## Pliki i logi

- Ustawienia i log błędów (`error.log`): `%APPDATA%\PieceOfCake`
- Autostart: wpis w `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (można go też wyłączyć w
  Ustawienia → Aplikacje → Autostart)

## Licencja

MIT, zobacz plik `LICENSE`. Licencje składników zewnętrznych są w `THIRD_PARTY_NOTICES.md`.
