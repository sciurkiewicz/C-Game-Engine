# Podłoże

Domyślne tekstury podłoża są plikami PNG 64 × 64: `grass.png`, `dirt.png`, `sand.png` i `stone.png`.
Możesz edytować je w dowolnym edytorze grafiki. Zapisz zmiany pod tą samą nazwą i uruchom ponownie edytor lub Runtime.
Istniejące mapy automatycznie korzystają z tych plików; tekstury nie są generowane w kodzie.
Zachowaj pliki domyślne — są wymagane przy starcie aplikacji.

`water.png` przechowuje dotychczasową teksturę materiału water. Osobna, animowana powierzchnia wody ma własne kolory i nie korzysta z tego PNG.

Umieść tutaj kwadratowe tekstury PNG i uruchom edytor ponownie.
Pojawią się w narzędziu **Ground**. Każda tekstura powtarza się na kafelkach o boku 1 jednostki.

Sąsiednie materiały automatycznie i płynnie mieszają się na granicach oraz w narożnikach. Środek kafelka zachowuje wybraną teksturę. Blendowanie działa na płaskim i wyrzeźbionym terenie w edytorze, Play oraz Runtime.
