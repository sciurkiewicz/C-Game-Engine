# Runtime

Gra testowa odczytuje mapę stworzoną w edytorze i korzysta z tego samego renderera 2.5D. Obiekty są grafikami częściowo zwróconymi ku kamerze; na podłożu jest widoczna siatka. Gra nie ma stałych paneli UI.

Obiekty i postać dopasowują się do poziomego kierunku kamery, a ich nachylenie wynosi połowę kąta patrzenia w dół (maksymalnie 30°). Podstawa pozostaje zakotwiczona na mapie. To nasze ustawienie częściowego billboardingu inspirowane opisem twórcy Don't Starve, a nie odtworzenie nieudokumentowanych parametrów jego silnika. Podłoże pozostaje płaskie. Renderer, zaznaczanie i obrys w edytorze używają tej samej geometrii.

Z głównego katalogu repozytorium:

```sh
dotnet run --project Runtime
```

Domyślnie wczytuje `Runtime/Maps/world.json`. Jeśli plik jeszcze nie istnieje, pokazuje przykładową scenę. Można wskazać inny plik:

```sh
dotnet run --project Runtime -- --map Runtime/Maps/test.json
```

## Sterowanie

- WASD lub strzałki — chodzenie.
- Lewy Shift — bieganie.
- Kółko myszy / + / - — płynny zoom.
- R — domyślny zoom.
- Escape — wyjście.

Punkt startowy, wygląd postaci, rozmiary obiektów i kolizje ustawia się w edytorze. Nie jest to jeszcze pełna gra RPG: nie ma zbierania przedmiotów ani mechanik przetrwania.

## Kamera

- FOV: 35°.
- Dystans: domyślnie 30, zakres 15–50, krok zoomu 4.
- Pitch: od 30° przy zbliżeniu do 60° przy oddaleniu, domyślnie około 42,9°.
- Stały heading: 90°, zgodnie z ostatnio wybranym widokiem; brak obracania Q/E.
- Płynne podążanie za postacią i ruch względem kierunku kamery.

## Dane

`World/` zawiera wspólny format mapy JSON, katalog grafik, renderer i ruch gracza. Editor kompiluje te same pliki źródłowe. Materiały są w `Editor/objects/`, `Editor/characters/` i `Editor/terrain/` — uruchamiaj aplikacje z zachowaniem struktury repozytorium.

Brakująca grafika obiektu jest zastępowana różowym prostokątem, a brakująca tekstura podłoża trawą. Odwołania zapisane w mapie pozostają zachowane.

## Testy

```sh
dotnet run --project Runtime -- --camera-test
dotnet run --project Runtime -- --billboard-test
dotnet run --project Runtime -- --smoke-test
```

Test kamery działa bez okna. Podgląd wymaga sesji graficznej, kończy się po 15 klatkach i zapisuje `wildwood-smoke.png` w katalogu głównym repozytorium. Warianty kamery: `--smoke-test --close` i `--smoke-test --far`.
