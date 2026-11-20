# AD Gestion Hub — Gestion commerciale gratuite

**Pilotez votre PME, de la vente aux chiffres.**

AD Gestion Hub est un logiciel de gestion commerciale en français pour les PME, commerces et distributeurs en RDC. Il relie les ventes, le stock, les encaissements et les dépenses en CDF et en USD. La vitrine présente les fonctions disponibles, des [aperçus ordinateur et iPhone](Views/Home/_MarketingDevices.cshtml) et une [démonstration en images](Views/Home/_MarketingDemo.cshtml) sur données fictives, sans compte.

L’application repose sur **ASP.NET Core MVC / Razor (.NET 10 LTS)**, **ASP.NET Core Identity**, Entity Framework Core, SQL Server, QuestPDF et SkiaSharp pour le traitement des logos.

## Démarrer en développement

Prérequis : SDK .NET 10 ; Python 3.10+ et Node.js 22+ pour la recette. SQLite est utilisé seulement en `Development`, sur un fichier `adgestionhub-v1-dev.db` ignoré par Git.

```bash
dotnet build AdGestionHub.slnx
dotnet run --project AdGestionHub.csproj --launch-profile http
```

Ouvrez `http://localhost:5216`, puis **Créer un compte**. L’inscription est gratuite et ne demande pas de moyen de paiement. Après création, l’utilisateur doit lire et accepter explicitement les conditions avant d’accéder à la gestion. L’administrateur issu de l’inscription voit ensuite le formulaire de configuration complet : il peut l’enregistrer ou choisir **Configurer plus tard** et y revenir via **Paramètres**. Créez ensuite un produit en stock, ajoutez un client si la vente est à crédit, puis enregistrez une **Nouvelle vente**.

SQLite n’applique pas les migrations SQL Server. Si le fichier de développement a été créé avec une version antérieure, configurez une nouvelle base jetable (exemple : `ConnectionStrings__DefaultConnection='Data Source=adgestionhub-v1-public-dev.db'`). Ne supprimez pas un fichier contenant des données à conserver.

## Fonctionnalités

- **Catalogue et stock** : catégorie, référence, prix, stock initial, entrées et sorties avec motif, historique et alertes visibles dans l’application. L’archivage retire un produit à stock nul du catalogue sans supprimer son historique.
- **Ventes et documents** : caisse avec grille de produits, recherche par nom ou référence, panier multiligne et total immédiat. Remise administrateur, règlement intégral, acompte ou crédit ; calcul facultatif de la monnaie pour les espèces. Une vente sort les articles du stock. Les documents commerciaux sont consultables, imprimables et téléchargeables en PDF ; ils affichent le logo facultatif de l’entreprise et la mention « Généré par AdGestionHub ». **Ils ne sont pas normalisés DGI.**
- **Clients et trésorerie** : fiches clients, suivi des créances et de leurs règlements, recettes diverses et dépenses courantes. Aucune connexion aux opérateurs Mobile Money ni aux banques : le moyen choisi décrit le versement réellement effectué.
- **Pilotage** : tableau de bord et rapports par période, avec le bénéfice pour les comptes autorisés, exports PDF, Excel (`.xlsx`) et CSV. Les listes d’activité gardent la devise d’origine de chaque opération ; les totaux restent calculés par devise. Le solde de clôture tient compte des paiements effectués jusqu’à la date choisie ; le stock est celui du jour, valorisé au coût catalogue.
- **Équipe et accès** : à la création ou à la modification d’un employé, choisissez un profil de départ et ajustez les fonctionnalités autorisées. Le profil **Caisse uniquement** permet de saisir les ventes et d’imprimer ses propres reçus, sans historique global, tableau de bord ni rapports. L’historique, le stock en lecture seule, les fiches clients, les règlements, les dépenses, les chiffres du tableau de bord et les exports se délèguent séparément. L’administrateur voit l’auteur des opérations et ouvre l’historique filtrable d’un employé en cliquant sur son nom. Les actions d’administration et le journal lui restent réservés ; la supervision est un compte distinct.

Les modules **devis, achats fournisseurs et fournisseurs** ne sont plus accessibles, y compris par leurs anciennes URL. Les données et mouvements historiques ne sont pas supprimés. Si nécessaire, les rapports et leurs exports comportent une rubrique **Historique fournisseurs** en lecture seule : achats archivés, décaissements et soldes de clôture restent distincts des recettes et des dépenses. Un achat non réceptionné n’ajoute pas de stock ; saisissez un mouvement d’entrée uniquement pour des articles effectivement reçus après le passage à cette version.

### CDF, USD et exactitude des chiffres

Choisissez la devise de référence de votre catalogue. Elle est verrouillée après le premier produit ou la première opération pour ne pas relibeller des prix existants. Dans **Paramètres**, indiquez le taux réellement utilisé sous la forme **1 USD = X CDF** (jusqu’à six décimales). Tant qu’il n’est pas configuré, les nouvelles opérations sont limitées à la devise du catalogue.

Les calculs utilisent des `decimal` à deux décimales pour les montants. Le prix unitaire converti est arrondi avant multiplication. Les opérations gardent leur devise et leur taux enregistré. Un règlement ultérieur peut être saisi dans l’autre devise : le versement, son taux et l’imputation sont stockés séparément. Les rapports n’additionnent jamais CDF et USD.

**Limites V1 :** aucun remboursement ni avoir ; seule une vente sans règlement peut être annulée depuis l’application. Pas de comptabilité générale, de facturation fiscale intégrée, de paie ni de paiement en ligne. Pour reproduire exactement un document émis, conservez un exemplaire : le logo et les coordonnées des réimpressions sont ceux des paramètres actuels.

## Vérifications

```bash
node --test tests/trade.test.cjs
dotnet run --project tests/CommerceSmoke
python3 tests/http_smoke.py   # après dotnet build
```

Les scénarios vérifient les écritures atomiques, les règlements concurrents, l’exactitude des archives, les accès employés et leur révocation, la validation du logo, l’acceptation versionnée et le report de la configuration. GitHub Actions lance ces vérifications ainsi que la migration sur SQL Server et la recette Chromium : navigation mobile/tablette/ordinateur, clavier et contraste, et vérification du logo et du texte dans le PDF téléchargé et celui généré par l’impression du navigateur.

Pour lancer la recette navigateur localement, installez `playwright` et `pypdf` dans votre environnement Python, lancez `python -m playwright install chromium`, puis `ADGH_SMOKE_BROWSER=1 python tests/http_smoke.py`.

Pour les scénarios SQL Server, configurez `ADGH_SMOKE_SQLSERVER` vers un serveur jetable. Le test y crée et supprime une base au nom aléatoire et applique deux fois le script idempotent de mise à jour.

## Déploiement et pilote

Consultez le [guide d’exploitation](docs/exploitation-mvp.md) avant toute migration. Pour une recette sous IIS chez MonsterASP, suivez le [guide de déploiement et de diagnostic de l’erreur 500.30](docs/deploiement-monsterasp.md). L’ouverture au public suppose notamment une restauration testée, la validation du déploiement SQL Server, la vérification des textes légaux et un pilote avec 5 à 10 entreprises.
