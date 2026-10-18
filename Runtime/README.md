# Runtime

Gra testowa odczytuje mapę stworzoną w edytorze i korzysta z tego samego renderera 2.5D. Obiekty i NPC są grafikami częściowo zwróconymi ku kamerze; na podłożu jest widoczna siatka. Podejście do NPC wyświetla podpowiedź rozmowy, a E otwiera jego dialog.

Obiekty i postać dopasowują się do poziomego kierunku kamery, a ich nachylenie wynosi połowę kąta patrzenia w dół (maksymalnie 30°). Podstawa pozostaje zakotwiczona na wysokości terenu. To nasze ustawienie częściowego billboardingu inspirowane opisem twórcy Don't Starve, a nie odtworzenie nieudokumentowanych parametrów jego silnika. Podłoże jest siatką trójkątów o wysokościach edytowanych pędzlem Sculpt. Renderer, zaznaczanie i obrys w edytorze używają tej samej geometrii.

Woda jest osobną powierzchnią 3D z animowanymi falami, przezroczystością i cieniowaniem. Wypełnia połączone zagłębienie do wybranego poziomu, a brzeg wyznacza przecięcie z terenem. Dno zachowuje wysokości i materiał. Wysokości wierzchołków terenu są zaokrąglane do 0,25 m. Postać porusza się na wysokości podłoża, a kamera podąża również pionowo. Głębokość ponad 0,55 m oraz strome zbocza blokują ruch.

Przezroczyste piksele grafik obiektów i postaci nie zapisują głębokości. Woda pozostaje widoczna przez puste fragmenty PNG, a widoczne części obiektu zachowują prawidłowe zasłanianie i zanurzenie.

Z głównego katalogu repozytorium:

```sh
dotnet run --project Runtime
```

Domyślnie wczytuje `Runtime/Maps/world.json`. Jeśli plik jeszcze nie istnieje, pokazuje przykładową scenę. Można wskazać inny plik:

```sh
dotnet run --project Runtime -- --map Runtime/Maps/test.json
```

Materiały podłoża płynnie blendują się na granicach kafelków oraz przy zbiegu czterech tekstur. Przejścia działają na zboczach i w obu rzutach kamery, również z własnymi PNG z `Editor/terrain/`. Malowanie i cofanie w edytorze od razu odświeżają efekt; format mapy pozostaje zgodny ze starszymi zapisami.

## Sterowanie

- WASD lub strzałki — chodzenie.
- Lewy Shift — bieganie.
- Spacja — atak najbliższego Monstera w zasięgu; można przytrzymać.
- Enter po śmierci — restart bieżącej mapy.
- F6 — zapis postępu gry; F9 — wczytanie ostatniego zapisu dla mapy startowej.
- Kółko myszy / + / - — płynny zoom.
- R — początkowy zoom zapisany w opcjach mapy (domyślnie odległość 30 m).
- E — rozmowa z najbliższym NPC lub otwarcie najbliższej skrzyni w odległości do 2,5 jednostki.
- I — otwarcie / zamknięcie ekwipunku.
- W ekwipunku: przeciąganie — układanie lub przenoszenie itemków między siatkami; Shift+klik — przeniesienie do drugiej siatki na pierwsze wolne miejsce.
- Podczas rozmowy: kliknięcie odpowiedzi, 1–9 albo strzałki góra/dół i Enter — wybór odpowiedzi. Kółko przewija długie kwestie lub listę odpowiedzi pod kursorem.
- Enter lub kliknięcie „dalej” — następna kwestia NPC, jeśli NPC mówi kilka kwestii po sobie. Przy ostatniej kwestii bez dalszych opcji kończy rozmowę. Odpowiedź bohatera bez dalszej kwestii kończy rozmowę od razu.
- Escape — zamknięcie rozmowy lub ekwipunku; poza tym wyjście z gry. Przycisk X także zamyka ekwipunek.

Rozmowa zatrzymuje ruch gracza. Każde nowe zagadanie zaczyna się od Start. Drzewko oraz nazwa, grafika, wysokość i kolizja NPC są ustawiane w edytorze i zapisane w mapie.

Odpowiedź może kierować do wcześniejszej kwestii NPC, np. „Wróć do pytań” ponownie pokazuje główne opcje rozmowy. Kilka odpowiedzi może prowadzić do tego samego NPC. Pętle przechodzą po jednym kroku przy wyborze gracza; Escape nadal zamyka rozmowę. Cele odpowiedzi wybiera się w edytorze przez **Redirect to NPC**.

NPC może wypowiedzieć kilka kwestii po sobie. Kolejne kwestie przechodzą po jednej na Enter lub kliknięcie „dalej”, a odpowiedzi gracza pojawiają się po zakończeniu tej sekwencji. Klawisze 1–9 działają przy odpowiedziach bohatera. W edytorze **Player Reply** dodaje opcję gracza, **NPC Reply** dodaje kolejną kwestię NPC; istniejące opcje pozostają zachowane za dodaną kwestią.

Punkt startowy, wygląd postaci, rozmiary obiektów i kolizje ustawia się w edytorze. Nie jest to jeszcze pełna gra RPG: nie ma mechanik przetrwania.

Menu hamburgerowe **Launch options** w edytorze zapisuje w mapie rzut **Perspective** lub **Isometric**, początkową odległość kamery, widoczność siatki oraz limit FPS. Runtime korzysta z tych samych ustawień co podgląd Play. Perspektywa zachowuje dotychczasową kamerę; izometria używa projekcji ortograficznej, kierunku 45° i stałego nachylenia około 35,26°. Sterowanie podąża za kierunkiem wybranego widoku. Starsze mapy domyślnie korzystają z perspektywy, odległości 30 m, widocznej siatki i 60 FPS. Argumenty `--close` / `--far` nadal pozwalają nadpisać startowy zoom.

## Ekwipunek i skrzynie

Ekwipunek bohatera ma 10×4 pola, a skrzynie 10×6. Pięć placeholderów zajmuje różne prostokąty: czerwona mikstura 1×1, niebieski sztylet 1×2, zielony miecz 1×3, żółta zbroja 2×2 i fioletowa tarcza 2×3. Item musi zmieścić się całym prostokątem na wolnych polach. Nieudane przeniesienie, zajęte miejsce i upuszczenie poza siatką pozostawiają go na poprzednim miejscu.

Podejdź do skrzyni i naciśnij E. Jej zawartość jest po lewej, EQ bohatera po prawej. Przenoszenie działa w obie strony. Otwarte okno zatrzymuje ruch postaci. Zawartość skrzyń i startowe EQ ustawia się w edytorze. Zabrane lub odłożone przedmioty pozostają w bieżącej sesji; F6 zapisuje je w osobnym pliku postępu, a F9 go przywraca.

## Monstery i walka

Potwory umieszcza się w edytorze narzędziem **Monster / 9**. Mają nazwę, PNG lub czerwony placeholder oraz własne HP, obrażenia, prędkość, zasięg wykrywania, zasięg ataku i przerwę między atakami. Domyślnie: 30 HP, 10 obrażeń, 2,4 m/s, wykrywanie do 6 m, zasięg ataku 1 m poza promieniem kolizji i atak co 1 s. Wykryty gracz jest ścigany; przeszkody uruchamiają ograniczone wyszukiwanie drogi. Woda ponad 0,55 m, strome zbocza i obiekty z kolizją blokują ruch. Potwór wraca do miejsca startowego, jeśli straci gracza lub oddali się od startu o ponad dwukrotność zasięgu wykrywania.

Bohater ma 100 HP i zadaje 10 obrażeń najbliższemu przeciwnikowi do 1,8 m co 0,4 s. **Spacja** wykonuje atak, przytrzymanie powtarza go. Przeszkody blokują ciosy obu stron. Paski życia oraz krótkie podświetlenie pokazują obrażenia. Potwór z 0 HP znika i przestaje blokować drogę. Dialog i ekwipunek pauzują również przeciwników i ataki.

Przy 0 HP bohater przestaje się poruszać i atakować. **Enter** przywraca początkową mapę, życie wszystkich aktorów oraz startową zawartość EQ i skrzyń. Zmiany walki pozostają w sesji i nie zapisują się do pliku mapy.

W JSON tablica `Monsters` zawiera ustawienia i punkty startowe. Aktualne HP oraz stan AI są przechowywane osobno w sesji. Mapy bez tego pola wczytują się z pustą listą potworów.

## Kamera

- FOV: 35°.
- Dystans: domyślnie 30, zakres 15–50, krok zoomu 4.
- Pitch: od 30° przy zbliżeniu do 60° przy oddaleniu, domyślnie około 42,9°.
- Stały heading: 90° dla perspektywy, 45° dla izometrii; brak obracania Q/E.
- Płynne podążanie za postacią i ruch względem kierunku kamery.

## Świat, dźwięk i zapis postępu

Ustawienia z **World** w edytorze działają również w Runtime. Obiekty zasłaniające gracza płynnie stają się przezroczyste. Oświetlenie obejmuje podłoże, grafiki i wodę; dostępne są kolorowe światła punktowe, miękkie cienie pod obiektami oraz opcjonalny cykl dnia i nocy. Ogniska i pochodnie automatycznie emitują światło. Na klatkę wybieranych jest do 16 najbliższych świateł.

Dźwięki cichną z odległością, a panorama stereo odpowiada kierunkowi kamery. Ogniska, pochodnie i pobliska woda mają automatyczny ambient; kroki zależą od materiału podłoża. Dostarczone dźwięki są generowanymi placeholderami. Własne WAV/OGG/MP3 umieść w `Editor/audio/` i przypisz do emiterów w **World → Sounds**. Głośność i wyciszenie ustawia **World → Environment**. Brak dostępnego urządzenia audio nie blokuje gry.

Wejście w obszar triggera może wyświetlić wiadomość, rozpocząć rozmowę, otworzyć/zamknąć drzwi, stworzyć potwora, teleportować gracza lub wczytać mapę z `Runtime/Maps/`. Akcja uruchamia się przy wejściu; **Once** ogranicza ją do jednego wykonania. Przejścia przenoszą HP i ekwipunek oraz zachowują stan odwiedzonych światów, do 32 map w zapisie. Rozmowa i otwarty ekwipunek zatrzymują triggery, czas świata oraz walkę.

**F6** atomowo zapisuje postęp w `Runtime/Saves/`, **F9** go przywraca. Zapis obejmuje pozycję i HP gracza, EQ, zawartość i stan otwarcia skrzyń, drzwi, pokonane potwory i pozostałe HP, wykonane triggery oraz porę dnia na odwiedzonych mapach. Plik jest przypisany do ścieżki mapy startowej; błędny zapis nie zastępuje bieżącej sesji. Podgląd edytora ma osobne pliki w `Editor/obj/progress/`. Mapy źródłowe JSON pozostają plikami projektu. Enter po śmierci odtwarza stan początkowy bieżącej mapy.

Kolizje mogą być okręgami, obracanymi prostokątami i wklęsłymi wielokątami. Sprawdzany jest cały odcinek ruchu, co zatrzymuje postacie przed cienkimi ścianami. Te same kształty są używane podczas wyznaczania tras i sprawdzania ciosów. Otwarte drzwi nie blokują ruchu.

Podłoże korzysta z buforowanych siatek GPU po 16 × 16 kafelków. Malowanie zmienia wyłącznie dane materiałów; zmiana wysokości przebudowuje dotknięte fragmenty i sąsiadów współdzielących wierzchołki. Cache usuwa nieużywane fragmenty podczas przemieszczania się po dużych mapach.

## Dane

`World/` zawiera wspólny format mapy JSON, katalog grafik, renderer, ruch gracza, teren, wodę i system dialogów. Editor kompiluje te same pliki źródłowe. NPC są w tablicy `Npcs`; każdy ma własne drzewko z identyfikatorami węzłów i uporządkowanymi połączeniami `Next`. Odpowiedź bohatera ma najwyżej jeden cel NPC; może wskazywać istniejącą kwestię, także wcześniejszą. Kwestia NPC prowadzi do jednej kwestii NPC albo do listy odpowiedzi bohatera. Sekwencje NPC, pętle i wspólne cele korzystają z tego samego formatu JSON. Starsze mapy bez `Npcs` wczytują się z pustą listą NPC. Mapy zachowują niezależne wymiary `Width` i `Depth`. `Heights` przechowuje wysokości `(Width + 1) × (Depth + 1)` wierzchołków lub pustą tablicę dla płaskiej mapy; `WaterLevel` i `WaveHeight` określają poziom i amplitudę wody. `WaterCells` to maski dwóch trójkątów każdego kafelka, zapisane jako Base64; materiały dna pozostają w `Tiles`. Starsze mapy z polami water w `Tiles` pozostają obsługiwane. Materiały są w `Editor/objects/`, `Editor/characters/` i `Editor/terrain/` — uruchamiaj aplikacje z zachowaniem struktury repozytorium.

Brakująca grafika obiektu jest zastępowana różowym prostokątem, a brakująca tekstura podłoża trawą. Odwołania zapisane w mapie pozostają zachowane.

`Player.Inventory` i `Chests[].Contents` przechowują wymiary siatki oraz tablicę `Items` z identyfikatorem egzemplarza, `DefinitionId` i pozycją X/Y. `Chests` zawiera także nazwę i właściwości obiektu. Brak tych pól w starszej mapie oznacza puste EQ i brak skrzyń. Format odrzuca nieznane przedmioty, powtórzone identyfikatory, nakładające się itemki i itemki wystające poza siatkę.

## Testy

```sh
dotnet run --project Runtime -- --camera-test
dotnet run --project Runtime -- --world-test
dotnet run --project Runtime -- --world-gpu-test
dotnet run --project Runtime -- --billboard-test
dotnet run --project Runtime -- --dialogue-test
dotnet run --project Runtime -- --inventory-test
dotnet run --project Runtime -- --monster-test
dotnet run --project Runtime -- --map-test
dotnet run --project Runtime -- --terrain-test
dotnet run --project Runtime -- --terrain-blend-test
dotnet run --project Runtime -- --sprite-water-test
dotnet run --project Runtime -- --smoke-test
```

Testy kamery, dialogów, map i terenu działają bez okna. Test dialogów sprawdza rozgałęzienia, zakończenia, zasięg rozmowy, kolizje NPC, walidację drzewa, polskie teksty i zgodność starszych map. Test map sprawdza tworzenie, malowanie, cofanie, zapis/wczytanie i podgląd mapy 2048 × 2048 oraz ograniczenie renderowania podłoża do widocznego obszaru. Test terenu sprawdza pędzle, trafianie promieniem w siatkę, zbocza, wysokość postaci, geometrię wody, fale i przycinanie brzegów. Podgląd wymaga sesji graficznej, kończy się po 15 klatkach i zapisuje `wildwood-smoke.png` w katalogu głównym repozytorium. Warianty kamery: `--smoke-test --close` i `--smoke-test --far`. `--dialogue-smoke-test --map ścieżka.json` pokazuje rozmowę pierwszego NPC z podanej mapy i kończy się w ten sam sposób.

`--sprite-water-test` wymaga sesji graficznej i porównuje piksele renderowane przez GPU przy różnych przybliżeniach: wodę pod przezroczystym tłem i otworami grafiki, widoczną część obiektu oraz zanurzenie. Zapisuje podgląd drzewa nad wodą w `Editor/obj/sprite-water.png`. Ten sam test uruchamia się w teście interakcji edytora.

`--terrain-blend-test` porównuje piksele GPU na środkach, granicach i narożnikach materiałów, sprawdza własne tekstury, malowanie/cofanie, powtarzanie bez przenikania atlasu, kilka partii renderowania, zmianę widocznego obszaru mapy 2048 × 2048, starsze pola water oraz oba rzuty na zboczach. Wymaga sesji graficznej. Zapisuje `Editor/obj/terrain-blend-perspective.png` oraz `Editor/obj/terrain-blend-isometric.png`. Pełny test interakcji edytora również go uruchamia.

`--inventory-test` działa bez okna i sprawdza rozmiary itemków, kolizje w siatce, przenoszenie bez utraty przedmiotów, pełne EQ, zasięg skrzyni, duplikowanie, zapis/wczytanie, cofanie oraz niezależność podglądu. `--inventory-smoke-test --map ścieżka.json` pokazuje EQ i pierwszą skrzynię z mapy (musi być w zasięgu punktu startowego); bez skrzyń pokazuje samo EQ. Kończy się po 15 klatkach i zapisuje `Editor/obj/inventory-runtime.png`.

`--monster-test` działa bez okna i sprawdza dane, walidację, zgodność starszych map, edycję, pościg, powrót, omijanie skały, wodę i zbocza, zasięg i przerwy ataków, obrażenia, śmierć, restart oraz pauzę w dialogach i EQ. `--monster-smoke-test --map ścieżka.json` uruchamia krótki podgląd i zapisuje `Editor/obj/monster-runtime.png`.

`--world-test` sprawdza dane i zgodność starszych map, kolizje i cienkie ściany, triggery, zapis/wczytanie i przejścia między mapami, niezależność Play, prefabrykaty, cofanie, wersje fragmentów terenu oraz panoramę i generowanie dźwięków. `--world-gpu-test` wymaga sesji graficznej i porównuje piksele oświetlenia, cieni oraz zanikania w obu rzutach; sprawdza ponowne użycie siatek GPU, częściową przebudowę, usuwanie starych fragmentów oraz dekodowanie dźwięków.
