# Editor

Uruchom z głównego katalogu repozytorium:

```sh
dotnet run --project Editor
```

## Narzędzia

| Narzędzie | Obsługa |
| --- | --- |
| Select / 1 | Kliknij obiekt, przeciągnij go po mapie. Panel po prawej zmienia X, Z, wysokość i kolizję. |
| Place / 2 | Wybierz grafikę z biblioteki i kliknij mapę. Kolejne kliknięcia wstawiają kolejne obiekty. |
| Ground / 3 | Paint maluje materiałem, Roads rysuje drogi przez punkty, a Biomes maluje teren wraz z roślinnością i kamieniami. |
| Player / 4 | Kliknij mapę, aby ustawić punkt startowy. Możesz wybrać grafikę postaci i jej wysokość. |
| NPC / 5 | Wybierz PNG postaci lub złoty znacznik i kliknij mapę, aby dodać NPC. Ostatnio dodany NPC pozostaje zaznaczony; przycisk po prawej otwiera jego dialog. |
| Sculpt / 6 | Rzeźbienie pędzlem, stemple terenu oraz erozja wodna i osypywanie zboczy. |
| Water / 7 | Wypełnij lub opróżnij zbiornik, wykop rzekę pędzlem i namaluj kierunek jej nurtu. |
| Chest / 8 | Kliknij mapę, aby wstawić skrzynię. Zaznacz ją i wybierz Items, aby przypisać zawartość. |
| Monster / 9 | Wybierz PNG postaci lub czerwony znacznik i kliknij mapę. Zaznacz potwora i wybierz Stats, aby ustawić walkę. |
| Ściany / 0 | Siatka maluje pełne bloki ścian lochu; Punkty zachowują rysowanie ścian przez węzły. |

Obiekty można duplikować, usuwać i skalować. Kolizje są prostymi okręgami wokół podstawy obiektu. Wysokość wpływa na ich promień. Pozycje są domyślnie przyciągane do siatki co 0,5 jednostki; przycisk **Snap** wyłącza przyciąganie.

Każde narzędzie pozostaje aktywne po użyciu. Można stawiać kolejne obiekty, NPC, skrzynie i Monstery bez ponownego wybierania zakładki. Dialog, Items i Stats po prawej edytują ostatnio dodanego aktora, a zamknięcie ich okna wraca do tej samej zakładki. **Select / 1** wybiera się ręcznie, aby przesuwać obiekty lub edytować wszystkie ich właściwości.

Biblioteka automatycznie odczytuje PNG z `Editor/objects/` oraz podkatalogów. Strzałki nad biblioteką zmieniają kategorię, kółko myszy przewija listę. Przezroczyste marginesy są pomijane podczas wczytywania, bez modyfikowania plików źródłowych.

Pole **Search graphics / Search materials** filtruje bibliotekę po nazwie i ścieżce, bez rozróżniania wielkości liter. Działa także dla grafik postaci i podłoża. Kliknij **x**, aby usunąć filtr. Podczas wpisywania tekstu skróty narzędzi, ruch kamery i F5 są zablokowane; Enter lub Escape kończy wpisywanie.

**Find objects / Ctrl+F** oraz **hamburger → Find scene objects** otwierają wyszukiwarkę obiektów już umieszczonych na mapie. Wyniki obejmują dekoracje, NPC, skrzynie, potwory, punkt startowy, triggery, światła i dźwięki. Możesz filtrować typ, nazwę, grafikę lub identyfikator. Kliknięcie wyniku albo Enter dla pierwszego wyniku centruje kamerę i otwiera właściwości danego elementu.

W **Place → Object brush: ON → Brush settings** ustaw promień, liczbę obiektów na odcisk, minimalny odstęp i losową zmianę rozmiaru. Przytrzymaj LMB i przesuwaj mysz, aby malować obiektami. **Ctrl+klik grafik w bibliotece** miesza kilka wybranych PNG. Pędzel pomija głęboką wodę i strome zbocza, zachowuje odstępy od istniejących aktorów i nie dokłada obiektów co klatkę przy nieruchomej myszy. Całe pociągnięcie cofa się jednym Ctrl+Z. Pędzel działa dla zwykłych obiektów; prefabrykaty i skrzynie zachowują osobne narzędzia.

Nowe obiekty i skrzynie zachowują wspólną skalę **128 pikseli = 1 jednostka świata**: grafika 256 × 256 jest dwa razy szersza i wyższa niż 128 × 128. Przycinanie przezroczystych marginesów usuwa tylko puste miejsce, a zmniejszanie dużych tekstur na GPU nie zmienia skali. Wysokość można nadal zmienić ręcznie; zapisane mapy zachowują ustawione rozmiary.

## Efekty do wstawiania na mapę

W bibliotece wybierz kategorię **effects**, kliknij kartę efektu i następnie miejsce na mapie. Kolejne kliknięcia dodają niezależne emitery. Dostępne są **Dym**, **Para**, **Gorące powietrze**, **Iskry** i **Świetliki**. Animacja jest widoczna od razu podczas edycji, a także w **Play / F5** i Runtime.

**Select / 1** zaznacza i przesuwa efekt tak jak zwykły obiekt. Pole **Rozmiar efektu** we właściwościach zmienia wielkość emitera. Efekty domyślnie nie mają kolizji. Obsługują zapis mapy, Ctrl+Z / Ctrl+Y, Ctrl+D, usuwanie i kopiowanie fragmentów mapy.

**Gorące powietrze** zniekształca obraz za emiterem, dając falowanie nad rozgrzanym miejscem. Przeszkody przed emiterem zasłaniają zniekształcenie; marker w widoku edycji ułatwia jego znalezienie. Pozostałe efekty pokazują unoszące się cząsteczki. Ręcznie wstawione emitery działają niezależnie od globalnego `Enabled` w [effects.json](../Runtime/Graphics/effects.json). `HeatDistortion` nadal steruje automatycznym falowaniem nad ogniskami i pochodniami.

Efekty i ich miniatury są generowane programowo: nie trzeba dodawać plików PNG. Mapa przechowuje je jako obiekty z identyfikatorami `objects/effects/smoke.png`, `steam.png`, `heat_haze.png`, `embers.png` i `fireflies.png` w tym samym katalogu wirtualnych zasobów.

Test biblioteki, wstawiania, edycji, zapisu i Play:

```sh
dotnet run --project Editor -- --effects-interaction-test
```

## Ściany

Osobna zakładka **Ściany / 0** ma dwa tryby: **Siatka** do labiryntów, korytarzy i lochów oraz **Punkty** do ścian rysowanych przez węzły. Oba tworzą stałe bryły 3D z materiałem **Skała jaskiniowa**, **Mur** lub **Drewno**.

W **Siatce** przytrzymaj LMB i przeciągaj po podłożu, aby malować pełne bloki ścian o wielkości **1 × 1 m**. Puste kratki pozostają podłogą i tworzą korytarze. **Shift+LMB** albo **Wymazuj / kuj korytarz** wykuwa przejścia. **Obrys pokoju** pozwala przeciągnąć prostokąt: powstają ściany obwodowe, a środek zostaje pusty. Pędzel ma szerokość 1, 3, 5 lub 7 kratek. Jedno Ctrl+Z cofa całe pociągnięcie lub cały pokój.

**Skała jaskiniowa** w siatce ma nierówne boki, zmienny przekrój i poszarpaną górną krawędź; suwak **Nierówność skały** zmienia intensywność 0–1. To zmiany geometrii, a kolizja odpowiada jej przebiegowi. Sąsiednie bloki łączą się wspólnymi krawędziami i pomijają zasłonięte ściany. **Mur** oraz **Drewno** pozostają regularne, odpowiednie do budynków i klasycznych labiryntów. Ustaw wysokość 0,5–12 m, kolizję i odsłanianie gracza przed malowaniem. Malowanie istniejącej kratki aktualizuje jej ustawienia. Kształt jaskini nie zmienia się przy przesuwaniu kamery, zapisie lub wczytaniu. Limit wynosi 65536 kratek ścian.

W **Punktach** klikaj kolejne punkty na podłożu, aby narysować przebieg ściany; przeciągaj zielone uchwyty, aby je przesuwać. Po prawej ustawisz wysokość 0,25–24 m, grubość 0,1–4 m, kolizję i odsłanianie gracza. **Zamknij obrys / pokój** łączy ostatni punkt z pierwszym. Pozostaw przerwę między osobnymi ścianami na wejście lub obiekt drzwi.

**Enter / Zapisz** zatwierdza cały szkic jako jedną operację cofania. **Backspace / Cofnij punkt** usuwa ostatni punkt, a **Esc** odrzuca szkic bez zmiany zapisanej ściany. Wybierz ścianę z listy po lewej lub użyj **Wybierz ścianę na mapie**, aby edytować jej przebieg, materiał i nazwę. Zapisz zmiany Enterem; **Delete / Usuń ścianę** usuwa wybraną ścianę. **Ctrl+Z / Ctrl+Y** cofa i przywraca całą operację.

Ściany podążają za wysokością podłoża i zachowują położenie w perspektywie, izometrii oraz wolnej kamerze. Widać je na minimapie i w wyszukiwarce sceny. Kopiowanie fragmentów mapy obejmuje osobną kategorię **Walls**, przycina ściany do obszaru i nadaje kopiom nowe identyfikatory. Zastępowanie zachowuje odcinki poza wklejanym prostokątem.

Właściwa gra i Play odczytują te same ściany: blokują ruch bohatera, potworów i ciosy. Materiały reagują na światła oraz efekty graficzne. Gdy ściana zasłania bohatera, ustawienie **World → Environment → Fade occluders** i jej opcja **Odsłanianie gracza** pozwalają ją wygaszać. Mapa mieści do 256 ścian po 128 punktów; starsze mapy otrzymują pustą listę ścian.

Test interfejsu, zapisu, kamer, Play i renderowania materiałów:

```sh
dotnet run --project Editor -- --wall-interaction-test
```

Sam tryb lochów: `dotnet run --project Editor -- --grid-wall-interaction-test`. Testy danych i geometrii: `dotnet run --project Runtime -- --grid-wall-test`.

Duże lochy korzystają z buforów GPU w fragmentach **8 × 8 kratek** i rysują fragmenty widoczne w kamerze. Malowanie przebudowuje tylko zmieniony fragment i jego bezpośrednich sąsiadów; zmiany wysokości podłoża odświeżają odpowiadające im siatki. Wygaszanie przed graczem aktualizuje kolor bufora bez przebudowy geometrii. Nierówne kształty skał, materiały oraz kolizje zachowują ten sam wygląd i przebieg. Bufory odległych fragmentów są zwalniane, a minimapa przechowuje ściany w swojej teksturze. Malowanie i sprawdzanie niezapisanych zmian korzystają z indeksu kratek, bez kopiowania i serializowania całego lochu co klatkę.

`dotnet run --project Runtime -- --wall-cache-gpu-test` porównuje piksele z rysowaniem pojedynczych brył w trzech widokach oraz sprawdza lokalną przebudowę, odsłanianie gracza i zwalnianie buforów. `dotnet run --project Editor -- --wall-performance-test` mierzy edytor, minimapę, świat gry i malowanie na pomocniczej scenie **15 360 bloków**; wyniki trafiają do katalogu kompilacji `obj/`.

## Sterowanie i zapis

- WASD lub przeciąganie prawym/środkowym przyciskiem — przesuwanie widoku w gotowych rzutach.
- Kółko nad mapą — zoom; w wolnej kamerze przelot do przodu lub do tyłu.
- V — kolejny tryb widoku: Perspective → Isometric → Free camera.
- M — włączenie/wyłączenie minimapy.
- Ctrl+F — wyszukiwanie obiektów na mapie.
- Ctrl+Shift+C — zaznaczanie fragmentu mapy; Ctrl+C odświeża kopię zaznaczenia, Ctrl+V otwiera podgląd wklejania.
- F — skupienie widoku na zaznaczonym obiekcie albo postaci.
- G — widoczność siatki pomocniczej z kratkami 0,5 × 0,5 jednostki, zgodnymi z przyciąganiem obiektów; działa tylko podczas edycji mapy.
- Delete — usunięcie zaznaczenia; Ctrl+D — duplikowanie obiektu.
- Ctrl+Z / Ctrl+Y — cofnięcie / ponowienie, do 80 kroków. Całe pociągnięcie pędzla jest jednym krokiem.
- Ctrl+S — zapis; Ctrl+O — wczytanie; Ctrl+N — nowa, pusta mapa.
- Escape podczas edycji — powrót do zaznaczania.
- F5 — włączenie/wyłączenie podglądu gry. Escape zamyka aktywny dialog lub ekwipunek; poza nimi kończy podgląd.

Domyślny plik: `Runtime/Maps/world.json`. Argument `--map ścieżka.json` zmienia plik roboczy. Zapis zawiera obiekty, NPC i dialogi, Monstery ze statystykami, skrzynie z zawartością, startowy ekwipunek, pozycję i grafikę postaci, materiał kafelków, wysokości powierzchni oraz ustawienia wody. Pliki PNG są wspólne dla edytora i Runtime; JSON przechowuje ścieżki względne, więc repozytorium można przenieść.

Podgląd **Play** korzysta z kopii bieżącej mapy, także przed zapisem. Ruch w podglądzie nie zmienia punktu startowego. Osobny Runtime odczytuje mapę z dysku.

Przycisk **View** u góry po prawej oraz **V** przełączają **Perspective → Isometric → Free camera**. Perspective i Isometric ustawiają też rzut kamery gry. **Perspective** zachowuje dotychczasową perspektywę i zmianę nachylenia przy zoomie. **Isometric** to rzut ortograficzny z kierunkiem 45° i stałym nachyleniem około 35,26°; zoom zmienia skalę widoku.

**Free camera** jest osobnym trybem roboczym edytora i można ją włączyć bezpośrednio przez **hamburger → Free camera**. Nie zmienia ustawień Play ani Runtime. **RMB + mysz** rozgląda się, **WASD** lata względem kierunku kamery, **Q/E** obniża/podnosi kamerę, **Shift** przyspiesza. **RMB + kółko** zmienia prędkość, samo kółko przesuwa kamerę, środkowy przycisk przesuwa kadr, a **Alt+LMB** obraca kamerę wokół zaznaczenia lub ostatniego punktu skupienia. **F** wraca do zaznaczonego obiektu albo startu gracza. Po zatrzymaniu Play edytor zachowuje wolną kamerę.

Podgląd **Play / F5** pokazuje również efekty graficzne gry: mgłę, wiatr roślin, bloom, migotanie światła ognia, głębię ostrości podążającą za bohaterem, korekcję kolorów, winietę, promienie słońca, kontury, gorące powietrze, deszcz z mokrym terenem i reakcję na obrażenia. Ustawienia znajdują się w [effects.json](../Runtime/Graphics/effects.json); po ich zmianie zatrzymaj i ponownie uruchom Play. W widoku roboczym mapy globalne efekty obrazu są wyłączone, a siatka i narzędzia pozostają dostępne. Emitery wstawione z kategorii **effects** są animowane także podczas edycji. [Instrukcja grafiki](../Runtime/Graphics/README.md) opisuje wszystkie parametry.

**Minimap / M**, przycisk we właściwościach i pozycja w hamburgerze włączają podgląd całej bieżącej mapy w prawym górnym rogu widoku roboczego. Minimapę widać podczas edycji; pokazuje materiały i ukształtowanie terenu, wodę, obiekty, aktorów, triggery, zaznaczenie, punkt startowy oraz obszar widziany przez kamerę. Kliknięcie przestawia kamerę bez użycia aktywnego narzędzia. Zachowuje proporcje prostokątnych map i używa ograniczonej liczby próbek również dla 2048 × 2048.

**Hamburger → Editor settings / recovery** ustawia automatyczne kopie odzyskiwania: wyłączone albo co **30 / 60 / 120 / 300 s**, domyślnie co minutę. Edytor przechowuje pięć ostatnich różnych stanów dla każdego pliku mapy w `.wildwood/autosaves/`, niezależnie od ręcznego zapisu. Zapis odbywa się w tle po zakończeniu edycji myszą i nie kasuje oznaczenia niezapisanych zmian. Po uruchomieniu edytor proponuje odzyskanie kopii nowszej niż mapa na dysku. **Browse recovery copies** pozwala przywrócić wcześniejszą wersję; przywrócenie można cofnąć. **Ctrl+S** nadal zapisuje mapę do jej pliku. Ustawienia edytora są przechowywane w `.wildwood/editor-settings.json`, poza formatem mapy.

**Menu hamburgerowe w prawym górnym rogu → Launch options** otwiera zaawansowane opcje uruchomieniowe: rzut kamery, początkową odległość kamery (15–50 m) i limit 30 / 60 / 120 FPS lub bez limitu. **Apply / Enter** zatwierdza, **Apply & Play** zatwierdza i uruchamia podgląd, **Cancel / Escape** odrzuca zmiany, a **Defaults** przywraca wartości domyślne w oknie. Zmiana ustawień jest jednym krokiem cofania. Zapisz mapę, aby używał ich również osobny Runtime. Starsze mapy zachowują dotychczasowy widok i ustawienia. Podczas Play opcje są nieaktywne; edytuj je po zatrzymaniu podglądu. Siatka pomocnicza jest dostępna wyłącznie podczas edycji mapy (G); podgląd Play i Runtime nie wyświetlają jej nawet na starszych mapach z włączonym `ShowGrid`.

## Środowisko, triggery, prefabrykaty i kolizje

Przycisk **World** u góry oraz **hamburger → World tools** otwierają panel funkcji świata. Dostęp pozostaje też przez **Launch options → Apply & world tools**. Zmiany są zapisywane z mapą i obsługują Ctrl+Z / Ctrl+Y. Okno blokuje skróty i edycję pod spodem; Escape je zamyka.

| Zakładka | Możliwości |
| --- | --- |
| **Environment** | Oświetlenie, miękkie cienie pod obiektami, zanikanie przeszkód zasłaniających gracza, docelowa przezroczystość, godzina początkowa, cykl dnia i nocy, długość dnia, dźwięk i głośność. |
| **Lights** | Dodawanie świateł w środku widoku; pozycja X/Z, wysokość, promień, intensywność, RGB i włączanie. Na klatkę wybieranych jest do 16 najbliższych świateł. Ogniska i pochodnie świecą automatycznie. |
| **Sounds** | Emitery zapętlonego dźwięku: pozycja, promień, głośność i wybór klipu. **Listen** odtwarza próbkę. WAV/OGG/MP3 z `Editor/audio/` są dostępne obok generowanych placeholderów. Ogniska, pochodnie, woda oraz kroki mają dźwięki automatyczne. |
| **Triggers** | **Draw area on map**: przeciągnij LMB, aby narysować prostokąt. Ustaw nazwę, rozmiar, Enabled, Once oraz akcję. |
| **Prefabs** | Zapis grupy zaznaczonych aktorów do `Editor/prefabs/` i wielokrotne wstawianie na mapę. |
| **Collision** | Wybór obiektu, automatyczny okrąg, własny okrąg, obracany prostokąt lub wielokąt, flagi drzwi i zanikania. |

Akcje triggerów to **Message**, **Dialogue**, **ToggleDoor**, **SpawnMonster**, **Teleport** i **ChangeMap**. Kliknięcie przycisku Action przełącza akcję. Dla rozmowy lub drzwi wybierz Target; obiekt jest drzwiami po zaznaczeniu **Door** w Collision. SpawnMonster używa domyślnych statystyk lub szablonu wybranego spośród potworów mapy. Teleport i spawn mają współrzędne docelowe. ChangeMap wskazuje względny plik JSON z `Runtime/Maps/` i punkt wejścia na docelowej mapie. Zmiana mapy przenosi HP i EQ oraz zachowuje stan odwiedzonych map. Once działa raz w sesji i jest uwzględniane w zapisie postępu; pozostałe obszary wykonują akcję przy ponownym wejściu, nie w każdej klatce.

**Shift+klik** dodaje lub usuwa aktora z grupy. Przeciąganie zaznaczonego obiektu przesuwa całą grupę; Ctrl+D duplikuje ją, Delete usuwa, a Ctrl+Z cofa operację. W Prefabs wybierz **Save selected group**, nadaj nazwę, następnie **Place prefab on map** i klikaj mapę. Dialogi, statystyki, zawartość skrzyń i kształty kolizji pozostają zachowane, a identyfikatory są nowe. Escape kończy wstawianie.

W narzędziu **Select** przeciągnij prostokąt od pustego miejsca, aby zaznaczyć obiekty środkiem ich grafik. **Ctrl+przeciągnięcie** rozpoczyna prostokąt także nad obiektem, **Shift** dodaje wyniki do obecnej grupy. Panel grupy przesuwa środek X/Z, zmienia względne rozmiary wszystkich grafik o ±10% oraz ustawia kolizję całej grupy. Zachowuje proporcje rozmiarów i odstępy przy przesuwaniu do granicy mapy. Każda operacja jest jednym krokiem cofania.

W **Collision → Polygon → Draw polygon on map** klikaj narożniki, Enter zatwierdza, Backspace usuwa ostatni punkt, Escape anuluje. Wielokąt ma 3–32 wierzchołki, może być wklęsły, ale jego boki nie mogą się przecinać. Kolizje działają dla gracza, tras potworów i ataków; ruch nie przeskakuje przez cienkie ściany. Otwarte drzwi przepuszczają postacie. **Automatic** przywraca dawny okrąg zależny od wysokości obiektu.

Zanikanie działa w Play/Runtime, gdy obiekt zasłania ciało gracza; przezroczystość zmienia się płynnie i wraca po odsłonięciu. Oświetlenie obejmuje teren, grafiki i wodę. **World → Environment → Contact shadows** włącza miękkie, spłaszczone cienie pod obiektami i postaciami. Ich rozmiar wynika z szerokości i wysokości widocznej grafiki po przycięciu przezroczystych marginesów; skaluje się razem z obiektem, niezależnie od kolizji. Cienie dopasowują się do wzniesień i zagłębień, działają w obu rzutach, w edytorze, Play i Runtime. Zmiana godziny w edytorze daje podgląd, a w grze czas biegnie po włączeniu cyklu dnia i nocy.

**F6 w Play** zapisuje postęp, **F9** go wczytuje: pozycję, HP, EQ, skrzynie i drzwi, stan potworów, wykonane triggery i porę dnia, także na odwiedzonych mapach. Podgląd używa osobnego pliku w `Editor/obj/progress/`; nie nadpisuje mapy roboczej ani zapisu Runtime. **Ctrl+S** nadal zapisuje mapę, a nie stan gry.

Teren ma siatki GPU w fragmentach **16 × 16** kafelków. Malowanie aktualizuje blendowanie bez przebudowy geometrii, terraformowanie odświeża tylko zmienione fragmenty, a przesuwanie widoku usuwa nieużywane siatki z cache. Cofanie i wczytanie mapy odświeżają cache automatycznie.

## Grafiki postaci i podłoża

- `Editor/characters/` — pojedyncze PNG postaci, najlepiej z przezroczystym tłem. Wybór w narzędziu Player.
- `Editor/terrain/` — kwadratowe, najlepiej powtarzalne PNG podłoża. Wybór w narzędziu Ground.

Po dodaniu materiałów uruchom aplikację ponownie. Na razie grafiki postaci są statyczne: nie ma obsługi arkuszy animacji. Bez grafiki postaci wyświetlany jest niebieski znacznik. Wbudowane materiały podłoża to grass, dirt, sand i stone. Woda ma własne narzędzie Water i zachowuje wybrany materiał dna.

Wszystkie tekstury w `Editor/terrain/` mają wspólny styl inspirowany **Don’t Starve**: ciemne, poszarpane kontury tuszem, widoczne kreskowanie i przygaszone kolory. Dotyczy to również bazowych materiałów oraz `cobblestone.png`. Podmiana zachowuje nazwy plików i identyfikatory materiałów, więc istniejące mapy otrzymują ten wygląd po ponownym uruchomieniu aplikacji.

| Plik | Materiał |
| --- | --- |
| [grass.png](terrain/grass.png) | Zielona trawa z ciemnymi konturami źdźbeł i kreskowaniem. |
| [grass_calm.png](terrain/grass_calm.png) | Spokojniejsza, oliwkowa trawa z rzadszymi kępkami. |
| [grass_dry.png](terrain/grass_dry.png) | Sucha, słomkowa trawa z poszarpaną kreską. |
| [grass_forest.png](terrain/grass_forest.png) | Ciemniejsza, chłodna trawa leśna z gęstszymi kępkami. |
| [grass_meadow.png](terrain/grass_meadow.png) | Jaśniejsza trawa łąkowa w przygaszonej zieleni. |
| [grass_flowers.png](terrain/grass_flowers.png) | Zielona trawa z nielicznymi drobnymi kremowymi i fioletowymi kwiatami. |
| [grass_sparse.png](terrain/grass_sparse.png) | Rzadkie kępki trawy na odsłoniętej ziemi. |
| [grass_withered.png](terrain/grass_withered.png) | Przywiędła, brązowo-oliwkowa trawa z pochylonymi źdźbłami. |
| [dirt.png](terrain/dirt.png) | Ubita ziemia z rysowanymi grudkami i drobnymi kamykami. |
| [sand.png](terrain/sand.png) | Suchy piasek z ciemnymi liniami zmarszczek. |
| [sand_wet.png](terrain/sand_wet.png) | Chłodniejszy, mokry piasek z kreskowanymi zagłębieniami. |
| [stone.png](terrain/stone.png) | Skaliste podłoże z wyraźnymi szczelinami i kreskowaniem. |
| [moss.png](terrain/moss.png) | Mech z nieregularnymi konturami drobnych poduszek. |
| [forest_floor.png](terrain/forest_floor.png) | Leśna ściółka z obrysami liści i igliwia. |
| [mud.png](terrain/mud.png) | Ciemne błoto z rysowanymi grudkami i zagłębieniami. |
| [gravel.png](terrain/gravel.png) | Drobny żwir z ciemnymi konturami kamyków. |
| [cobblestone.png](terrain/cobblestone.png) | Nieregularny kamienny bruk z mocnymi konturami tuszem. |
| [city_sidewalk.png](terrain/city_sidewalk.png) | Kwadratowe płyty chodnikowe z rysowanymi spoinami i pęknięciami. |
| [city_granite_setts.png](terrain/city_granite_setts.png) | Drobna kostka granitowa w lekko nierównych rzędach. |
| [city_fan_paving.png](terrain/city_fan_paving.png) | Małe kamienie brukowe układane w powtarzające się wachlarze. |
| [city_brick_herringbone.png](terrain/city_brick_herringbone.png) | Ceglany bruk w jodełkę z ciemnymi konturami. |
| [city_cobbles_round.png](terrain/city_cobbles_round.png) | Kocie łby z drobnych, zaokrąglonych kamieni. |
| [city_asphalt_cracked.png](terrain/city_asphalt_cracked.png) | Szary asfalt z nieregularnymi pęknięciami i kreskowaniem. |
| [snow.png](terrain/snow.png) | Jasny śnieg z rysowanymi krawędziami zasp i kreskowaniem. |
| [stone_tiles.png](terrain/stone_tiles.png) | Kamienne płyty, identyczne z zaakceptowanym wariantem `stone_tiles_ink.png`. |
| [wood_planks_ink.png](terrain/wood_planks_ink.png) | Krzywe, sękate deski z konturami i kreskowaniem. |
| [stone_tiles_ink.png](terrain/stone_tiles_ink.png) | Ponure płyty kamienne z rysowanymi pęknięciami. |
| [brick_paving_ink.png](terrain/brick_paving_ink.png) | Ceglany bruk z nieregularną kreską tuszu. |
| [cracked_earth_ink.png](terrain/cracked_earth_ink.png) | Sucha ziemia z poszarpanymi szczelinami i kreskowaniem. |

Każda tekstura ma 1254 × 1254 px. Pomocniczy [water.png](terrain/water.png) również ma kreskowane fale w tym stylu; powierzchnia wody z narzędzia **Water** jest nadal rysowana proceduralnie.

Prompty ujednolicenia całej biblioteki i wspólny wzorzec stylu zapisano w [terrain/GENERATION_ALL_INK.md](terrain/GENERATION_ALL_INK.md). Prompty wcześniejszych wariantów są w [terrain/GENERATION_INK.md](terrain/GENERATION_INK.md); [terrain/GENERATION.md](terrain/GENERATION.md) dokumentuje historyczny pierwszy zestaw.

Pięć dodatkowych wariantów trawy ma ten sam styl konturów i kreskowania. Są osobnymi materiałami w **Ground** po ponownym uruchomieniu aplikacji. Ich prompty zapisano w [terrain/GENERATION_GRASS_VARIANTS.md](terrain/GENERATION_GRASS_VARIANTS.md).

Sześć miejskich materiałów z przedrostkiem `city_` zachowuje ten sam styl ciemnego tuszu i kreskowania. Są dostępne w **Ground** po ponownym uruchomieniu aplikacji. Prompty i wzorce zapisano w [terrain/GENERATION_CITY.md](terrain/GENERATION_CITY.md).

Podłoże korzysta z kolorów PNG bez dodatkowej korekcji nasycenia, jasności ani barwy. Na mapie wygląd zmieniają blendowanie sąsiednich materiałów, cieniowanie zboczy oraz ustawienia **World → Environment** i światła. Aby porównać kolory z plikiem, wyłącz **Lighting** i **Shadows** oraz sprawdź płaski fragment jednego materiału. Zmiana PNG wymaga ponownego uruchomienia aplikacji, ale nie rekompilacji.

W widokach 3/4 i izometrycznym górna część tekstury PNG jest skierowana w głąb widoku, dzięki czemu źdźbła trawy mają właściwy kierunek. **Wolna kamera zachowuje ułożenie tekstur z ostatniego widoku 3/4 lub izometrycznego**: obracanie, orbitowanie, przesuwanie i zoom nie obracają ani nie przesuwają wzoru po terenie. Tekstury, pomalowane obszary i ich przejścia pozostają zakotwiczone na mapie. Play i Runtime zachowują orientację wybranego rzutu; reguła działa także na istniejących mapach.

**Hamburger w prawym górnym rogu → Nowa mapa** otwiera okno wyboru typu, szerokości X i głębokości Z. Ten sam formularz otwierają **+ New map** oraz **Ctrl+N**. Wpisz rozmiary, użyj przycisków +/- lub gotowych wartości, w tym 2048 × 2048. Każdy bok może mieć 4–2048 kafelków; mapa może być prostokątna. **Utwórz / Enter** tworzy mapę, Tab przełącza pole, **Anuluj / Escape** zamyka okno. Przed zastąpieniem niezapisanej mapy pojawia się wybór Save / Discard / Cancel. Utworzenie mapy wraz z typem i oświetleniem można cofnąć i ponowić.

| Typ | Początkowe podłoże i oświetlenie |
| --- | --- |
| **Otwarty świat** | Trawa; oświetlenie i opcjonalny cykl dnia i nocy według ustawień Environment. |
| **Budynek** | Kamienne płyty; stały mrok, rozświetlany lokalnymi światłami. |
| **Jaskinia** | Skaliste podłoże; stały mrok, rozświetlany lokalnymi światłami. |

Typ zapisuje się z mapą i obowiązuje w edytorze, Play oraz Runtime. W budynku i jaskini pora dnia nie rozjaśnia otoczenia, także po wczytaniu mapy lub postępu; w Environment kontrolki wyłączania Lighting, cyklu i godziny są zablokowane. Światła dodaje się w **World → Lights**. Starsze mapy bez typu są otwartym światem i zachowują dotychczasowe oświetlenie.

Hamburger zawiera też **Launch options** oraz **World tools**. Escape lub kliknięcie poza menu zamyka je; kliknięcie zamykające nie uruchamia narzędzia pod menu. **Save / Ctrl+S** nadal zapisuje mapę w bieżącym pliku; aby pracować na osobnym pliku, uruchom edytor z argumentem `--map Runtime/Maps/nazwa.json`.

Tekstury podłoża płynnie blendują się między sąsiednimi kafelkami, także przy zbiegu czterech materiałów i na zboczach. Dotyczy to zarówno wbudowanych materiałów, jak i własnych PNG. Przejścia aktualizują się podczas malowania oraz cofania i działają identycznie w obu rzutach, Play oraz Runtime; istniejące mapy korzystają z nich automatycznie.

## Drogi i biomy

**Ground / 3 → Roads** rysuje drogę jako krzywą. Wybierz materiał w bibliotece, ustaw **Width** i **Soft edge**, a następnie klikaj punkty na mapie. Zielony podgląd pokazuje przebieg i szerokość. Przeciąganie punktu poprawia jego położenie, **Backspace / Remove point** usuwa ostatni punkt, **Apply / Enter** zatwierdza całą drogę, a **Escape** porzuca szkic. Droga może mieć 2–128 punktów, szerokość 0,5–64 m i miękki brzeg do połowy szerokości.

**Clear vegetation** usuwa zwykłe obiekty z kategorii vegetation w obrębie zatwierdzanej drogi. Ctrl+Z przywraca je razem z cofnięciem drogi. **Road list** otwiera zapisane krzywe: wybierz drogę, zmień jej punkty lub parametry i ponownie zatwierdź. Przycisk **Name** zmienia nazwę; **Delete road** usuwa otwartą drogę. Drogi są dostępne w wyszukiwarce **Ctrl+F → Road** i widoczne na minimapie.

Droga dopasowuje się do wysokości podłoża, także po dalszym rzeźbieniu i erozji. Miękkie brzegi mieszają jej materiał z podłożem bez zastępowania pomalowanych kafelków. Zapis obejmuje punkty, materiał, szerokość i miękkość; ten sam wygląd obowiązuje w edytorze, Play oraz Runtime. Starsze mapy wczytują się bez dróg. Utworzenie, poprawienie lub usunięcie drogi jest jednym krokiem cofania.

**Ground / 3 → Biomes** udostępnia **Forest**, **Meadow** i **Rocky**. Pędzel miesza materiały w nieregularne płaty i rozmieszcza dostępne grafiki drzew, krzewów, trawy lub kamieni. Każdy biom ma osobno pamiętane podczas pracy ustawienia: promień 1–32 m, gęstość obiektów, minimalny odstęp i losową skalę. Paleta pokazuje mieszankę podłoża oraz liczbę pasujących grafik z biblioteki; gdy takich grafik nie ma, można nadal malować teren.

Przytrzymaj LMB i przesuwaj mysz, aby malować biomem. Gęstość zależy od powierzchni, a kolejne odciski od przebytej odległości; nieruchomy kursor nie dokłada obiektów co klatkę. Pędzel omija głęboką wodę, pozostawia miejsce wokół startu gracza i dróg oraz respektuje odstępy od istniejących aktorów. Na stromych zboczach używa kamiennego podłoża. Całe pociągnięcie, wraz z materiałami i nowymi obiektami, cofa się jednym Ctrl+Z i zapisuje w mapie.

## Fragmenty mapy

**Select → Copy map area**, **hamburger → Map fragments** lub **Ctrl+Shift+C** otwierają narzędzie kopiowania obszarów. Przeciągnij prostokąt po terenie; zaznaczenie obejmuje pełne kafelki i po zwolnieniu myszy automatycznie trafia do schowka. **Copy / Ctrl+C** ponownie odczytuje ten obszar, np. po dalszych zmianach. Kopia jest niezależna od mapy źródłowej i sama nie zmienia mapy ani historii cofania.

**Paste / Ctrl+V** pokazuje obrys obszaru, siatkę wysokości i obiekty w miejscu kursora. Kliknięcie wkleja całość, a kolejne kliknięcia stawiają niezależne kopie. Przytrzymanie myszy nie powtarza operacji. Czerwony podgląd oznacza np. wyjście poza granice mapy lub przekroczenie zakresu wysokości. Fragment musi zmieścić się w całości. Escape opuszcza wklejanie, kolejny Escape zamyka narzędzie; schowek pozostaje dostępny także po utworzeniu lub wczytaniu innej mapy. Punkt startowy gracza i globalne ustawienia świata pozostają ustawieniami mapy docelowej.

Fragment obejmuje materiały, wszystkie wierzchołki wysokości, maski wody, zwykłe obiekty z kolizjami, NPC z dialogami, skrzynie z zawartością, potwory ze statystykami, drogi, światła, dźwięki i triggery. Obiekty oraz elementy świata są wybierane po punkcie ustawienia; odcinki dróg przecinające zaznaczenie są przycinane według przebiegu osi. Kopie aktorów, dróg, elementów świata i przedmiotów w skrzyniach otrzymują nowe identyfikatory. Całe wklejenie — wraz z terenem, wodą i zawartością — cofa się jednym **Ctrl+Z**, a **Ctrl+Y** odtwarza ten sam układ i identyfikatory.

**Paste options** pozwala osobno włączać teren z wysokościami i wodą, aktorów, drogi oraz światła/dźwięki/triggery. Teren zastępuje wskazane kafelki i wierzchołki. Pozostałe elementy domyślnie są dodawane; **Replace destination contents** zastępuje zawartość wybranych kategorii w prostokącie. Odcinki istniejących dróg poza nim zostają zachowane. Można też ustawić dodatkowe przesunięcie wysokości co 0,25 m.

Mapa ma jeden poziom wody. Domyślne **Align copied water to this map's water level** przesuwa kopiowany teren z wodą względem tego poziomu, zachowując głębokość zbiornika przy zerowym dodatkowym przesunięciu. Nie zmienia poziomu wody w pozostałej części mapy. Wyłączenie tej opcji zachowuje bezwzględne wysokości źródłowe.

Triggery odnoszące się do kopiowanych NPC i drzwi wskazują nowe kopie. Lokalne teleporty i punkty spawnu przesuwają się razem z fragmentem; przejścia do innych map zachowują plik oraz punkt wejścia. Odwołania do elementów spoza obszaru pozostają aktywne, jeśli cel istnieje na mapie docelowej. Brakujący cel wyłącza skopiowany trigger, a komunikat po wklejeniu podaje ich liczbę; cel można poprawić w **World → Triggers**.

**Save fragment** nadaje nazwę i zapisuje obszar w `Editor/fragments/`. **Fragment library** otwiera zapisane polany, obozy lub fragmenty wiosek; **Place** uruchamia ich podgląd, a **Refresh** odświeża listę. Zapis fragmentu nie zapisuje mapy roboczej. Biblioteka działa między uruchomieniami i między mapami; zmienione lub brakujące PNG zachowują te same względne odwołania co zwykłe mapy.

## Terraformowanie i woda

**Sculpt / 6** edytuje wysokości wierzchołków powierzchni. Wybierz **Raise**, **Lower**, **Smooth** lub **Flatten** po lewej. Panel po prawej ustawia promień pędzla, siłę oraz docelową wysokość dla Flatten. Przytrzymaj LMB nad mapą; Shift zamienia Raise z Lower. Krawędź pędzla ma łagodne przejście. Wysokości wierzchołków są zaokrąglane do 0,25 m, także przy wygładzaniu, wyrównywaniu i wczytywaniu map. Siła pędzla kumuluje się między klatkami, więc słaby pędzel nadal działa. Zakres wysokości wynosi od −32 do 64 metrów. Obrys pędzla, trafianie kursorem, siatka oraz pozycje obiektów i NPC dopasowują się do podłoża.

**Sculpt → Terrain stamps** udostępnia gotowe kształty **Hill**, **Mountain**, **Ridge** i **Crater**. Jeden LMB nakłada cały stempel; kolejne kliknięcia budują teren warstwami. Promień wynosi **1–64 m**, wysokość odcisku **0,25–32 m**, a **Shape variation** ustawia nieregularność. Kolejne odciski mają inny obrót i szum kształtu. **Shift+klik** odwraca wysokość, np. drąży zagłębienie zamiast góry. Przytrzymanie myszy nie powiela stempla; każde kliknięcie jest jednym krokiem cofania. **Back to sculpt brushes** wraca do dotychczasowego rzeźbienia.

**Sculpt → Terrain erosion** naturalizuje istniejące wzniesienia, np. po użyciu stempli. **Hydraulic erosion** symuluje krople spływające po zboczu, które żłobią kanały, przenoszą ziemię i odkładają ją niżej. **Talus / loose debris** osypuje strome fragmenty i tworzy łagodniejsze podnóża. Ustaw promień 1–64 m, siłę oraz liczbę kropli lub przebiegów osypywania, po czym kliknij obszar. Każde kliknięcie jest osobną operacją; przytrzymanie LMB jej nie powtarza. Obszar poza pędzlem pozostaje bez zmian, wysokości zachowują krok 0,25 m i zapisują się w mapie. Ctrl+Z przywraca teren sprzed całej symulacji, Ctrl+Y dokładnie odtwarza jej wynik.

**Water / 7** tworzy zbiorniki i rzeki. Aby wypełnić istniejące zagłębienie, wykop je narzędziem Sculpt, ustaw **Water level**, wybierz **Fill basin** i kliknij poniżej tego poziomu. Woda rozlewa się po połączonych częściach zagłębienia, zatrzymuje na suchych grzbietach i kończy dokładnie na przecięciu z terenem. Wypełnianie zachowuje wysokości i materiały dna. Powierzchnia ma animowane fale, odbicia otoczenia oraz przejrzystość zależną od głębokości. **Drain basin** opróżnia kliknięty zbiornik. **Water level** jest wspólny dla mapy i zmienia się co 0,25 m; podnoszenie poziomu rozszerza istniejące zbiorniki. Woda może sięgać krawędzi mapy, także podczas wypełniania i podnoszenia poziomu. Przycisk **Waves** przełącza fale.

**Water → Pędzel rzeki** pozwala określić bieg rzeki bez wcześniejszego kopania. Ustaw po prawej **Szerokość (m)** od **2 do 32 m** oraz **Głębokość (m)** od **0,25 do 8 m**, po czym przytrzymaj LMB i przeciągnij po mapie. Pędzel wykopie ciągłe koryto z łagodnymi brzegami i od razu doda wodę wzdłuż trasy, także przy szybkim ruchu myszy. Głębokość jest liczona od wspólnego **Poziomu wody**; wszystkie rzeki i zbiorniki na mapie mają tę samą wysokość tafli. Ponowne prowadzenie po tej samej trasie nie pogłębia koryta, a istniejące głębsze dno pozostaje zachowane. Całe pociągnięcie wraz z wodą cofa jeden **Ctrl+Z**; **Ctrl+Y** je odtwarza. Koryto i woda zapisują się przez **Ctrl+S** w dotychczasowym formacie mapy.

**Water → Kierunek nurtu** to osobny pędzel animacji istniejącej wody. Ustaw **Szerokość pędzla (m)** od **2 do 32 m** i **Prędkość nurtu (m/s)** od **0,2 do 4 m/s**, a następnie przeciągnij LMB zgodnie z kierunkiem przepływu. Fale i refleksy podążają za namalowaną trasą, także na zakrętach. Przeciągnięcie w przeciwną stronę odwraca lokalny nurt. Strzałki widoczne przy aktywnym trybie pokazują kierunki na powierzchni. Przełącz przycisk **Maluj kierunek** na **Usuń nurt: ON**, aby wymazać namalowany prąd i przywrócić zwykłą animację wody. Pędzel zachowuje koryto i zasięg zbiornika. Jedno **Ctrl+Z** cofa całe pociągnięcie, **Ctrl+Y** je odtwarza, a **Ctrl+S** zapisuje nurt z mapą. Działa on także w Play i Runtime; starsze mapy zachowują dotychczasową animację.

**Water → Wygląd wody...** otwiera ustawienia materiału. Wybierz paletę **Jezioro**, **Rzeka**, **Tropikalna** lub **Bagno**, albo ustaw własne RGB osobno dla płycizny i głębi. Przyciski RGB zmieniają wartość co 8; przytrzymanie **Shift** daje krok 1. **Przejrzystość (m)** określa, jak głęboko widać dno: większa wartość oznacza przejrzystszą wodę. Pozostałe pola ustawiają **Siłę odbić**, **Załamanie obrazu**, **Pianę przy brzegu** i **Wysokość fal (m)**.

Płytka woda odsłania więcej materiału dna, a głębsza stopniowo przyjmuje wybrany kolor głębi. Odbicia obiektów i terenu nad wodą oraz falujące załamanie obrazu dna działają w edytorze, Play i Runtime. Zmiany materiału są widoczne od razu, obsługują **Ctrl+Z / Ctrl+Y** i zapisują się z mapą przez **Zapisz mapę / Ctrl+S**. **Escape** zamyka ustawienia. Starsze mapy bez ustawień wyglądu otrzymują materiał domyślny.

Całe pociągnięcie pędzla, wypełnienie lub opróżnienie zbiornika jest jednym krokiem cofania. W podglądzie i Runtime postać oraz kamera podążają za wysokością podłoża. Głęboka woda (ponad 0,55 m) i strome zbocza zatrzymują ruch; płytką wodę można przejść. Starsze mapy bez wysokości wczytują się jako płaskie; zapisane pola water pozostają obsługiwane.

## NPC i dialogi

NPC można zaznaczać, przeciągać, skalować, duplikować i usuwać jak obiekty. Jego właściwości zawierają **Name**, **Sprite** oraz **Dialogue**. Sprite przełącza bibliotekę na grafiki postaci; wybór aktualizuje zaznaczonego NPC oraz grafikę następnego NPC. Bez PNG wyświetla się złoty znacznik. Kolizja NPC działa także w podglądzie gry.

Przycisk **Dialogue** otwiera graficzne drzewko. Każdy NPC otrzymuje przykładową rozmowę, którą można dowolnie zmienić:

- **Start** wskazuje pierwszą kwestię NPC.
- **+ Player Reply** dodaje odpowiedź bohatera do zaznaczonej kwestii NPC. Można dodać wiele opcji.
- **+ NPC Reply** dodaje następną kwestię NPC. NPC może wypowiedzieć kilka kwestii po sobie. Jeśli zaznaczona kwestia ma już odpowiedzi bohatera, zostają przeniesione za nową kwestię z zachowaniem tekstów, identyfikatorów i dalszych gałęzi. Dodanie NPC Reply do istniejącej sekwencji wstawia nową kwestię przed dotychczasową kontynuacją.
- Zaznacz odpowiedź bohatera i wybierz **+ NPC Reply**, aby dodać reakcję NPC. Odpowiedzi bohatera dodaje się na końcu sekwencji NPC; przy wcześniejszych kwestiach przycisk Player Reply jest wyłączony.
- Zaznacz odpowiedź i wybierz **Redirect to NPC**, a następnie kliknij istniejącą kwestię NPC na drzewku. Możesz wrócić do wcześniejszych pytań lub skierować kilka odpowiedzi do tej samej kwestii. Przerywana strzałka pokazuje dodatkowe połączenie. Escape / **Cancel redirect** anuluje wybór celu; **End after this reply** usuwa przejście i kończy rozmowę po tej odpowiedzi. Zmiana celu zastępuje dotychczasowe przejście i usuwa dalszą gałąź, jeśli nie prowadzi już do niej żadna opcja; Ctrl+Z przywraca zmianę.
- **Edit text** otwiera pole tekstowe: wpisywanie lub wklejanie, Ctrl+A zaznacza całość, strzałki / Home / End lub kliknięcie przesuwają kursor, Backspace / Delete usuwa znaki, Enter dodaje nowy wiersz, Ctrl+Enter zatwierdza, Escape anuluje. Początkowy tekst jest zaznaczony; wpisanie nowego go zastępuje.
- **Delete branch / Delete** usuwa węzeł i odłączone części gałęzi. Powrót do wcześniejszej kwestii nie usuwa tej kwestii, a wspólne kwestie pozostają, jeśli są osiągalne inną drogą. Usunięcie celu przekierowania zmienia prowadzące do niego odpowiedzi w zakończenia rozmowy. Start oraz pierwsza kwestia NPC pozostają w drzewku.
- **Reply up / down** zmienia kolejność odpowiedzi. Węzeł bez dzieci kończy rozmowę: ostatnia odpowiedź bohatera zamyka okno, a ostatnią kwestię NPC zamyka Enter lub kliknięcie.
- Prawy/środkowy przycisk przeciąga drzewko, kółko przewija pionowo, Shift+kółko poziomo. **Home** przywraca początek, **Focus selected node** centruje zaznaczenie.

Ctrl+Z / Ctrl+Y cofa i ponawia zmiany drzewa i przekierowań. **Save / Ctrl+S** zapisuje całą mapę wraz z NPC i dialogami. **Close / Escape** wraca do mapy. Dialog może mieć do 256 węzłów, a pojedyncza kwestia do 2000 znaków. Format sprawdza połączenia i osiągalność węzłów. Kwestia NPC prowadzi do jednej kolejnej kwestii NPC albo do listy odpowiedzi bohatera. Pętle są dozwolone: każde przejście wymaga wyboru odpowiedzi lub potwierdzenia kontynuacji przez gracza. Starsze mapy i dotychczasowe drzewka nadal działają.

W **Play / F5** podejdź do NPC i naciśnij E. **Enter / kliknij: dalej** przechodzi do następnej kwestii NPC. Po sekwencji pojawiają się odpowiedzi bohatera, a ostatnią kwestię bez kontynuacji zamyka Enter lub kliknięcie. Podgląd używa tych samych dialogów i sterowania co Runtime. Escape podczas rozmowy zamyka rozmowę; następny Escape kończy podgląd. F5 kończy podgląd również podczas dialogu.

## Itemki, ekwipunek i skrzynie

**Chest / 8** dodaje skrzynię z grafiką `objects/props/containers/chest.png`. Można ją zaznaczać, przesuwać, skalować, nazywać, usuwać i duplikować. Wybór tej grafiki w bibliotece również włącza narzędzie Chest. Starszy obiekt z grafiką skrzyni można zamienić w skrzynię przyciskiem **Enable chest contents** we właściwościach.

**Items** otwiera siatkę zawartości skrzyni (10×6 pól). Przyciski **+** dodają wybrany przedmiot na pierwsze wolne miejsce, a **+ 5 placeholderów** dodaje po jednym z każdego rodzaju, jeśli jest miejsce. Przeciągaj przedmioty, aby ustawić ich pozycje. Przedmiot zajmuje wszystkie pola swojego prostokąta; upuszczenie na zajętych polach lub poza siatką zachowuje jego poprzednie położenie. Kliknij przedmiot i użyj **Delete / Usuń item**, aby usunąć go z zawartości. Ctrl+Z / Ctrl+Y cofa i ponawia zmiany; **Zamknij / Escape** wraca do mapy.

| Placeholder | Rozmiar | Kolor |
| --- | --- | --- |
| Mikstura | 1×1 | czerwony |
| Sztylet | 1×2 | niebieski |
| Miecz | 1×3 | zielony |
| Zbroja | 2×2 | żółty |
| Tarcza | 2×3 | fioletowy |

**Player → Starting inventory** otwiera ten sam edytor dla startowego ekwipunku bohatera (10×4 pola). Zapis mapy obejmuje zawartość obu rodzajów siatek. Duplikat skrzyni otrzymuje własne egzemplarze itemków.

Wspólne informacje i parametry bohatera są w [player.json](../Runtime/Characters/player.json): nazwa, opis, HP, obrażenia, prędkości chodu i biegu, zasięg oraz odstęp ataków i zasięg interakcji. Play odczytuje plik przy każdym uruchomieniu; po zmianie zatrzymaj i włącz podgląd ponownie. Punkt startowy, grafika, wysokość i początkowe EQ pozostają ustawieniami konkretnej mapy. [Instrukcja postaci](../Runtime/Characters/README.md) opisuje pola, a [Mechanics](../Runtime/Mechanics/README.md) wskazuje pliki z logiką gry.

W Play / Runtime **I** otwiera ekwipunek, **E** otwiera najbliższą skrzynię lub rozmowę z NPC w zasięgu 2,5 jednostki. Przeciąganie przenosi przedmioty między skrzynią a EQ; **Shift+klik** przenosi je na pierwsze pasujące miejsce drugiej siatki. Brak miejsca zachowuje item w źródłowej siatce. Otwarte EQ zatrzymuje ruch. **I / Escape / X** zamyka panel, F5 wraca z podglądu do edycji. Zabranie itemków w podglądzie nie zmienia zawartości mapy w edytorze; F6 zapisuje stan gry w osobnym pliku postępu, a F9 go wczytuje.

Starsze mapy wczytują się z pustym startowym EQ i bez skrzyń. Ta wersja nie zawiera skryptów ani systemu zadań.

## Monstery

**Monster / 9** dodaje przeciwnika. Zaznaczanie, przeciąganie, wysokość, kolizja, duplikowanie i usuwanie działają tak jak dla NPC. **Name** zmienia nazwę, **Sprite** wybiera PNG z `Editor/characters/` lub czerwony placeholder, a **Stats** otwiera ustawienia HP, obrażeń, prędkości, zasięgu wykrywania, zasięgu ataku oraz przerwy między atakami. Przyciski +/- zmieniają wartości, Ctrl+Z / Ctrl+Y cofają i ponawiają zmiany również w tym oknie. Escape / Enter / Zamknij wracają do mapy. Statystyki zapisują się w JSON; duplikat otrzymuje własny identyfikator.

W **Play / Runtime** potwory wykrywają pobliskiego gracza, ścigają go, obchodzą przeszkody i atakują w zwarciu. Głęboka woda i strome zbocza blokują ich ruch. Po utracie gracza lub zbyt dużym oddaleniu wracają na miejsce początkowe. **Spacja** atakuje najbliższego potwora w zasięgu; można ją przytrzymać. Gracz domyślnie zaczyna ze 100 HP, zadaje 10 obrażeń i atakuje co 0,4 s. Stały HUD zawiera tylko pasek życia bohatera bez liczb, nazw i podpowiedzi klawiszy; przeciwnicy nie mają pasków ani etykiet. Pokonany Monster znika i przestaje blokować ruch. Otwarte EQ i rozmowa zatrzymują walkę obu stron.

Po śmierci gracza **Enter** przywraca początkową mapę, przeciwników, HP oraz zawartość ekwipunków i skrzyń. **F5** nadal wraca do edycji. Ruch, obrażenia, śmierć oraz restart w podglądzie nie zmieniają mapy w edytorze. Starsze mapy wczytują się bez potworów.

## Testy

```sh
dotnet run --project Editor -- --editor-test
dotnet run --project Editor -- --river-test
dotnet run --project Editor -- --water-flow-test
dotnet run --project Editor -- --smoke-test
dotnet run --project Editor -- --interaction-test
dotnet run --project Editor -- --inventory-interaction-test
dotnet run --project Editor -- --water-interaction-test
dotnet run --project Editor -- --river-interaction-test
dotnet run --project Editor -- --water-flow-interaction-test
dotnet run --project Editor -- --dialogue-link-interaction-test
dotnet run --project Editor -- --dialogue-sequence-interaction-test
dotnet run --project Editor -- --monster-interaction-test
dotnet run --project Editor -- --launch-options-interaction-test
dotnet run --project Editor -- --new-map-interaction-test
dotnet run --project Editor -- --world-gpu-test
dotnet run --project Editor -- --world-interaction-test
dotnet run --project Editor -- --workflow-interaction-test
dotnet run --project Editor -- --landscape-interaction-test
dotnet run --project Editor -- --fragment-test
dotnet run --project Editor -- --fragment-interaction-test
```

`--editor-test` sprawdza dane i operacje edycji bez okna, w tym przedmioty, skrzynie, teren, wodę i mapy 2048 × 2048. Warianty `--smoke-test` oraz testy interakcji wymagają sesji graficznej. Pełny test interakcji zapisuje `Editor/editor-smoke.png` i obejmuje także itemki. Wariant `--inventory-interaction-test` sprawdza tylko skrzynie, przypisywanie, przeciąganie, zajęte pola, cofanie, zapis, otwieranie E/I, Shift+klik, zatrzymanie ruchu i niezależność podglądu. Mapy oraz zrzuty okien testowych są w `Editor/obj/`.

`--water-interaction-test` sprawdza otwieranie ustawień wody, palety, RGB, przejrzystość i odbicia, cofanie/ponawianie, blokowanie skrótów pod oknem, zapis/wczytanie oraz minimalny rozmiar okna. Zapisuje `Editor/obj/water-settings.png`.

`--river-test` sprawdza bez okna ciągłość koryta, szerokość i głębokość, ponowne malowanie bez pogłębiania, lokalny zasięg wody, zapis/wczytanie, cofanie/ponawianie, krawędzie mapy oraz mapę 2048 × 2048. `--river-interaction-test` sprawdza wybór pędzla i jego ustawień, rysowanie zakrętów, jedno cofnięcie całego pociągnięcia, blokowanie malowania pod oknem i podczas przesuwania kamery, minimapę, zapis/wczytanie oraz niezależność Play. Zapisuje `Editor/obj/river-brush-editor.png` i `Editor/obj/river-brush-play.png`.

`--water-flow-test` działa bez okna i sprawdza lokalne kierunki i zakręty, prędkość, interpolację, miękkie krawędzie, wymazywanie, ograniczenie do istniejącej wody, walidację, cofanie, zapis, starsze mapy oraz niezależne kopie. `--water-flow-interaction-test` sprawdza wybór trybu, szerokość i prędkość, rysowanie oraz odwracanie nurtu, gumkę, jedno cofnięcie całego pociągnięcia, ochronę suchego terenu, obsługę okien i minimapy, zapis/wczytanie, Play oraz minimalny rozmiar okna. Zapisuje `Editor/obj/water-flow-editor.png`, `water-flow-play.png` i `water-flow-minimum.png`.

`--dialogue-link-interaction-test` sprawdza tworzenie i zmianę przekierowania, cykliczny układ drzewka, anulowanie, usuwanie, cofanie, zapis/wczytanie oraz wielokrotne powroty w podglądzie gry. Pełny test interakcji również obejmuje te sprawdzenia.

`--dialogue-sequence-interaction-test` sprawdza oba rodzaje odpowiedzi, zachowanie istniejących opcji przy dodawaniu kwestii NPC, cofanie, zapis/wczytanie oraz kontynuację przez Enter i kliknięcie w podglądzie. Pełny test interakcji również go uruchamia; testy danych i układu sekwencji są częścią `--editor-test`.

`--monster-interaction-test` sprawdza dodawanie, zaznaczanie, przeciąganie, nazwę, grafikę, wszystkie statystyki, cofanie, duplikowanie, usuwanie, zapis/wczytanie, małe okno, pościg, obrażenia, pauzę w panelach, Spację, śmierć i restart przez Enter. Pełny test interakcji także go uruchamia. Testy danych i logiki walki są częścią `--editor-test`.

`--launch-options-interaction-test` sprawdza przełącznik rzutów, hamburger i modal, blokowanie skrótów w oknie, anulowanie, zatwierdzanie, cofanie, zapis/wczytanie, zaznaczanie i przeciąganie w izometrii, ruch i zoom w Play oraz minimalny rozmiar okna. Pełny test interakcji także go uruchamia. Zrzuty zapisuje w `Editor/obj/launch-options.png` i `Editor/obj/isometric-editor.png`.

`--new-map-interaction-test` sprawdza pozycję Nowa mapa w hamburgerze, konfigurację trzech typów i prostokątnych rozmiarów, minimalne okno, anulowanie, walidację, potwierdzenie niezapisanej pracy, cofanie/ponawianie, zapis/wczytanie i stały mrok w budynku ze światłem. Zrzuty są w `Editor/obj/new-map-menu.png`, `new-map-building.png` i `new-map-building-play.png`. Testy danych typów są częścią `--editor-test` i `Runtime --world-test`; `--world-gpu-test` w edytorze lub Runtime sprawdza również ciemne piksele i lokalne światła w budynku i jaskini.

`--world-interaction-test` sprawdza zakładki World, ustawienia środowiska, światła i dźwięki, rysowanie obszarów i wielokątów, drzwi, zaznaczanie i duplikowanie grup, bibliotekę prefabrykatów, zapis mapy oraz F6/F9 w Play. Korzysta z osobnej biblioteki w `Editor/obj/` i zapisuje `Editor/obj/world-tools.png`. Pełny test interakcji uruchamia również te sprawdzenia i testy GPU światła, cieni, zanikania oraz cache terenu.

`--workflow-interaction-test` sprawdza wolną kamerę i niezależność Play, obie wyszukiwarki, prostokąt zaznaczenia, wspólne właściwości grupy, pędzel obiektów, stemple, minimapę i odzyskiwanie automatycznej kopii z potwierdzeniem niezapisanej pracy. Sprawdza też minimalne okno i zapisuje `Editor/obj/workflow-tools.png` oraz `Editor/obj/workflow-tools-small.png`. Używa osobnych kopii w `Editor/obj/test-autosaves/` i nie zmienia ustawień użytkownika. Testy danych tych narzędzi są częścią `--editor-test`.

`--landscape-interaction-test` sprawdza rysowanie, przeciąganie punktów, parametry, nazwy, wyszukiwanie, usuwanie i cofanie dróg, usuwanie roślinności, zapis i Play, malowanie biomów z osobnymi ustawieniami, cofanie całych pociągnięć, erozję hydrauliczną i osypywanie, minimapę oraz małe okno. Zrzuty są w `Editor/obj/landscape-road-preview.png`, `landscape-road.png`, `landscape-biome.png`, `landscape-erosion.png` i `landscape-small.png`. Testy danych obejmują także granice mapy, zachowanie ziemi przed zaokrągleniem wysokości i powtarzalność symulacji. Test GPU blendowania sprawdza miękkie brzegi i krzywe dróg, oba rzuty, zbocza, zmianę szerokości, cofanie oraz ograniczony cache maski dla 2048 × 2048.

`--fragment-test` sprawdza kopiowanie kafelków i granicznych wierzchołków, wodę i starsze materiały water, przycinanie i zachowanie dróg, dialogi i skrzynie, nowe identyfikatory, lokalne/zewnętrzne połączenia triggerów, niezależność kopii, wybór kategorii, zastępowanie zawartości, bibliotekę, limity, brak częściowych zmian po błędzie oraz cofanie/ponawianie. Te same sprawdzenia należą do `--editor-test`. `--fragment-interaction-test` testuje zaznaczanie, podgląd, wielokrotne wklejanie, opcje, bibliotekę, przenoszenie między mapami, minimapę, zapis, Play i minimalne okno. Używa osobnej biblioteki w `Editor/obj/` i zapisuje `map-fragment-selection.png`, `map-fragment-preview.png`, `map-fragment-library.png` i `map-fragment-small.png` w tym katalogu. Pełny test interakcji również obejmuje fragmenty mapy.
