# Menu autonome

Application WinForms VB.NET **indépendante de l'exécutable C-Menu**. Projet .NET 6 Windows.

Ouvrir `Menu-Autonome.vbproj` dans Visual Studio et démarrer avec F5.

Au lancement, l'application lit en **lecture seule** la base existante :
`%LOCALAPPDATA%\C-Menu\CMenu.db`.

Les rubriques sont créées depuis les champs `Frm_Menu`, `NumMenu`, `Menu`, `NumSousMenu`, `SousMenu` et `Chemin`.
Les lignes dont `Menu` ou `SousMenu` commence par « Couleurs » (sans distinction de casse) sont exclues.

Chaque sous-menu ouvre le chemin enregistré avec Windows. La barre de menus s'adapte à la longueur des libellés ; une horloge affiche la date longue et les secondes.

**Indépendance** : le projet ne référence aucun code ni bibliothèque du projet C-Menu. Il partage uniquement la base SQLite existante. Si elle est absente, un message est affiché ; aucune base vide n'est créée.

Pour distribuer l'application, publier le projet depuis Visual Studio en mode autonome Windows, ou utiliser `dotnet publish -c Release -r win-x64 --self-contained true` depuis un environnement compatible.
