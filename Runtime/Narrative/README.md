# Karty narracyjne

[intro.json](intro.json) zawiera wstęp uruchamiany po kliknięciu **Nowa gra**. Każda pozycja w tablicy `Cards` to jedna karta. Kolejność pozycji jest kolejnością wyświetlania. Możesz dodawać, usuwać i przestawiać karty oraz zmieniać ich zawartość bez edytowania kodu C#.

Zapisz plik, wróć do menu głównego i ponownie wybierz **Nowa gra**, aby odczytać zmiany. Odtwarzany już wstęp zachowuje swoją kopię danych. Ostatnia karta wygasza cały ekran do czerni, łącznie z przyciskami. Po krótkiej chwili czerni następuje płynne odsłonięcie gry. Świat, walka, ruch i ekwipunek pozostają zatrzymane aż do końca tego przejścia. Podgląd Play w edytorze nadal uruchamia bezpośrednio mapę.

## Edycja karty

```json
{
  "Enabled": true,
  "BlackHold": 0.15,
  "GameFadeIn": 1.2,
  "Cards": [
    {
      "TextBlocks": [
        "Pierwszy blok tekstu. Możesz tu wpisać własną historię.",
        "Drugi blok pojawi się pod pierwszym, z odstępem.",
        "Możesz też wymusić nowy wiersz przez \nwewnątrz jednego bloku."
      ],
      "Image": "images/las.png",
      "Effect": "Typewriter",
      "Duration": 12,
      "FadeIn": 0.8,
      "FadeOut": 0.8,
      "CharactersPerSecond": 32,
      "FontSize": 26
    }
  ]
}
```

Obraz w tym przykładzie trzeba najpierw dodać do [images/](images/). Ustaw `"Image": null`, aby karta zawierała sam tekst. Możliwa jest również karta z samym obrazem i pustą tablicą `TextBlocks`.

| Pole | Znaczenie |
| --- | --- |
| `Enabled` | Włącza cały wstęp. `false` rozpoczyna grę od razu; tak samo działa pusta tablica `Cards`. |
| `BlackHold` | Chwila pełnej czerni po ostatniej karcie, domyślnie 0.15 s. Pole całej sekwencji, zakres 0–10 s. |
| `GameFadeIn` | Czas odsłaniania gry z czerni, domyślnie 1.2 s. Pole całej sekwencji, zakres 0.1–30 s. |
| `TextBlocks` | Osobne bloki tekstu. Pomiędzy nimi pojawia się pusty wiersz. Polskie znaki i znaki złożone są odsłaniane w całości. |
| `Image` | Opcjonalny obraz nad tekstem, np. `images/las.png`. Ścieżka jest względna wobec pliku JSON. |
| `Effect` | `Typewriter`, `Fade` albo `Scroll`. |
| `Duration` | Cały czas karty w sekundach, łącznie z pojawianiem i zanikaniem. Zakres: 0.1–3600. |
| `FadeIn` / `FadeOut` | Czas pojawiania i zanikania treści w sekundach, domyślnie po 0.6. Przy krótkiej karcie każdy fade zajmuje najwyżej połowę jej czasu. |
| `CharactersPerSecond` | Tempo pisania dla `Typewriter`, domyślnie 35. Ustaw odpowiednio długi `Duration`, aby zostawić czas na przeczytanie tekstu. |
| `FontSize` | Bazowy rozmiar tekstu, domyślnie 26; dostosowuje się do rozmiaru okna. |
| `ScrollDirection` | Kierunek dla `Scroll`: `Down` z góry na dół, `Up` z dołu do góry jak w typowych napisach końcowych. Domyślnie `Down`. |

W JSON używaj kropek dziesiętnych, np. `1.5`, oraz cudzysłowów wokół tekstu. Cudzysłów wewnątrz tekstu zapisuje się jako `\"`. Nieznane pola, nieprawidłowy czas i brakujące obrazy powodują komunikat w menu zamiast rozpoczęcia nieprawidłowego wstępu.

## Efekty

- **Typewriter** odsłania tekst znak po znaku po fade-in. Obraz pojawia się nad nim. Pierwsza spacja, Enter lub kliknięcie odsłania cały tekst; następne rozpoczyna fade-out i przejście dalej.
- **Fade** pokazuje od razu cały tekst i opcjonalny obraz, płynnie zwiększając i zmniejszając ich przezroczystość.
- **Scroll** przesuwa cały zestaw: opcjonalny obraz oraz bloki tekstu. Treść jest przycinana do obszaru karty, więc długie napisy przechodzą przez ekran. `Up` i `Down` wybierają kierunek ruchu.

Każda karta sama przechodzi do następnej po `Duration`. Spacja, Enter lub kliknięcie pozwalają przejść wcześniej, zachowując wygaszenie. **Esc** lub przycisk **Pomiń** pomija cały pozostały wstęp. Przy bardzo długim tekście na nieruchomej karcie kółko myszy przewija zawartość; do napisów końcowych użyj `Scroll`.

Obrazy obsługują PNG, JPG/JPEG, BMP i TGA. Zachowują proporcje i mieszczą się nad tekstem. W pamięci jest tylko obraz bieżącej karty; duże pliki są dopasowane do maksymalnie 2048 pikseli na dłuższym boku. Pliki źródłowe pozostają bez zmian.

## Pliki i sprawdzenie

- [NarrativeData.cs](NarrativeData.cs) — format, zapis, odczyt i walidacja kart.
- [NarrativePlayback.cs](NarrativePlayback.cs) — czas, odsłanianie tekstu i przechodzenie pomiędzy kartami.
- [NarrativePlayer.cs](NarrativePlayer.cs) — obraz, układ bloków, efekty, przewijanie i sterowanie.
- [NarrativeGameTransition.cs](NarrativeGameTransition.cs) — chwila czerni i płynne odsłonięcie gry.
- [NarrativeChecks.cs](NarrativeChecks.cs) — testy danych i czasu bez okna.
- [GameApp.cs](../GameApp.cs) — przejście menu → wstęp → gra.

Z głównego katalogu projektu:

```sh
dotnet run --project Runtime -- --narrative-test
dotnet run --project Runtime -- --narrative-interaction-test
```

Drugi test otwiera okno, sprawdza rzeczywiste wejście do gry z menu, układ obrazów i tekstu, trzy efekty, kierunki przewijania, czas, edycję pliku, pomijanie oraz zatrzymanie rozgrywki. Używa własnych plików tymczasowych; podglądy zapisuje w `Editor/obj/narrative-*.png`.
