# Piece of Cake

Własny wysuwany panel w stylu szybkich ustawień Windows 11 (ikona przy zegarze).

Zawiera: głośność, plan zasilania, CPU/RAM, światła Philips Hue (pokoje, strefy, sceny),
własne skrypty, „Paszport komputera" i przełącznik autostartu.

## Uruchomienie

- Gotowy folder `publish` (samodzielny, nie wymaga instalowania .NET). Uruchom `PieceOfCake.exe`, a w zasobniku
  pojawi się ikona. Kliknięcie ikony otwiera i zamyka panel, `Esc` także go zamyka.
- Kod aplikacji jest w `PieceOfCake.dll` (ok. 260 KB). Przy zmianach w programie wystarczy podmienić ten jeden plik
  (po zamknięciu aplikacji). Reszta folderu to biblioteki .NET i zmienia się tylko przy zmianie wersji .NET lub paczek.
- Z kodu: zainstaluj .NET 8 SDK i uruchom `build.bat` (wynik w folderze `publish`). Testy: `dotnet test PieceOfCake.sln`.

## Instalator (opcjonalnie)

Zamiast uruchamiać samego `.exe` możesz zbudować zwykły instalator (menu Start, opcjonalny skrót na pulpicie,
pole „Uruchamiaj z systemem", odinstalowanie z Ustawienia → Aplikacje).

1. Zainstaluj darmowy [Inno Setup 6](https://jrsoftware.org/isdl.php) (wersja 6.3 lub nowsza).
2. Upewnij się, że istnieje folder `publish` z `PieceOfCake.exe` (gotowy folder skopiuj obok `installer`
   albo uruchom `build.bat`, który go zbuduje).
3. Uruchom `installer\build-installer.bat`. Wynik: `installer-output\PieceOfCake-Setup.exe`.
   Można też otworzyć `installer\PieceOfCake.iss` w Inno Setup i nacisnąć Ctrl+F9.

Instalator domyślnie instaluje program tylko dla Twojego konta (bez praw administratora). W oknie
wyboru można zmienić to na instalację dla wszystkich użytkowników. Przy odinstalowaniu pyta, czy usunąć
też ustawienia i sparowanie z mostkiem Hue.

## Pierwsze połączenie z Hue

1. Otwórz panel i w sekcji „Światła Hue" wpisz adres IP mostka (albo kliknij „Wykryj").
2. Kliknij „Połącz" i w ciągu 30 sekund naciśnij okrągły przycisk na mostku.
3. Pokoje i strefy pojawią się w panelu razem ze scenami z aplikacji Hue.

Adres IP mostka znajdziesz w aplikacji Hue: Ustawienia → Mostki Hue → (i).

Uwagi:
- „Wykryj" pyta usługę `discovery.meethue.com` (chmura Philips). Jeśli wolisz tego uniknąć, wpisz IP ręcznie.
- Klucz dostępu jest zapisany zaszyfrowany (DPAPI, tylko dla Twojego konta Windows).
- Certyfikat mostka jest zapamiętany przy pierwszym połączeniu. Jeśli się zmieni, panel poprosi o ponowne parowanie.

## Własne skrypty

Plik ustawień: `%APPDATA%\PieceOfCake\settings.json` (przycisk z zębatką w panelu otwiera ten folder).
Dodaj wpisy do listy `Scripts`, np.:

```json
"Scripts": [
  { "Name": "Backup", "Path": "C:\\Scripts\\backup.bat", "Arguments": "" },
  { "Name": "Sortuj Pobrane", "Path": "C:\\Scripts\\sort.ps1", "Arguments": "" }
]
```

Każdy wpis staje się przyciskiem w panelu. Uruchamiane są tylko pliki, które sam wpiszesz.

## Paszport komputera

Zapisuje plik Markdown w `Dokumenty\PieceOfCake` (system, procesor, RAM, dyski, sieć, zainstalowane programy)
i pokazuje go w Eksploratorze.

## Ikony

Ikona aplikacji (menu Start, skrót, instalator) jest różowa, a ikona w zasobniku jest biała na ciemnym
pasku zadań i ciemna na jasnym (przełącza się sama razem z motywem Windows). Pliki są w folderze `Assets`.
Kształt pochodzi z Lucide (licencja ISC), szczegóły w `THIRD_PARTY_NOTICES.md`.

## Pliki i logi

- Ustawienia i log błędów (`error.log`): `%APPDATA%\PieceOfCake`
- Autostart: wpis w `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (można go też wyłączyć w
  Ustawienia → Aplikacje → Autostart)

## Licencja

MIT, zobacz plik `LICENSE`. Licencje składników zewnętrznych są w `THIRD_PARTY_NOTICES.md`.
