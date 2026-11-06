# Mechanika gry

Ten folder zawiera działającą logikę gry współdzieloną przez Runtime i podgląd **Play / F5** w edytorze.

| Plik | Odpowiedzialność |
| --- | --- |
| [PlaySession.cs](PlaySession.cs) | Start sesji, sterowanie i kolejność aktualizacji gry. |
| [PlayerMovement.cs](PlayerMovement.cs) | Chodzenie, bieganie, normalizacja ruchu po skosie, kolizje i dopasowanie do terenu. |
| [PlayerInteraction.cs](PlayerInteraction.cs) | Zasięg rozmów i skrzyń, otwieranie oraz zamykanie ekwipunku. |
| [MonsterCombat.cs](MonsterCombat.cs) | Ataki bohatera i potworów, obrażenia, przerwy między atakami, śmierć i restart. |
| [MonsterNavigation.cs](MonsterNavigation.cs) | Omijanie przeszkód, wody i stromych zboczy przez przeciwników. |
| [DialogueSession.cs](DialogueSession.cs) | Rozpoczynanie rozmów, wybór odpowiedzi i przechodzenie przez dialog. |
| [WorldProgress.cs](WorldProgress.cs) | Triggery, cykl dnia, przejścia między mapami oraz zapis i wczytanie postępu. |

Parametry bohatera zmieniaj w [player.json](../Characters/player.json). Informacje o postaciach i format tych ustawień opisuje [instrukcja postaci](../Characters/README.md). Przedmioty, siatki ekwipunku i drzewa dialogów są częścią danych map w [World](../World/); ten folder odpowiada za ich zachowanie podczas rozgrywki.

Po zmianach w kodzie uruchom `dotnet run --project Runtime` z głównego katalogu projektu, aby zbudować i włączyć grę. Zmiany mechaniki działają też po ponownym uruchomieniu edytora.
