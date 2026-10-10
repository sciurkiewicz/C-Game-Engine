# Runtime

Gra testowa odczytuje mapę stworzoną w edytorze i korzysta z tego samego renderera 2.5D. Obiekty i NPC są grafikami częściowo zwróconymi ku kamerze; na podłożu jest widoczna siatka. Podejście do NPC wyświetla podpowiedź rozmowy, a E otwiera jego dialog.

Obiekty i postać dopasowują się do poziomego kierunku kamery, a ich nachylenie wynosi połowę kąta patrzenia w dół (maksymalnie 30°). Podstawa pozostaje zakotwiczona na wysokości terenu. To nasze ustawienie częściowego billboardingu inspirowane opisem twórcy Don't Starve, a nie odtworzenie nieudokumentowanych parametrów jego silnika. Podłoże jest siatką trójkątów o wysokościach edytowanych pędzlem Sculpt. Renderer, zaznaczanie i obrys w edytorze używają tej samej geometrii.

Woda jest osobną powierzchnią 3D z animowanymi falami, przezroczystością i cieniowaniem. Wypełnia połączone zagłębienie do wybranego poziomu, a brzeg wyznacza przecięcie z terenem. Dno zachowuje wysokości i materiał. Wysokości wierzchołków terenu są zaokrąglane do 0,25 m. Postać porusza się na wysokości podłoża, a kamera podąża również pionowo. Głębokość ponad 0,55 m oraz strome zbocza blokują ruch.

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
- E — rozmowa z najbliższym NPC w odległości do 2,5 jednostki.
- Podczas rozmowy: kliknięcie odpowiedzi, 1–9 albo strzałki góra/dół i Enter — wybór odpowiedzi. Kółko przewija długie kwestie lub listę odpowiedzi pod kursorem.
- Enter lub kliknięcie — zakończenie ostatniej kwestii NPC. Odpowiedź bohatera bez dalszej kwestii kończy rozmowę od razu.
- Escape — zamknięcie rozmowy; poza rozmową wyjście z gry.

Rozmowa zatrzymuje ruch gracza. Każde nowe zagadanie zaczyna się od Start. Drzewko oraz nazwa, grafika, wysokość i kolizja NPC są ustawiane w edytorze i zapisane w mapie.

Punkt startowy, wygląd postaci, rozmiary obiektów i kolizje ustawia się w edytorze. Nie jest to jeszcze pełna gra RPG: nie ma zbierania przedmiotów ani mechanik przetrwania.

## Kamera

- FOV: 35°.
- Dystans: domyślnie 30, zakres 15–50, krok zoomu 4.
- Pitch: od 30° przy zbliżeniu do 60° przy oddaleniu, domyślnie około 42,9°.
- Stały heading: 90°, zgodnie z ostatnio wybranym widokiem; brak obracania Q/E.
- Płynne podążanie za postacią i ruch względem kierunku kamery.

## Dane

`World/` zawiera wspólny format mapy JSON, katalog grafik, renderer, ruch gracza, teren, wodę i system dialogów. Editor kompiluje te same pliki źródłowe. NPC są w tablicy `Npcs`; każdy ma własne drzewko z identyfikatorami węzłów i uporządkowanymi połączeniami `Next`. Starsze mapy bez `Npcs` wczytują się z pustą listą NPC. Mapy zachowują niezależne wymiary `Width` i `Depth`. `Heights` przechowuje wysokości `(Width + 1) × (Depth + 1)` wierzchołków lub pustą tablicę dla płaskiej mapy; `WaterLevel` i `WaveHeight` określają poziom i amplitudę wody. `WaterCells` to maski dwóch trójkątów każdego kafelka, zapisane jako Base64; materiały dna pozostają w `Tiles`. Starsze mapy z polami water w `Tiles` pozostają obsługiwane. Materiały są w `Editor/objects/`, `Editor/characters/` i `Editor/terrain/` — uruchamiaj aplikacje z zachowaniem struktury repozytorium.

Brakująca grafika obiektu jest zastępowana różowym prostokątem, a brakująca tekstura podłoża trawą. Odwołania zapisane w mapie pozostają zachowane.

## Testy

```sh
dotnet run --project Runtime -- --camera-test
dotnet run --project Runtime -- --billboard-test
dotnet run --project Runtime -- --dialogue-test
dotnet run --project Runtime -- --map-test
dotnet run --project Runtime -- --terrain-test
dotnet run --project Runtime -- --smoke-test
```

Testy kamery, dialogów, map i terenu działają bez okna. Test dialogów sprawdza rozgałęzienia, zakończenia, zasięg rozmowy, kolizje NPC, walidację drzewa, polskie teksty i zgodność starszych map. Test map sprawdza tworzenie, malowanie, cofanie, zapis/wczytanie i podgląd mapy 2048 × 2048 oraz ograniczenie renderowania podłoża do widocznego obszaru. Test terenu sprawdza pędzle, trafianie promieniem w siatkę, zbocza, wysokość postaci, geometrię wody, fale i przycinanie brzegów. Podgląd wymaga sesji graficznej, kończy się po 15 klatkach i zapisuje `wildwood-smoke.png` w katalogu głównym repozytorium. Warianty kamery: `--smoke-test --close` i `--smoke-test --far`. `--dialogue-smoke-test --map ścieżka.json` pokazuje rozmowę pierwszego NPC z podanej mapy i kończy się w ten sam sposób.
