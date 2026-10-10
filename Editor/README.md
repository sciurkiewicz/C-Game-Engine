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
| NPC / 5 | Wybierz PNG postaci lub złoty znacznik i kliknij mapę, aby dodać NPC. Po dodaniu edytor zaznacza NPC i pokazuje jego właściwości. |
| Sculpt / 6 | Przytrzymaj lewy przycisk myszy, aby podnosić, obniżać, wygładzać lub wyrównywać teren. |
| Water / 7 | Kliknij zagłębienie, aby wypełnić je wodą do ustawionego poziomu, lub opróżnij zbiornik. |

Obiekty można duplikować, usuwać i skalować. Kolizje są prostymi okręgami wokół podstawy obiektu. Wysokość wpływa na ich promień. Pozycje są domyślnie przyciągane do siatki co 0,5 jednostki; przycisk **Snap** wyłącza przyciąganie.

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
- F5 — włączenie/wyłączenie podglądu gry. Escape również kończy podgląd.

Domyślny plik: `Runtime/Maps/world.json`. Argument `--map ścieżka.json` zmienia plik roboczy. Zapis zawiera obiekty, NPC i dialogi, pozycję i grafikę postaci, materiał kafelków, wysokości powierzchni oraz ustawienia wody. Pliki PNG są wspólne dla edytora i Runtime; JSON przechowuje ścieżki względne, więc repozytorium można przenieść.

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
- Zaznacz kwestię NPC i wybierz **+ Player reply**, aby dodać odpowiedź bohatera. Można dodać wiele odpowiedzi.
- Zaznacz odpowiedź i wybierz **+ NPC line**, aby dodać dalszą kwestię NPC.
- **Edit text** otwiera pole tekstowe: wpisywanie lub wklejanie, Ctrl+A zaznacza całość, strzałki / Home / End lub kliknięcie przesuwają kursor, Backspace / Delete usuwa znaki, Enter dodaje nowy wiersz, Ctrl+Enter zatwierdza, Escape anuluje. Początkowy tekst jest zaznaczony; wpisanie nowego go zastępuje.
- **Delete branch / Delete** usuwa węzeł i jego potomków. Start oraz pierwsza kwestia NPC pozostają w drzewku.
- **Reply up / down** zmienia kolejność odpowiedzi. Węzeł bez dzieci kończy rozmowę: ostatnia odpowiedź bohatera zamyka okno, a ostatnią kwestię NPC zamyka Enter lub kliknięcie.
- Prawy/środkowy przycisk przeciąga drzewko, kółko przewija pionowo, Shift+kółko poziomo. **Home** przywraca początek, **Focus selected node** centruje zaznaczenie.

Ctrl+Z / Ctrl+Y cofa i ponawia zmiany drzewa. **Save / Ctrl+S** zapisuje całą mapę wraz z NPC i dialogami. **Close / Escape** wraca do mapy. Dialog może mieć do 256 węzłów, a pojedyncza kwestia do 2000 znaków. Format sprawdza połączenia i odrzuca cykle; starsze mapy bez NPC nadal działają.

W **Play / F5** podejdź do NPC i naciśnij E. Podgląd używa tych samych dialogów i sterowania co Runtime. Escape podczas rozmowy zamyka rozmowę; następny Escape kończy podgląd. F5 kończy podgląd również podczas dialogu.

Ta wersja nie zawiera skryptów, ekwipunku ani systemu zadań.

## Testy

```sh
dotnet run --project Editor -- --editor-test
dotnet run --project Editor -- --smoke-test
dotnet run --project Editor -- --interaction-test
```

Pierwszy test sprawdza dane i operacje edycji bez okna, w tym teren, wodę i mapy 2048 × 2048. Pozostałe wymagają sesji graficznej i zapisują `Editor/editor-smoke.png`. Test interakcji używa osobnych map w `Editor/obj/` oraz zapisuje zrzuty terraformowanego terenu i wody.
