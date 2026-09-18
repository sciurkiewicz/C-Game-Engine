# Wildwood — edytor i gra testowa

Projekt ma dwa katalogi aplikacji:

- `Editor/` — edytor map 2.5D z biblioteką dostarczonych grafik.
- `Runtime/` — gra testowa uruchamiająca zapisaną mapę; wspólny format mapy i renderer znajdują się w `Runtime/World/`.

## Uruchomienie

Wymagany .NET SDK 10. Z głównego katalogu repozytorium:

```sh
dotnet run --project Editor
```

Wybierz grafikę po lewej i kliknij mapę, aby wstawić obiekt. Narzędzie **Select** pozwala zaznaczać i przeciągać obiekty, **Ground** maluje podłoże, a **Player** ustawia postać. Panel po prawej zawiera właściwości.

**Save / Ctrl+S** zapisuje mapę do `Runtime/Maps/world.json`. **Play / F5** uruchamia jej bieżącą wersję wewnątrz edytora. F5 lub Escape wraca do edycji.

Osobna gra odczytuje ostatnio zapisaną mapę:

```sh
dotnet run --project Runtime
```

Przed pierwszym zapisem obie aplikacje wyświetlają przykładową scenę z dostarczonymi obiektami. **New** tworzy pustą mapę. Edytor pyta o niezapisane zmiany przed utworzeniem nowej mapy, wczytaniem lub zamknięciem.

Można pracować na innym pliku:

```sh
dotnet run --project Editor -- --map Runtime/Maps/test.json
dotnet run --project Runtime -- --map Runtime/Maps/test.json
```

## Materiały

Dostarczone 38 grafik jest w `Editor/objects/`. Postać jest tymczasowo niebieskim znacznikiem, a podłoże korzysta z pięciu prostych tekstur generowanych w kodzie. Własne grafiki postaci umieść w `Editor/characters/`, a tekstury podłoża w `Editor/terrain/` — aplikacje wczytują PNG przy starcie.

Szczegóły: [obsługa edytora](Editor/README.md), [gra testowa i kamera](Runtime/README.md).

## Sprawdzenie

```sh
dotnet build
dotnet run --project Editor -- --editor-test
dotnet run --project Runtime -- --camera-test
dotnet run --project Editor -- --interaction-test
```

Ostatni test otwiera okno i symuluje działania myszy oraz klawiatury przez mechanizm automatyzacji Raylib. Używa osobnej mapy w `Editor/obj/interaction-test.json`; nie zmienia map użytkownika. Sprawdza wstawianie, zaznaczanie, przeciąganie, właściwości, cofanie, malowanie, postać, podgląd gry, zapis/wczytanie i zmianę rozmiaru okna.
