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

Domyślny plik: `Runtime/Maps/world.json`. Argument `--map ścieżka.json` zmienia plik roboczy. Zapis zawiera obiekty, pozycję i grafikę postaci oraz materiał każdego kafelka. Pliki PNG są wspólne dla edytora i Runtime; JSON przechowuje ścieżki względne, więc repozytorium można przenieść.

Podgląd **Play** korzysta z kopii bieżącej mapy, także przed zapisem. Ruch w podglądzie nie zmienia punktu startowego. Osobny Runtime odczytuje mapę z dysku.

## Grafiki postaci i podłoża

- `Editor/characters/` — pojedyncze PNG postaci, najlepiej z przezroczystym tłem. Wybór w narzędziu Player.
- `Editor/terrain/` — kwadratowe, najlepiej powtarzalne PNG podłoża. Wybór w narzędziu Ground.

Po dodaniu materiałów uruchom aplikację ponownie. Na razie grafiki są statyczne: nie ma obsługi arkuszy animacji. Bez grafiki postaci wyświetlany jest niebieski znacznik. Wbudowane materiały to grass, dirt, sand, stone i water; water jest obecnie tylko teksturą, nie przeszkodą.

Nowa mapa ma 48 × 48 kafelków. Ta wersja nie zawiera dialogów, skryptów, ekwipunku ani systemu zadań.

## Testy

```sh
dotnet run --project Editor -- --editor-test
dotnet run --project Editor -- --smoke-test
dotnet run --project Editor -- --interaction-test
```

Pierwszy test sprawdza dane i operacje edycji bez okna. Pozostałe wymagają sesji graficznej i zapisują `Editor/editor-smoke.png`. Test interakcji używa wyłącznie pliku `Editor/obj/interaction-test.json`.
