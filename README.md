# C-Menu

Application Windows Forms VB.NET ciblant **.NET 6.0** et utilisant **SQLite**.

## Fonctionnalités
- Enregistrer, modifier et supprimer des menus et sous-menus
- Rechercher avec Filtrer et trier avec A-Z
- Tableau DataGridView
- SQLite stocké dans `%LOCALAPPDATA%\C-Menu\CMenu.db`

## Démarrage
1. Installer Visual Studio avec la charge de travail **Développement .NET Desktop** et le SDK .NET 6.
2. Ouvrir `C-Menu.vbproj`.
3. Restaurer les packages NuGet.
4. Exécuter avec F5.

Le fichier de base de données est créé au premier lancement. Le projet cible `net6.0-windows` afin d'éviter l'erreur NETSDK1045 avec le SDK 6.0.300.
