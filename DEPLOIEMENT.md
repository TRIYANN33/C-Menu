# Déploiement de C-Menu

## Création de l'exécutable

Sur le PC de développement Windows équipé du SDK .NET 6, lancer `Publier-C-Menu.cmd` à la racine du dépôt.

Le script publie l'application WinForms en version **Windows x64 autonome**, avec les dépendances .NET et SQLite incluses dans la publication, à l'emplacement :

`C:\Menu\C-Menu\C-Menu.exe`

Il ne faut pas installer Visual Studio ou le SDK .NET sur le PC destinataire. Windows peut toutefois afficher des avertissements de sécurité pour un exécutable non signé.

## Première ouverture et base SQLite

À la première ouverture, `MenuRepository` crée automatiquement la base et la table `Menus` si elles n'existent pas. L'emplacement de la base reste :

`%LOCALAPPDATA%\C-Menu\CMenu.db`

Cela conserve la compatibilité avec les données déjà enregistrées par C-Menu. La base est **distincte** du dossier de publication et n'est pas écrasée lors des mises à jour de l'exécutable.

Si vous copiez C-Menu sur un autre PC, une nouvelle base vide sera créée lors de la première ouverture. Pour transférer les données existantes, copiez séparément `CMenu.db` dans le dossier `%LOCALAPPDATA%\C-Menu` de l'utilisateur Windows concerné, application fermée.

## Applications générées

Le bouton **Créer Menu** publie chaque application dans `C:\Menu\applications\NomDuFormulaire`, avec sa copie portable de `CMenu.db`.

## Limites

La publication cible Windows x64 ; les PC Windows ARM64 ou 32 bits nécessitent une publication adaptée. L'écriture dans `C:\Menu` doit être autorisée. Le script publie les fichiers sur **le PC où il est exécuté** ; GitHub ne peut pas créer directement ce dossier sur votre ordinateur.
