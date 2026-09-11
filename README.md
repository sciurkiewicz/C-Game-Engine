# Wildwood — prototyp Raylib / C#

Mała scena 2.5D inspirowana klimatem survivalowych gier: proceduralne,
autorskie billboardy postaci, drzew, skał, traw i krzaków w świecie 3D.
To prototyp eksploracji, bez pełnego systemu przetrwania.

## Uruchomienie

Wymagany .NET SDK 10. W katalogu projektu:

```sh
dotnet run
```

Pierwsze uruchomienie pobiera Raylib-cs z NuGet. Grafiki są rysowane
w kodzie; nie trzeba pobierać dodatkowych assetów.

## Sterowanie

- WASD lub strzałki — chodzenie
- Lewy Shift — bieganie
- Kółko myszy / przewijanie na gładziku lub + / - — płynny zoom
- R — domyślny zoom i obrót kamery
- Q / E — obrót kamery o 45 stopni
- Spacja — zbieranie jagód z pobliskiego krzaka
- Escape — wyjście

Kamera perspektywiczna patrzy pod kątem i podąża za postacią.
Billboardy obracają się ku kamerze, a ruch WASD jest liczony względem widoku. Drzewa i skały mają prostą kolizję.
Mapa jest generowana z ustalonym ziarnem, więc wygląda tak samo przy każdym starcie.

## Sprawdzenie renderowania

```sh
dotnet run -- --smoke-test
```

Otwiera okno na 15 klatek, zapisuje `wildwood-smoke.png` i kończy program.
Wymaga działającej sesji graficznej. Nie sprawdza ręcznego sterowania.

Dodatkowe warianty podglądu: `--smoke-test --rotated`, `--smoke-test --close` i `--smoke-test --far`.

## Kamera

Kamera używa parametrów powierzchni z klasycznego Don't Starve:
FOV 35°, domyślny obrót 45°, odległość 30 (zakres 15–50), krok zoomu 4.
Nachylenie płynnie rośnie od 30° przy zbliżeniu do 60° przy oddaleniu.
Podążanie, obrót i odległość mają osobne tempo wygładzania: 4, 20 i 1.
Zoom jest skierowany na postać, a Q/E obraca widok o 45° najkrótszą drogą.
Ruch używa docelowego kierunku kamery, więc podczas obrotu nie zakreśla łuku.
To zachowanie zwykłej kamery; nie obejmuje jaskiń ani efektów specjalnych gry.

Parametry odniesienia: [followcamera.lua](https://github.com/taichunmin/dont-starve-game-scripts/blob/master/cameras/followcamera.lua).
Implementacja: `FollowCamera.cs`.

Sprawdzenie podążania, ograniczeń zoomu, nachylenia i pełnego obrotu przy
30, 60 i 144 FPS (bez otwierania okna):

```sh
dotnet run -- --camera-test
```
