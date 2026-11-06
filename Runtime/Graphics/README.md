# Efekty graficzne

[effects.json](effects.json) ustawia efekty świata w Runtime i podglądzie **Play / F5**. Wartości są odczytywane przy **Nowej grze** albo ponownym włączeniu Play. Można zmieniać je bez edytowania kodu i bez ponownego zapisywania map.

Domyślnie wszystkie osiem efektów jest włączonych z delikatną intensywnością. `Enabled: false` wyłącza cały zestaw. Każdy efekt ma też własny przełącznik.

| Efekt | Przełącznik i parametry |
| --- | --- |
| Mgła głębi i niska mgła | `Fog`, `FogStrength`, `FogStart`, `FogEnd`, `GroundFog`. Dystans zaczyna się za punktem skupienia kamery. Niska mgła jest najgęstsza w dolnych partiach mapy i delikatnie przepływa. Kolor uwzględnia dzień i noc. |
| Wiatr roślin | `Wind`, `WindStrength`, `WindSpeed`. Shader wychyla górę grafik roślin z kategorii `vegetation` i `grass`; podstawy pozostają zakotwiczone. Postacie, kolizje oraz tekstury podłoża zachowują swoje położenie. |
| Poświata światła | `Bloom`, `BloomStrength`, `BloomThreshold`. Jasne fragmenty obrazu i lokalne źródła światła trafiają do rozmywanej maski poświaty. |
| Promienie słońca | `SunRays`, `SunRaysStrength`. Promienie w przestrzeni ekranu wykorzystują maskę sylwetek obiektów, w tym przezroczyste fragmenty PNG i ruch roślin. Zanikają w nocy. |
| Rysunkowe kontury | `Outlines`, `OutlineStrength`, `OutlineWidth`. Cienki kontur podkreśla krawędzie sylwetek; kolorowe placeholdery również otrzymują obrys. |
| Gorące powietrze | `HeatDistortion`, `HeatStrength`. Falujące zniekształcenie obrazu występuje lokalnie nad ogniskami i pochodniami. |
| Deszcz i mokre powierzchnie | `Rain`, `RainIntensity`, `Wetness`. Animowane smugi deszczu, kręgi na powierzchni wody oraz przyciemnienie i połysk terenu od słońca i lokalnych świateł. |
| Reakcja na obrażenia | `Damage`, `DamageStrength`, `DamageDuration`, `CameraShake`. Faktyczne trafienie wywołuje krótkie zaczerwienienie brzegów obrazu i wygasające drgnięcie kamery. |

Deszcz, mokry teren, mgła, wiatr i promienie słońca działają na mapach typu **OpenWorld**. Budynki i jaskinie zachowują własne oświetlenie; poświata lokalnych świateł, kontury i gorące powietrze mogą działać również we wnętrzach. Ogniska i pochodnie są rozpoznawane po nazwach grafik zawierających `campfire`, `torch`, `ognisko` lub `pochodnia`, także dla dostarczonej grafiki ogniska.

## Przykładowe zmiany

- Sucha pogoda: `"Rain": false`.
- Mokry teren po deszczu bez widocznych kropli: `"Rain": true`, `"RainIntensity": 0`, `"Wetness": 0.4`.
- Mocniejsza mgła: zwiększ `FogStrength` i zmniejsz `FogEnd`, zachowując `FogEnd` większe od `FogStart` o co najmniej 1.
- Łagodniejsza poświata: zmniejsz `BloomStrength` lub zwiększ `BloomThreshold`.
- Reakcja na obrażenia bez drgania kamery: `"Damage": true`, `"CameraShake": 0`.
- Całkowite wyłączenie efektów: `"Enabled": false`.

Najważniejsze zakresy: intensywności deszczu, mokrości, konturów, promieni i obrażeń 0–1; mgła i bloom 0–2; siła wiatru 0–1 m; tempo wiatru 0–10; szerokość konturu 0.25–4; siła drgania powietrza 0–0.03; czas reakcji na obrażenia 0.1–3 s; drgnięcie kamery 0–0.5 m. JSON używa kropki dziesiętnej, np. `0.3`. Błędne wartości powodują komunikat przy próbie rozpoczęcia gry.

## Renderowanie

Efekty są nakładane na obraz świata przed paskiem HP i oknami rozmów czy ekwipunku. Menu główne i karty fabularne mają osobny wygląd. Przejście z kart do gry nadal przechodzi przez pełną czerń. Podgląd mapy podczas edycji zachowuje surowy obraz i pomocniczą siatkę; podgląd Play pokazuje efekty gry bez siatki.

Poświata używa dwóch kierunków rozmycia w połowie rozdzielczości, a promienie 32 próbek i osobnej maski w połowie rozdzielczości. Jedna klatka wykorzystuje najwyżej 16 źródeł poświaty oraz 16 źródeł gorącego powietrza. Bufory są ponownie używane, odtwarzane po zmianie rozmiaru okna i zwalniane przy zamknięciu aplikacji.

- [GraphicsSettings.cs](GraphicsSettings.cs) — format i walidacja ustawień.
- [WorldVisualFrame.cs](WorldVisualFrame.cs) — parametry shaderów świata i reguły dla wnętrz.
- [GraphicsShaders.cs](GraphicsShaders.cs) — wiatr, kontury, bloom, promienie, deszcz, gorące powietrze i winieta obrażeń.
- [WorldVisualEffects.cs](WorldVisualEffects.cs) — bufory, kolejne etapy obrazu oraz reakcja na trafienia.
- [WorldLighting.cs](../World/WorldLighting.cs) — mgła i mokre podłoże we wspólnym kodzie shaderów materiałów.

## Sprawdzenie

```sh
dotnet run --project Runtime -- --graphics-test
dotnet run --project Runtime -- --graphics-gpu-test
```

Pierwszy test działa bez okna. Drugi porównuje rzeczywiste piksele: wyłączenie efektów, mgłę głębi, zakotwiczone rośliny, kontury, poświatę ogniska, drganie obrazu nad ogniem, promienie, pogodę we wnętrzach i w nocy, mokry teren, deszcz, kręgi na wodzie oraz reakcję na trafienie. Sprawdza także zmianę rozmiaru buforów i zapisuje podglądy w `Editor/obj/graphics-*.png`.
