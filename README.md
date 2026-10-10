# Wildwood — edytor i gra testowa

Projekt ma dwa katalogi aplikacji:

- `Editor/` — edytor map 2.5D z biblioteką dostarczonych grafik.
- `Runtime/` — gra testowa uruchamiająca zapisaną mapę; wspólny format mapy i renderer znajdują się w `Runtime/World/`.

## Uruchomienie

Wymagany .NET SDK 10. Z głównego katalogu repozytorium:

```sh
dotnet run --project Editor
```

Wybierz grafikę po lewej i kliknij mapę, aby wstawić obiekt. Narzędzie **Select** pozwala zaznaczać i przeciągać obiekty oraz NPC, **Ground** maluje podłoże, a **Player** ustawia postać. **NPC / 5** dodaje rozmówcę; przycisk **Dialogue** w jego właściwościach otwiera edytor drzewka dialogowego. Panel po prawej zawiera właściwości.

**Save / Ctrl+S** zapisuje mapę do `Runtime/Maps/world.json`. **Play / F5** uruchamia jej bieżącą wersję wewnątrz edytora. F5 lub Escape wraca do edycji.

Osobna gra odczytuje ostatnio zapisaną mapę:

```sh
dotnet run --project Runtime
```

Przed pierwszym zapisem obie aplikacje wyświetlają przykładową scenę z dostarczonymi obiektami. **New** pozwala wybrać szerokość i głębokość pustej mapy, od 4 do 2048 kafelków na bok. Edytor pyta o niezapisane zmiany przed utworzeniem nowej mapy, wczytaniem lub zamknięciem.

W grze podejdź do NPC i naciśnij **E**. Dialog ma strukturę **Start → kwestia NPC → odpowiedzi bohatera → kolejne kwestie NPC**. Odpowiedź wybierzesz myszą, klawiszami 1–9 albo strzałkami i Enter. Escape zamyka rozmowę; podczas dialogu postać pozostaje w miejscu.

**Sculpt / 6** podnosi, obniża, wygładza i wyrównuje powierzchnię w krokach 0,25 m. **Water / 7 → Fill basin** wypełnia kliknięte zagłębienie wodą do ustawionego poziomu. Najpierw wykop zbiornik narzędziem Sculpt; woda zachowuje dno i kończy się na brzegu wyznaczonym przez teren. **Drain basin** opróżnia zbiornik. Wysokości terenu i ustawienia wody zapisują się w mapie; obiekty oraz postacie opierają się na podłożu. Głęboka woda i strome zbocza zatrzymują ruch w grze.

Można pracować na innym pliku:

```sh
dotnet run --project Editor -- --map Runtime/Maps/test.json
dotnet run --project Runtime -- --map Runtime/Maps/test.json
```

## Materiały

Dostarczone 38 grafik jest w `Editor/objects/`. Postać jest tymczasowo niebieskim znacznikiem, a podłoże korzysta z prostych tekstur generowanych w kodzie. Własne grafiki postaci umieść w `Editor/characters/`, a tekstury podłoża w `Editor/terrain/` — aplikacje wczytują PNG przy starcie.

Szczegóły: [obsługa edytora](Editor/README.md), [gra testowa i kamera](Runtime/README.md).

## Sprawdzenie

```sh
dotnet build
dotnet run --project Editor -- --editor-test
dotnet run --project Runtime -- --camera-test
dotnet run --project Runtime -- --dialogue-test
dotnet run --project Runtime -- --terrain-test
dotnet run --project Editor -- --interaction-test
```

Ostatni test otwiera okno i symuluje działania myszy oraz klawiatury przez mechanizm automatyzacji Raylib. Używa osobnych map w `Editor/obj/`; nie zmienia map użytkownika. Sprawdza wstawianie, zaznaczanie, przeciąganie, właściwości, cofanie, malowanie, terraformowanie, wodę 3D, postać na nierównym terenie, rozmiar nowej mapy, NPC, edycję drzewa, rozmowy w podglądzie gry, zapis/wczytanie i zmianę rozmiaru okna.
