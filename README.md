# Wildwood — edytor i gra

Projekt ma dwa katalogi aplikacji:

- `Editor/` — edytor map 2.5D z biblioteką dostarczonych grafik.
- `Runtime/` — właściwa gra uruchamiająca zapisaną mapę; wspólny format mapy i renderer znajdują się w `Runtime/World/`.

Logika rozgrywki jest w [Runtime/Mechanics/](Runtime/Mechanics/), a dane bohatera, NPC i przeciwników w [Runtime/Characters/](Runtime/Characters/). [Profil bohatera](Runtime/Characters/player.json) zawiera nazwę, opis, HP, obrażenia, prędkości i zasięgi; gra i Play odczytują go przy rozpoczęciu rozgrywki. Szczegóły edycji opisuje [instrukcja postaci](Runtime/Characters/README.md).

[Runtime/Graphics/effects.json](Runtime/Graphics/effects.json) ustawia mgłę, wiatr roślin, poświatę, promienie światła, kontury, gorące powietrze, deszcz z mokrymi powierzchniami oraz reakcję na obrażenia. Każdy efekt można wyłączyć lub dostosować jego intensywność. Zestaw działa w grze i podglądzie Play; szczegóły są w [instrukcji grafiki](Runtime/Graphics/README.md).

## Uruchomienie

Wymagany .NET SDK 10. Z głównego katalogu repozytorium:

```sh
dotnet run --project Editor
```

Wybierz grafikę po lewej i kliknij mapę, aby wstawić obiekt. Narzędzie **Select** pozwala zaznaczać i przeciągać obiekty oraz NPC, **Ground** maluje podłoże, a **Player** ustawia postać. **NPC / 5** dodaje rozmówcę; przycisk **Dialogue** w jego właściwościach otwiera edytor drzewka dialogowego. Panel po prawej zawiera właściwości.

**Save / Ctrl+S** zapisuje mapę do `Runtime/Maps/world.json`. **Play / F5** uruchamia jej bieżącą wersję wewnątrz edytora. F5 lub Escape wraca do edycji.

**View: Perspective / Isometric** u góry po prawej przełącza dotychczasowy rzut perspektywiczny i kamerę izometryczną. Hamburger obok otwiera **Launch options** z wyborem rzutu, startowego zoomu i limitu FPS. Opcje zapisują się z mapą i działają w Play oraz Runtime. Siatkę pomocniczą przełącza **G** wyłącznie podczas edycji; w grze i podglądzie Play jest ukryta.

**View / V** udostępnia też **Free camera**: RMB rozgląda, WASD lata, Q/E zmienia wysokość, Shift przyspiesza, a Alt+LMB obraca wokół zaznaczenia. Wolna kamera pozostaje niezależna od widoku gry. **Ctrl+F** wyszukuje obiekty na mapie, pola w bibliotece filtrują grafiki, **M** włącza klikalną minimapę całego świata. **Select** obsługuje zaznaczanie prostokątem i wspólne właściwości grupy. **Place → Object brush** maluje grupami obiektów z losową skalą i odstępami, a **Sculpt → Terrain stamps** nakłada wzgórza, góry, grzbiety i kratery pojedynczymi kliknięciami. **Hamburger → Editor settings / recovery** ustawia automatyczne kopie i przywraca ostatnie pięć wersji; domyślnie kopia powstaje co minutę w `.wildwood/autosaves/`.

**Ground → Roads** rysuje edytowalne drogi przez punkty, z wybranym materiałem, szerokością, miękkim brzegiem i usuwaniem roślinności. Enter zatwierdza, Backspace usuwa punkt, a **Road list** wraca do zapisanych krzywych. **Ground → Biomes** maluje las, łąkę lub teren skalisty wraz z materiałami i dostępnymi obiektami. **Sculpt → Terrain erosion** symuluje spływ wody i osypywanie zboczy po postawieniu gór. Drogi działają także w Play i Runtime; wszystkie operacje obsługują cofanie oraz zapis mapy.

**Ctrl+Shift+C / hamburger → Map fragments** zaznacza i kopiuje cały obszar: teren, wysokości, wodę, obiekty, NPC, skrzynie, potwory, drogi i elementy świata. **Ctrl+V** uruchamia podgląd; klikaj, aby wklejać niezależne kopie. **Paste options** wybiera kategorie oraz dodawanie lub zastępowanie zawartości, **Save fragment / Fragment library** zapisują i otwierają gotowe obozy czy fragmenty wiosek w `Editor/fragments/`. Jedno Ctrl+Z cofa całe wklejenie.

**World** otwiera ustawienia oświetlenia, cieni, zanikania przeszkód, cyklu dnia i nocy, dźwięku przestrzennego, triggerów, prefabrykatów oraz kształtów kolizji. Shift+klik zaznacza grupy; prefabrykaty zapisują się w `Editor/prefabs/`. Triggery i wielokąty kolizji rysuje się bezpośrednio na mapie. W Play i Runtime **F6** zapisuje postęp, **F9** go przywraca. Teren korzysta z fragmentów siatki GPU 16 × 16 i przebudowuje je po zmianach wysokości. Szczegóły obsługi są w [instrukcji edytora](Editor/README.md).

Osobna gra odczytuje ostatnio zapisaną mapę:

```sh
dotnet run --project Runtime
```

Gra otwiera proste menu: **Nowa gra**, nieaktywne **Wczytaj grę**, **Opcje** i **Wyjdź z gry**. W opcjach ustawisz głośność i pełny ekran. Mysz lub strzałki + Enter obsługują menu; Escape podczas rozgrywki wraca do niego po zamknięciu rozmowy lub ekwipunku.

Po wybraniu **Nowej gry** uruchamia się edytowalny wstęp z kart narracyjnych. [Runtime/Narrative/intro.json](Runtime/Narrative/intro.json) zawiera bloki tekstu, opcjonalny obraz, czas i efekt każdej karty: pisanie maszynowe, fade albo przewijanie. [Instrukcja kart](Runtime/Narrative/README.md) wyjaśnia edycję. Spacja / Enter odsłania tekst i przechodzi dalej, Esc pomija wstęp; świat rusza po zakończeniu kart.

Przed pierwszym zapisem obie aplikacje wyświetlają przykładową scenę z dostarczonymi obiektami. Wyróżniony przycisk **+ New map** u góry obok nazwy pliku lub **Ctrl+N** pozwala wybrać szerokość i głębokość pustej mapy, od 4 do 2048 kafelków na bok. Zatwierdź rozmiar przyciskiem **Create** lub klawiszem Enter. Edytor pyta o niezapisane zmiany przed utworzeniem nowej mapy, wczytaniem lub zamknięciem.

W grze podejdź do NPC i naciśnij **E**. Dialog ma strukturę **Start → kwestia NPC → odpowiedzi bohatera → kolejne kwestie NPC**. Odpowiedź wybierzesz myszą, klawiszami 1–9 albo strzałkami i Enter. Escape zamyka rozmowę; podczas dialogu postać pozostaje w miejscu.

Odpowiedzi mogą wracać do wcześniejszych kwestii NPC. W edytorze drzewka zaznacz odpowiedź, wybierz **Redirect to NPC** i kliknij docelową kwestię, np. główne pytania. Przekierowania zapisują się w mapie i działają w Play oraz Runtime.

W drzewku możesz wybrać **+ Player Reply** (opcja bohatera) albo **+ NPC Reply** (kolejna kwestia NPC). NPC może mówić kilka kwestii po sobie: Enter lub kliknięcie „dalej” przechodzi do kolejnej, a odpowiedzi gracza pojawiają się na końcu sekwencji. Dodanie kwestii NPC zachowuje dotychczasowe opcje bohatera.

**Chest / 8** wstawia skrzynię; przycisk **Items** w jej właściwościach przypisuje przedmioty. **Player → Starting inventory** ustawia startowe EQ. Ekwipunek ma siatkę 10×4, skrzynia 10×6, a każdy item zajmuje prostokąt pól jak w Diablo. Jest pięć kolorowych placeholderów o rozmiarach od 1×1 do 2×3. W grze **I** otwiera EQ, **E** pobliską skrzynię. Przeciągaj itemki między siatkami lub użyj Shift+klik; **I / Escape** zamyka panel. Zawartość przypisana w edytorze zapisuje się w mapie.

**Monster / 9** dodaje przeciwnika z PNG lub czerwonym placeholderem. **Stats** ustawia HP, obrażenia, prędkość, zasięg wykrywania i ataku oraz przerwę między atakami. W Play i Runtime potwory ścigają bohatera, omijają przeszkody i atakują. **Spacja** atakuje, pojedynczy pasek pokazuje życie bohatera, **Enter** po śmierci rozpoczyna mapę od nowa. Stały HUD zawiera tylko ten pasek. Rozmowa i EQ pauzują walkę. Testowanie potworów zachowuje mapę w edytorze.

**Sculpt / 6** podnosi, obniża, wygładza i wyrównuje powierzchnię w krokach 0,25 m. **Water / 7 → Fill basin** wypełnia kliknięte zagłębienie wodą do ustawionego poziomu. Najpierw wykop zbiornik narzędziem Sculpt; woda zachowuje dno i kończy się na brzegu wyznaczonym przez teren. **Drain basin** opróżnia zbiornik. Wysokości terenu i ustawienia wody zapisują się w mapie; obiekty oraz postacie opierają się na podłożu. Głęboka woda i strome zbocza zatrzymują ruch w grze.

Tekstury podłoża automatycznie blendują się na granicach i w narożnikach kafelków, również na zboczach. Efekt obejmuje wbudowane materiały oraz własne PNG i działa w edytorze, Play i Runtime.

Można pracować na innym pliku:

```sh
dotnet run --project Editor -- --map Runtime/Maps/test.json
dotnet run --project Runtime -- --map Runtime/Maps/test.json
```

## Materiały

Dostarczone 38 grafik jest w `Editor/objects/`. Postać jest tymczasowo niebieskim znacznikiem, a podłoże korzysta z edytowalnych plików PNG w `Editor/terrain/` (`grass.png`, `dirt.png`, `sand.png`, `stone.png`). Własne grafiki postaci umieść w `Editor/characters/`, a tekstury podłoża w `Editor/terrain/` — aplikacje wczytują PNG przy starcie. Po edycji tekstury uruchom ponownie edytor lub Runtime; istniejące mapy od razu skorzystają ze zmienionych plików.

Szczegóły: [obsługa edytora](Editor/README.md), [gra i kamera](Runtime/README.md).

## Sprawdzenie

```sh
dotnet build
dotnet run --project Editor -- --editor-test
dotnet run --project Runtime -- --camera-test
dotnet run --project Runtime -- --character-test
dotnet run --project Runtime -- --world-test
dotnet run --project Runtime -- --world-gpu-test
dotnet run --project Runtime -- --dialogue-test
dotnet run --project Runtime -- --inventory-test
dotnet run --project Runtime -- --monster-test
dotnet run --project Runtime -- --terrain-test
dotnet run --project Runtime -- --terrain-blend-test
dotnet run --project Editor -- --interaction-test
```

Ostatni test otwiera okno i symuluje działania myszy oraz klawiatury przez mechanizm automatyzacji Raylib. Używa osobnych map w `Editor/obj/`; nie zmienia map użytkownika. Sprawdza wstawianie, zaznaczanie, przeciąganie, właściwości, cofanie, malowanie, terraformowanie, wodę 3D, postać na nierównym terenie, rozmiar nowej mapy, NPC, edycję drzewa, rozmowy, skrzynie i ekwipunek w podglądzie gry, zapis/wczytanie i zmianę rozmiaru okna.
