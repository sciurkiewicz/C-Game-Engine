# Efekty graficzne

[effects.json](effects.json) ustawia efekty świata w Runtime i podglądzie **Play / F5**. Wartości są odczytywane przy **Nowej grze** albo ponownym włączeniu Play. Można zmieniać je bez edytowania kodu i bez ponownego zapisywania map.

Domyślnie wszystkich 12 globalnych efektów jest włączonych z delikatną intensywnością. `Enabled: false` wyłącza ten zestaw. Każdy efekt ma też własny przełącznik. Emitery umieszczone ręcznie na mapie mają osobne działanie opisane poniżej.

## Emitery na mapie

W edytorze wybierz kategorię biblioteki **effects**, następnie kartę i miejsce na mapie. Wbudowane efekty to **Dym**, **Para**, **Gorące powietrze**, **Iskry** oraz **Świetliki**. Są animowane podczas edycji, w Play i Runtime. Pole **Rozmiar efektu** zmienia skalę emitera; domyślnie efekt nie ma kolizji. Zaznaczanie, przesuwanie, duplikowanie, cofanie i zapis działają jak dla pozostałych obiektów.

Gorące powietrze delikatnie załamuje obraz tła w obszarze emitera. Drobna, nieregularna turbulencja szybko przesuwa się ku górze, z niezależnym wzorem dla każdego emitera. Bufor głębokości zapobiega zniekształcaniu przeszkód, które znajdują się przed nim; marker w edytorze wskazuje położenie efektu. Dym i para tworzą unoszące się, miękkie obłoki, iskry lecą do góry, a świetliki krążą wokół emitera.

Ręcznie wstawione emitery działają także przy `Enabled: false` i `HeatDistortion: false`. Te przełączniki dotyczą globalnego zestawu; `HeatDistortion` steruje automatycznym gorącym powietrzem nad ogniskami i pochodniami. Aby usunąć lokalny efekt, usuń jego obiekt z mapy.

Miniatury i cząsteczki są generowane programowo bez dodatkowych plików PNG. W mapie pozostają zwykłe obiekty z wirtualnymi identyfikatorami `objects/effects/smoke.png`, `objects/effects/steam.png`, `objects/effects/heat_haze.png`, `objects/effects/embers.png` oraz `objects/effects/fireflies.png`. Dzięki temu obejmują je istniejące zapisy, prefabrykaty i fragmenty mapy.

## Globalne ustawienia obrazu

| Efekt | Przełącznik i parametry |
| --- | --- |
| Mgła głębi i niska mgła | `Fog`, `FogStrength`, `FogStart`, `FogEnd`, `GroundFog`. Dystans zaczyna się za punktem skupienia kamery. Niska mgła jest najgęstsza w dolnych partiach mapy i delikatnie przepływa. Kolor uwzględnia dzień i noc. |
| Wiatr roślin | `Wind`, `WindStrength`, `WindSpeed`. Shader wychyla górę grafik roślin z kategorii `vegetation` i `grass`; podstawy pozostają zakotwiczone. Postacie, kolizje oraz tekstury podłoża zachowują swoje położenie. |
| Poświata światła | `Bloom`, `BloomStrength`, `BloomThreshold`. Jasne fragmenty obrazu i lokalne źródła światła trafiają do rozmywanej maski poświaty. |
| Dynamiczne światło ognia | `DynamicLights`, `LightFlickerStrength`, `LightFlickerSpeed`. Ogniska i pochodnie nieregularnie zmieniają jasność, a każde źródło ma własny rytm. Teren, postacie, ściany, woda i poświata korzystają z tej samej animacji. Ręcznie dodane światła punktowe zachowują ustawioną intensywność. |
| Głębia ostrości | `DepthOfField`, `FocusRange`, `FocusFalloff`, `MaxBlurRadius`. Ostrość podąża za bohaterem, także po zmianie wysokości terenu i zoomu. Bliższe i dalsze części sceny stopniowo się rozmywają. Efekt działa w rzucie perspektywicznym oraz izometrycznym. |
| Korekcja kolorów | `ColorGrading`, `Exposure`, `Contrast`, `Saturation`, `Temperature`. Ekspozycja, kontrast, nasycenie i ocieplenie lub ochłodzenie palety nadają scenie spójny wygląd. |
| Winieta | `Vignette`, `VignetteStrength`. Delikatnie przyciemnia brzegi obrazu i skupia uwagę na środku sceny. Działa niezależnie od czerwonej winiety obrażeń. |
| Promienie słońca | `SunRays`, `SunRaysStrength`. Promienie w przestrzeni ekranu wykorzystują maskę sylwetek obiektów, w tym przezroczyste fragmenty PNG i ruch roślin. Zanikają w nocy. |
| Rysunkowe kontury | `Outlines`, `OutlineStrength`, `OutlineWidth`. Cienki kontur podkreśla krawędzie sylwetek; kolorowe placeholdery również otrzymują obrys. |
| Gorące powietrze | `HeatDistortion`, `HeatStrength`. Falujące zniekształcenie obrazu występuje lokalnie nad ogniskami i pochodniami. |
| Deszcz i mokre powierzchnie | `Rain`, `RainIntensity`, `Wetness`. Animowane smugi deszczu, kręgi na powierzchni wody oraz przyciemnienie i połysk terenu od słońca i lokalnych świateł. |
| Reakcja na obrażenia | `Damage`, `DamageStrength`, `DamageDuration`, `CameraShake`. Faktyczne trafienie wywołuje krótkie zaczerwienienie brzegów obrazu i wygasające drgnięcie kamery. |

Deszcz, mokry teren, mgła, wiatr i promienie słońca działają na mapach typu **OpenWorld**. Budynki i jaskinie zachowują własne oświetlenie; migotanie ognia, poświata lokalnych świateł, kontury, gorące powietrze, głębia ostrości, korekcja kolorów i winieta działają również we wnętrzach. Ogniska i pochodnie są rozpoznawane po nazwach grafik zawierających `campfire`, `torch`, `ognisko` lub `pochodnia`, także dla dostarczonej grafiki ogniska.

Światła punktowe dodaje się przez **World → Lights** w edytorze: można ustawić ich kolor, położenie, promień i intensywność. `DynamicLights` steruje animacją ognia; po jego wyłączeniu ogniska nadal oświetlają otoczenie stałym światłem. Miękkie cienie kontaktowe obiektów pozostają osobnym ustawieniem **World → Environment → Shadows**.

## Przykładowe zmiany

- Sucha pogoda: `"Rain": false`.
- Mokry teren po deszczu bez widocznych kropli: `"Rain": true`, `"RainIntensity": 0`, `"Wetness": 0.4`.
- Mocniejsza mgła: zwiększ `FogStrength` i zmniejsz `FogEnd`, zachowując `FogEnd` większe od `FogStart` o co najmniej 1.
- Łagodniejsza poświata: zmniejsz `BloomStrength` lub zwiększ `BloomThreshold`.
- Wyraźniejszy efekt miniatury: `"DepthOfField": true`, `"FocusRange": 2`, `"FocusFalloff": 8`, `"MaxBlurRadius": 10`.
- Większy ostry obszar wokół bohatera: zwiększ `FocusRange`; całkowicie ostry obraz: `"DepthOfField": false`.
- Stałe światło ognia: `"DynamicLights": false`. Łagodniejsze migotanie: zmniejsz `LightFlickerStrength`.
- Cieplejsza paleta: `"ColorGrading": true`, `"Temperature": 0.15`; chłodniejsza: wartość ujemna.
- Oryginalne kolory i jasne brzegi: `"ColorGrading": false`, `"Vignette": false`.
- Reakcja na obrażenia bez drgania kamery: `"Damage": true`, `"CameraShake": 0`.
- Całkowite wyłączenie efektów: `"Enabled": false`.

Najważniejsze zakresy: intensywności deszczu, mokrości, konturów, promieni i obrażeń 0–1; mgła i bloom 0–2; siła wiatru 0–1 m; tempo wiatru 0–10; szerokość konturu 0.25–4; siła drgania powietrza 0–0.03; czas reakcji na obrażenia 0.1–3 s; drgnięcie kamery 0–0.5 m. JSON używa kropki dziesiętnej, np. `0.3`. Błędne wartości powodują komunikat przy próbie rozpoczęcia gry.

| Parametr nowych efektów | Zakres i znaczenie |
| --- | --- |
| `LightFlickerStrength` | 0–1, maksymalna względna zmiana jasności ognia; domyślnie `0.18`. |
| `LightFlickerSpeed` | 0–10, szybkość migotania; domyślnie `1.6`. Zero zatrzymuje animację i przywraca stałą jasność. |
| `FocusRange` | 0.25–64 m, półzakres ostrego obszaru: tyle metrów przed i za głębokością bohatera pozostaje ostrych; domyślnie `4`. |
| `FocusFalloff` | 0.25–128 m, dodatkowy dystans poza ostrym obszarem, na którym rozmycie narasta do maksimum; domyślnie `12`. |
| `MaxBlurRadius` | 0–24 piksele przy wysokości obrazu 1080 px; promień skaluje się z rozdzielczością. Domyślnie `7`, zero wyłącza rozmycie. |
| `Exposure` | 0.25–4, mnożnik ekspozycji; domyślnie `1`. |
| `Contrast` | 0.5–1.5, kontrast; domyślnie `1.08`, wartość neutralna `1`. |
| `Saturation` | 0–2, nasycenie; domyślnie `0.95`, zero daje obraz bez kolorów, wartość neutralna `1`. |
| `Temperature` | −1–1, ujemne wartości ochładzają, dodatnie ocieplają paletę; domyślnie `0.06`, wartość neutralna `0`. |
| `VignetteStrength` | 0–1, siła przyciemnienia brzegów; domyślnie `0.22`. |

## Renderowanie

Globalne efekty są nakładane na obraz świata przed paskiem HP i oknami rozmów czy ekwipunku. Menu główne i karty fabularne mają osobny wygląd. Przejście z kart do gry nadal przechodzi przez pełną czerń. Podgląd mapy podczas edycji zachowuje surowy obraz i pomocniczą siatkę, ale pokazuje animowane emitery z kategorii **effects**; podgląd Play pokazuje także globalne efekty gry bez siatki.

Głębia ostrości korzysta z bufora głębokości sceny, a nie z położenia piksela względem góry i dołu ekranu. Płaszczyzna ostrości przechodzi przez środek postaci; gdy postać znajduje się poza widokiem, punktem odniesienia jest cel kamery. Woda zapisuje głębokość swojej powierzchni, dzięki czemu odległe dno nie powoduje rozmycia bliższych fal i refleksów. Częściowo przezroczyste przeszkody korzystają z głębokości powierzchni pod nimi. Przezroczyste piksele PNG pozostawiają głębokość tła.

Nurt malowany w edytorze przez **Water → Kierunek nurtu** przesuwa zarówno fale siatki wody, jak i drobne fale oraz refleksy shadera. Lokalne wektory prędkości są interpolowane biliniowo, dzięki czemu ruch zmienia kierunek na zakrętach bez szwów między komórkami. Dwie warstwy animacji przesunięte w fazie płynnie się przenikają i regularnie odnawiają swoje przesunięcie. Ogranicza to rozciąganie wzoru przy zmiennym kierunku nurtu i zachowuje ciągłość przy odnowieniu fazy. Obszary bez namalowanego prądu oraz starsze mapy zachowują dotychczasową animację. Ustawienia nurtu zapisują się w mapie.

Poświata używa dwóch kierunków rozmycia w połowie rozdzielczości, a promienie 32 próbek i osobnej maski w połowie rozdzielczości. Jedna klatka wykorzystuje najwyżej 16 źródeł poświaty oraz 32 źródła gorącego powietrza. Bufory są ponownie używane, odtwarzane po zmianie rozmiaru okna i zwalniane przy zamknięciu aplikacji.

- [GraphicsSettings.cs](GraphicsSettings.cs) — format i walidacja ustawień.
- [WorldVisualFrame.cs](WorldVisualFrame.cs) — parametry shaderów świata i reguły dla wnętrz.
- [GraphicsShaders.cs](GraphicsShaders.cs) — wiatr, kontury, bloom, głębia ostrości, korekcja kolorów, winieta, promienie, deszcz, gorące powietrze i winieta obrażeń.
- [WorldVisualEffects.cs](WorldVisualEffects.cs) — bufory, kolejne etapy obrazu oraz reakcja na trafienia.
- [WorldFocus.cs](WorldFocus.cs) — automatyczny wybór odległości ostrości dla obu rzutów kamery.
- [WorldLighting.cs](../World/WorldLighting.cs) — światła punktowe i animacja ognia, mgła oraz mokre podłoże we wspólnym kodzie shaderów materiałów.
- [DynamicLightingChecks.cs](DynamicLightingChecks.cs) — sprawdzenie animacji świateł, przełączników i niezależności danych mapy.
- [Hd2dGpuChecks.cs](Hd2dGpuChecks.cs) — porównanie pikseli głębi ostrości, korekcji kolorów i winiety.
- [WorldEffectCatalog.cs](WorldEffectCatalog.cs) — wirtualne zasoby efektów do wstawiania i ich miniatury.
- [WorldEffectParticles.cs](WorldEffectParticles.cs) — animowane cząsteczki dymu, pary, iskier i świetlików.

## Sprawdzenie

```sh
dotnet run --project Runtime -- --graphics-test
dotnet run --project Runtime -- --graphics-gpu-test
dotnet run --project Runtime -- --water-flow-gpu-test
dotnet run --project Editor -- --effects-interaction-test
```

Pierwszy test działa bez okna i obejmuje ustawienia, walidację oraz deterministyczne migotanie ognia bez zmiany mapy. Drugi porównuje rzeczywiste piksele: wyłączenie efektów, mgłę głębi, zakotwiczone rośliny, kontury, poświatę ogniska, drganie obrazu nad ogniem, promienie, pogodę we wnętrzach i w nocy, mokry teren, deszcz, kręgi na wodzie oraz reakcję na trafienie. Sprawdza też rozmycie pierwszego planu i tła w obu rzutach, ostrość podążającą za bohaterem, neutralną i zmienioną korekcję kolorów, winietę oraz zmianę rozmiaru buforów. Zapisuje podglądy w `Editor/obj/graphics-*.png`, w tym `graphics-hd2d-*.png`.

Test GPU obejmuje także emitery mapy. `--effects-interaction-test` sprawdza ich bibliotekę i obsługę w edytorze, w tym wstawianie, zaznaczanie, zmianę rozmiaru, cofanie, zapis i Play.

`--water-flow-gpu-test` porównuje ruch fal zgodnie z namalowanym nurtem, przeciwny kierunek, lokalny zakręt i płynność odnowienia faz animacji. Sprawdza też izolację odległej wody i suchego terenu oraz przywrócenie zwykłej animacji po usunięciu nurtu. Podglądy zapisuje w `Editor/obj/water-flow-*.png`.
