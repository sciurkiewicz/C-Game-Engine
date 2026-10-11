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
| Ground / 3 | Wybierz materiał. Trzymaj lewy przycisk myszy, aby malować podłoże. Rozmiar pędzla: 1, 3, 5 lub 7 kafelków. |
| Player / 4 | Kliknij mapę, aby ustawić punkt startowy. Możesz wybrać grafikę postaci i jej wysokość. |
| NPC / 5 | Wybierz PNG postaci lub złoty znacznik i kliknij mapę, aby dodać NPC. Ostatnio dodany NPC pozostaje zaznaczony; przycisk po prawej otwiera jego dialog. |
| Sculpt / 6 | Przytrzymaj lewy przycisk myszy, aby podnosić, obniżać, wygładzać lub wyrównywać teren. |
| Water / 7 | Kliknij zagłębienie, aby wypełnić je wodą do ustawionego poziomu, lub opróżnij zbiornik. |
| Chest / 8 | Kliknij mapę, aby wstawić skrzynię. Zaznacz ją i wybierz Items, aby przypisać zawartość. |
| Monster / 9 | Wybierz PNG postaci lub czerwony znacznik i kliknij mapę. Zaznacz potwora i wybierz Stats, aby ustawić walkę. |

Obiekty można duplikować, usuwać i skalować. Kolizje są prostymi okręgami wokół podstawy obiektu. Wysokość wpływa na ich promień. Pozycje są domyślnie przyciągane do siatki co 0,5 jednostki; przycisk **Snap** wyłącza przyciąganie.

Każde narzędzie pozostaje aktywne po użyciu. Można stawiać kolejne obiekty, NPC, skrzynie i Monstery bez ponownego wybierania zakładki. Dialog, Items i Stats po prawej edytują ostatnio dodanego aktora, a zamknięcie ich okna wraca do tej samej zakładki. **Select / 1** wybiera się ręcznie, aby przesuwać obiekty lub edytować wszystkie ich właściwości.

Biblioteka automatycznie odczytuje PNG z `Editor/objects/` oraz podkatalogów. Strzałki nad biblioteką zmieniają kategorię, kółko myszy przewija listę. Przezroczyste marginesy są pomijane podczas wczytywania, bez modyfikowania plików źródłowych.

## Sterowanie i zapis

- WASD lub przeciąganie prawym/środkowym przyciskiem — przesuwanie widoku.
- Kółko nad mapą — zoom. Kąt poziomy kamery pozostaje stały.
- F — skupienie widoku na zaznaczonym obiekcie albo postaci.
- G — widoczność siatki.
- Delete — usunięcie zaznaczenia; Ctrl+D — duplikowanie obiektu.
- Ctrl+Z / Ctrl+Y — cofnięcie / ponowienie, do 80 kroków. Całe pociągnięcie pędzla jest jednym krokiem.
- Ctrl+S — zapis; Ctrl+O — wczytanie; Ctrl+N — nowa, pusta mapa.
- Escape podczas edycji — powrót do zaznaczania.
- F5 — włączenie/wyłączenie podglądu gry. Escape zamyka aktywny dialog lub ekwipunek; poza nimi kończy podgląd.

Domyślny plik: `Runtime/Maps/world.json`. Argument `--map ścieżka.json` zmienia plik roboczy. Zapis zawiera obiekty, NPC i dialogi, Monstery ze statystykami, skrzynie z zawartością, startowy ekwipunek, pozycję i grafikę postaci, materiał kafelków, wysokości powierzchni oraz ustawienia wody. Pliki PNG są wspólne dla edytora i Runtime; JSON przechowuje ścieżki względne, więc repozytorium można przenieść.

Podgląd **Play** korzysta z kopii bieżącej mapy, także przed zapisem. Ruch w podglądzie nie zmienia punktu startowego. Osobny Runtime odczytuje mapę z dysku.

## Grafiki postaci i podłoża

- `Editor/characters/` — pojedyncze PNG postaci, najlepiej z przezroczystym tłem. Wybór w narzędziu Player.
- `Editor/terrain/` — kwadratowe, najlepiej powtarzalne PNG podłoża. Wybór w narzędziu Ground.

Po dodaniu materiałów uruchom aplikację ponownie. Na razie grafiki postaci są statyczne: nie ma obsługi arkuszy animacji. Bez grafiki postaci wyświetlany jest niebieski znacznik. Wbudowane materiały podłoża to grass, dirt, sand i stone. Woda ma własne narzędzie Water i zachowuje wybrany materiał dna.

**New / Ctrl+N** otwiera okno wyboru szerokości X i głębokości Z. Wpisz liczby, użyj przycisków +/- lub gotowych rozmiarów, w tym 2048 × 2048. Każdy bok może mieć 4–2048 kafelków; mapa może być prostokątna. Tab przełącza pole, Enter tworzy mapę, Escape anuluje. Po wybraniu rozmiaru edytor pyta o niezapisane zmiany. Utworzenie mapy można cofnąć.

## Terraformowanie i woda

**Sculpt / 6** edytuje wysokości wierzchołków powierzchni. Wybierz **Raise**, **Lower**, **Smooth** lub **Flatten** po lewej. Panel po prawej ustawia promień pędzla, siłę oraz docelową wysokość dla Flatten. Przytrzymaj LMB nad mapą; Shift zamienia Raise z Lower. Krawędź pędzla ma łagodne przejście. Wysokości wierzchołków są zaokrąglane do 0,25 m, także przy wygładzaniu, wyrównywaniu i wczytywaniu map. Siła pędzla kumuluje się między klatkami, więc słaby pędzel nadal działa. Zakres wysokości wynosi od −32 do 64 metrów. Obrys pędzla, trafianie kursorem, siatka oraz pozycje obiektów i NPC dopasowują się do podłoża.

**Water / 7** wypełnia zbiorniki. Najpierw wykop zagłębienie narzędziem Sculpt, ustaw **Water level**, wybierz **Fill basin** i kliknij poniżej tego poziomu. Woda rozlewa się po połączonych częściach zagłębienia, zatrzymuje na suchych grzbietach i kończy dokładnie na przecięciu z terenem. Wypełnianie zachowuje wysokości i materiały dna. Powierzchnia ma animowane fale i przezroczystość. **Drain basin** opróżnia kliknięty zbiornik. **Water level** jest wspólny dla mapy i zmienia się co 0,25 m; podnoszenie poziomu rozszerza istniejące zbiorniki. Woda może sięgać krawędzi mapy, także podczas wypełniania i podnoszenia poziomu. Przycisk **Waves** przełącza fale.

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

W Play / Runtime **I** otwiera ekwipunek, **E** otwiera najbliższą skrzynię lub rozmowę z NPC w zasięgu 2,5 jednostki. Przeciąganie przenosi przedmioty między skrzynią a EQ; **Shift+klik** przenosi je na pierwsze pasujące miejsce drugiej siatki. Brak miejsca zachowuje item w źródłowej siatce. Otwarte EQ zatrzymuje ruch. **I / Escape / X** zamyka panel, F5 wraca z podglądu do edycji. Zabranie itemków w podglądzie nie zmienia zawartości mapy w edytorze; Runtime przechowuje zmiany w bieżącej sesji.

Starsze mapy wczytują się z pustym startowym EQ i bez skrzyń. Ta wersja nie zawiera skryptów ani systemu zadań.

## Monstery

**Monster / 9** dodaje przeciwnika. Zaznaczanie, przeciąganie, wysokość, kolizja, duplikowanie i usuwanie działają tak jak dla NPC. **Name** zmienia nazwę, **Sprite** wybiera PNG z `Editor/characters/` lub czerwony placeholder, a **Stats** otwiera ustawienia HP, obrażeń, prędkości, zasięgu wykrywania, zasięgu ataku oraz przerwy między atakami. Przyciski +/- zmieniają wartości, Ctrl+Z / Ctrl+Y cofają i ponawiają zmiany również w tym oknie. Escape / Enter / Zamknij wracają do mapy. Statystyki zapisują się w JSON; duplikat otrzymuje własny identyfikator.

W **Play / Runtime** potwory wykrywają pobliskiego gracza, ścigają go, obchodzą przeszkody i atakują w zwarciu. Głęboka woda i strome zbocza blokują ich ruch. Po utracie gracza lub zbyt dużym oddaleniu wracają na miejsce początkowe. **Spacja** atakuje najbliższego potwora w zasięgu; można ją przytrzymać. Gracz zaczyna ze 100 HP, zadaje 10 obrażeń i atakuje co 0,4 s. Paski życia pokazują stan gracza i przeciwników. Pokonany Monster znika i przestaje blokować ruch. Otwarte EQ i rozmowa zatrzymują walkę obu stron.

Po śmierci gracza **Enter** przywraca początkową mapę, przeciwników, HP oraz zawartość ekwipunków i skrzyń. **F5** nadal wraca do edycji. Ruch, obrażenia, śmierć oraz restart w podglądzie nie zmieniają mapy w edytorze. Starsze mapy wczytują się bez potworów.

## Testy

```sh
dotnet run --project Editor -- --editor-test
dotnet run --project Editor -- --smoke-test
dotnet run --project Editor -- --interaction-test
dotnet run --project Editor -- --inventory-interaction-test
dotnet run --project Editor -- --dialogue-link-interaction-test
dotnet run --project Editor -- --dialogue-sequence-interaction-test
dotnet run --project Editor -- --monster-interaction-test
```

Pierwszy test sprawdza dane i operacje edycji bez okna, w tym przedmioty, skrzynie, teren, wodę i mapy 2048 × 2048. Pozostałe wymagają sesji graficznej. Pełny test interakcji zapisuje `Editor/editor-smoke.png` i obejmuje także itemki. Wariant `--inventory-interaction-test` sprawdza tylko skrzynie, przypisywanie, przeciąganie, zajęte pola, cofanie, zapis, otwieranie E/I, Shift+klik, zatrzymanie ruchu i niezależność podglądu. Mapy oraz zrzuty okien testowych są w `Editor/obj/`.

`--dialogue-link-interaction-test` sprawdza tworzenie i zmianę przekierowania, cykliczny układ drzewka, anulowanie, usuwanie, cofanie, zapis/wczytanie oraz wielokrotne powroty w podglądzie gry. Pełny test interakcji również obejmuje te sprawdzenia.

`--dialogue-sequence-interaction-test` sprawdza oba rodzaje odpowiedzi, zachowanie istniejących opcji przy dodawaniu kwestii NPC, cofanie, zapis/wczytanie oraz kontynuację przez Enter i kliknięcie w podglądzie. Pełny test interakcji również go uruchamia; testy danych i układu sekwencji są częścią `--editor-test`.

`--monster-interaction-test` sprawdza dodawanie, zaznaczanie, przeciąganie, nazwę, grafikę, wszystkie statystyki, cofanie, duplikowanie, usuwanie, zapis/wczytanie, małe okno, pościg, obrażenia, pauzę w panelach, Spację, śmierć i restart przez Enter. Pełny test interakcji także go uruchamia. Testy danych i logiki walki są częścią `--editor-test`.
