# AdGestionHub — Plateforme SaaS de Gestion Commerciale (PME & Boutiques)

Application web multi-boutiques (multi-tenant) développée avec **ASP.NET Core MVC / Razor Pages (.NET 10)**, **Entity Framework Core**, **ASP.NET Core Identity** et **QuestPDF**.

## Fonctionnalités principales

- **Multi-boutiques isolé** : chaque commerce dispose de son propre catalogue, historique de ventes, dépenses, dettes et équipe.
- **Gestion des Stocks** : alertes automatiques de réapprovisionnement, recherche et filtrage par statut (disponible, stock bas, rupture), décrémentation SQL atomique anti-concurrence.
- **Point de Vente & Reçus** : panier dynamique avec autocomplétion, moyens de paiement adaptés (Cash, M-Pesa, Airtel Money, Orange Money, Carte Bancaire), reçus imprimables et téléchargement PDF multiplateforme via **QuestPDF**.
- **Suivi Financier & Rapports** : journal des dépenses par catégorie, calcul exact de la marge brute (coût d'achat figé `UnitCost` au moment de la vente) et du bénéfice net, export PDF par période.
- **Cahier des Dettes Clients** : suivi des créances, échéances et relance en un clic via WhatsApp.
- **Rôles & Permissions (RBAC)** :
  - `SuperAdmin` : supervision des boutiques (activation/suspension), annonces globales, journal des erreurs, tableau de bord Marketing & export CSV des leads Pro.
  - `Admin` (Propriétaire/Gérant) : accès complet à sa boutique, gestion des prix, marges, rapports, paramètres et comptes employés.
  - `Employé` (Vendeur) : encaissement au prix catalogue, consultation du stock (sans accès aux prix d'achat ni aux bénéfices), saisie des dépenses courantes.

---

## Démarrage rapide (Développement)

### 1. Configurer le compte `SuperAdmin` (sans secret dans le code)

Les identifiants du `SuperAdmin` ne sont jamais stockés dans `appsettings.json` ni dans le code source. Utilisez **User Secrets** en développement :

```bash
dotnet user-secrets init
dotnet user-secrets set "SuperAdmin:Email" "votre-email@domaine.com"
dotnet user-secrets set "SuperAdmin:Password" "UnMotDePasseFort2026!"
```

En production (Azure, Linux, Docker, IIS), définissez les variables d'environnement :

```bash
SuperAdmin__Email="votre-email@domaine.com"
SuperAdmin__Password="UnMotDePasseFort2026!"
ConnectionStrings__DefaultConnection="Server=...;Database=AdGestionHub;..."
```

> **Important (Sécurité)** : Si un ancien mot de passe a figuré dans l'historique Git, changez-le immédiatement depuis `/Identity/Account/Manage/ChangePassword` après connexion.

### 2. Compiler et lancer l'application

Au démarrage, l'application applique automatiquement les migrations EF Core en attente (`Database.MigrateAsync()`) et initialise les rôles (`Admin`, `Employé`, `SuperAdmin`) :

```bash
dotnet restore
dotnet build
dotnet run
```

Vous pouvez également appliquer les migrations manuellement via CLI :

```bash
dotnet ef database update
```

### 3. Option SQLite (développement léger / multiplateforme)

Par défaut, le projet utilise SQL Server (`DefaultConnection`). Pour utiliser SQLite localement, définissez dans vos User Secrets ou `appsettings.Development.json` :

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=adgestionhub.db"
  }
}
```
