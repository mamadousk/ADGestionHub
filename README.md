# AD Gestion Hub — MVP V1

**Pilotez votre activité. Maîtrisez vos chiffres.**

AD Gestion Hub est une application de gestion commerciale en français pour les commerçants et petites entreprises en RDC. Elle utilise **ASP.NET Core MVC / Razor (.NET 10 LTS)**, **ASP.NET Core Identity**, Entity Framework Core, SQL Server et QuestPDF.

## Démarrer en développement

Prérequis : SDK .NET 10 et, pour les tests HTTP et du navigateur, Python 3.10+ et Node.js 22+. SQLite est configuré par défaut dans l’environnement `Development` ; le fichier `adgestionhub-v1-dev.db` est ignoré par Git.

```bash
dotnet build AdGestionHub.slnx
dotnet run --project AdGestionHub.csproj --launch-profile http
```

Ouvrez `http://localhost:5216`, puis **Créer mon entreprise**. Créez un produit avec une quantité initiale, ajoutez un client si la vente est à crédit et faites une **Nouvelle vente**. Relisez le solde sur la fiche de la vente et les mouvements sur la fiche du produit.

SQLite est réservé aux nouvelles bases locales et aux tests. Il n’applique pas les migrations SQL Server : utilisez un nouveau fichier pour un environnement jetable si le schéma change. Ne supprimez pas une base contenant des données à conserver.

## Parcours couverts

- **Catalogue et stock** : catégorie, référence, prix, stock initial, entrées et sorties avec motif, historique et alertes visibles dans l’application. L’archivage retire un produit à stock nul du catalogue sans supprimer son historique.
- **Ventes et achats** : plusieurs lignes, remise sur vente réservée à l’administrateur, règlement intégral, partiel ou différé. La vente réduit le stock ; l’achat le réapprovisionne uniquement à la réception. Les paiements fournisseurs et les dépenses courantes sont distincts.
- **Répertoires et trésorerie** : fiches clients et fournisseurs, historique et soldes par devise. Un règlement est lié à son document. Une recette diverse peut être enregistrée sans vente. Aucune connexion aux opérateurs Mobile Money ni aux banques : le moyen choisi décrit le versement réellement effectué.
- **Documents** : brouillon de devis modifiable, émission, acceptation puis conversion en vente, sans double conversion. Une vente génère une facture commerciale imprimable et téléchargeable (PDF). **Les documents ne sont pas des factures normalisées DGI.**
- **Pilotage** : tableau de bord et rapports par période, exports PDF, Excel (`.xlsx`) et CSV. Le solde de clôture tient compte des paiements effectués jusqu’à la date choisie ; l’état du stock est celui du jour, valorisé au coût du catalogue.
- **Permissions** : les employés saisissent les ventes, règlements clients, fiches clients, devis et dépenses, et consultent le stock sans coût d’achat. L’administrateur de l’entreprise gère en plus le catalogue, les achats, les fournisseurs, l’équipe, la trésorerie et les rapports, et peut consulter le journal d’activité. Le compte de supervision de la plateforme est distinct des comptes des entreprises.

### CDF, USD et exactitude des chiffres

Le catalogue est en CDF ou en USD, selon le choix de l’entreprise. Cette devise est verrouillée après la première écriture pour ne pas relibeller des prix existants. Dans **Paramètres**, saisissez le taux réellement utilisé sous la forme **1 USD = X CDF** (jusqu’à six décimales). Tant qu’il n’est pas configuré, les nouvelles opérations sont limitées à la devise du catalogue et aucun taux n’est inventé.

Les montants sont calculés en `decimal` et arrondis à deux décimales pour CDF et USD. Le prix unitaire converti est arrondi avant multiplication. Les opérations mémorisent leur devise et leur taux lorsqu’il est disponible. Un règlement ultérieur peut être saisi dans l’autre devise : le montant versé, son taux et le montant imputé au document sont enregistrés séparément. Si l’arrondi empêche de solder dans la seconde devise, enregistrez le reste dans la devise du document. Les totaux de trésorerie sont exprimés dans la devise réellement versée ; les créances et dettes restent dans la devise du document. Les rapports n’additionnent jamais CDF et USD.

**Limites V1 :** aucun remboursement ni avoir. Seule une vente sans règlement peut être annulée depuis l’application. Les achats en attente sont réceptionnés en une seule fois. Les autres exclusions du MVP (fiscalité/DGI, comptabilité générale, paie, services bancaires intégrés) ne sont pas implémentées.

## Vérifications

```bash
node --test tests/trade.test.cjs
dotnet run --project tests/CommerceSmoke
python3 tests/http_smoke.py   # après dotnet build
```

Le scénario métier utilise une vraie base SQLite. Il vérifie notamment les écritures atomiques, les règlements concurrents, la reprise d’un devis, le taux historique et les formats exportés. Le scénario HTTP teste l’inscription, la vente partielle, le stock, les téléchargements, les droits des employés et l’isolation entre deux entreprises. GitHub Actions lance ces vérifications à chaque push et pull request, y compris la migration sur un SQL Server jetable et la recette Chromium (téléphone, tablette, ordinateur, impression). Pour ajouter le test Chromium en local : installez `playwright` dans votre environnement Python, lancez `python -m playwright install chromium`, puis `ADGH_SMOKE_BROWSER=1 python tests/http_smoke.py`.

Pour exécuter aussi les scénarios de migration et de concurrence sur un **serveur SQL Server de test**, fournissez la chaîne de connexion via `ADGH_SMOKE_SQLSERVER`. Le test crée et supprime une base au nom aléatoire sur ce serveur : utilisez uniquement un environnement jetable où le compte est autorisé à le faire.

## Déploiement et pilote

Consultez le [guide de déploiement et de sauvegarde](docs/exploitation-mvp.md) avant toute migration des données. L’ouverture au public nécessite une restauration testée, la validation du déploiement SQL Server et un pilote avec 5 à 10 entreprises sur ordinateur et téléphone.
