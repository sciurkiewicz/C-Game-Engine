# Informacje o postaciach

[player.json](player.json) to wspólny profil bohatera dla wszystkich map. Możesz edytować go bez zmiany kodu C#. Runtime odczytuje go przy starcie, a edytor przy każdym włączeniu **Play / F5**. Żeby zastosować nowe wartości, uruchom grę ponownie albo zatrzymaj i włącz Play.

| Pole | Domyślnie | Znaczenie i zakres |
| --- | --- | --- |
| `Name` | `Bohater` | Nazwa postaci, 1–80 znaków. |
| `Description` | pusty tekst | Opis lub historia postaci, do 2000 znaków. |
| `MaxHealth` | `100` | Maksymalne HP, 1–10000. |
| `Damage` | `10` | Obrażenia pojedynczego ataku, 1–1000. |
| `WalkSpeed` | `3.6` | Prędkość chodzenia w m/s, 0.1–100. |
| `RunSpeed` | `6.0` | Prędkość biegu w m/s, od prędkości chodzenia do 100. |
| `AttackRange` | `1.8` | Zasięg ataku w metrach, 0.1–16. |
| `AttackCooldown` | `0.4` | Odstęp między atakami w sekundach, 0.05–10. |
| `InteractionRange` | `2.5` | Zasięg rozmów i otwierania skrzyń w metrach, 0.1–32. |

Przykład: ustaw `MaxHealth` na `150` i `WalkSpeed` na `4.2`, zapisz plik, a następnie uruchom ponownie grę lub Play. W JSON używaj kropki dziesiętnej. Pominięte pola przyjmują wartości domyślne; nieznane pola i nieprawidłowe wartości powodują czytelny komunikat. Błędny profil w edytorze pozostawia otwartą mapę i pozwala poprawić plik przed kolejną próbą Play.

`Name` i `Description` przechowują informacje o bohaterze; obecny HUD pokazuje tylko pasek jego życia bez liczb. Pozycję startową, grafikę, wysokość i początkowy ekwipunek nadal ustawiasz dla każdej mapy w edytorze. Są zapisane w polu `Player` mapy, więc zmiana wspólnego profilu nie wymaga ponownego zapisywania map.

| Plik | Zawartość |
| --- | --- |
| [PlayerDefinition.cs](PlayerDefinition.cs) | Format, wartości domyślne, odczyt i walidacja profilu bohatera. |
| [PlayerStart.cs](PlayerStart.cs) | Dane bohatera przypisane do konkretnej mapy: start, grafika, wysokość i EQ. |
| [NpcData.cs](NpcData.cs) | Nazwa i dialog postaci niezależnej; właściwości sceny obejmują grafikę i pozycję. |
| [MonsterData.cs](MonsterData.cs) | Dane przeciwników: nazwa, HP, obrażenia, ruch i zasięgi; walidacja i operacje edytora. |
| [MonsterState.cs](MonsterState.cs) | Bieżące HP, stan AI i trasa przeciwnika podczas rozgrywki. |
| [CharacterChecks.cs](CharacterChecks.cs) | Testy profilu, ruchu, walki i zgodności zapisów postępu. |

Statystyki poszczególnych potworów i informacje o NPC nadal zapisują się w mapach. Logika ich zachowania znajduje się w [Mechanics](../Mechanics/).

Nowe zapisy postępu pamiętają także maksymalne HP z chwili zapisu. Po zmniejszeniu `MaxHealth` w profilu wczytane życie zostaje ograniczone do nowego maksimum. Starsze zapisy z domyślnymi 100 HP również działają.

Test bez okna, z głównego katalogu projektu:

```sh
dotnet run --project Runtime -- --character-test
```
