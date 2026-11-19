# Tester AD Gestion Hub sur MonsterASP.NET

L’erreur **HTTP 500.30** est une erreur de démarrage sous IIS. La page publique n’indique pas la cause. En production, AD Gestion Hub utilise **ASP.NET Core 10 et SQL Server** : au démarrage, il applique ou vérifie les migrations, puis initialise les rôles. Sans configuration hébergée, `appsettings.json` pointe vers **LocalDB**, une base locale de développement, et non vers votre base MonsterASP. SQLite est réservé au développement.

## 1. Lire l’erreur de démarrage

Dans le panneau MonsterASP, ouvrez **Websites → votre site → Logs** et l’entrée **Application Error** de l’heure de votre essai. Si elle est trop courte, dans **Logs → ASP.NET Core debug**, activez temporairement **Status → Enabled**, puis cliquez sur **Restart application & reload log** si le journal reste vide. Après avoir relevé la première exception et son exception interne, désactivez le journal de débogage. Gardez l’application en `Production`. Ne partagez qu’un extrait expurgé, sans mot de passe, chaîne de connexion complète ni profil de publication.

[Aide MonsterASP : erreur 500.30](https://help.monsterasp.net/troubleshooting/common-errors/http-500-30) · [Journal de démarrage ASP.NET Core](https://help.monsterasp.net/troubleshooting/logs/aspnet-core-debug-logging)

## 2. Relier le site à la base SQL Server

1. Dans **Databases**, créez une base **MSSQL / SQL Server** de recette si vous n’en avez pas déjà une. Choisissez le même environnement d’hébergement que le site (Free ou Premium). Dans la base, ouvrez **Users → Connection string → Local access**. Copiez la chaîne complète pour le compte de cette base, sans la publier ni l’ajouter au dépôt. « Local access » désigne ici l’adresse interne MonsterASP, pas LocalDB sur votre PC. Il n’est pas nécessaire d’activer l’accès distant pour le site hébergé chez eux.
2. Dans **Websites → votre site → Scripting → Environment variables**, renseignez ces variables. Si `DOTNET_ENVIRONMENT` est déjà défini, vérifiez qu’il vaut aussi `Production`.

   | Nom | Valeur |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `DatabaseProvider` | `SqlServer` |
   | `ConnectionStrings__DefaultConnection` | Chaîne complète **Local access** de cette base MSSQL |

3. Pour une **nouvelle base vide de recette**, le comportement par défaut applique les migrations au démarrage si le compte peut modifier le schéma. Pour une base déjà utilisée, sauvegardez-la et testez la migration sur une copie. Si le compte applicatif ne peut pas créer/modifier les tables, générez et relisez le script idempotent SQL Server décrit dans le [guide d’exploitation](exploitation-mvp.md#migrer-et-contrôler-les-données), puis appliquez-le sur la base cible avec un compte autorisé. Si vous utilisez l’[import SQL du panneau](https://help.monsterasp.net/databases/mssql/import-sql-file), vérifiez le détail du journal : l’import peut continuer après une erreur. Une fois le schéma à jour, configurez `Database__ApplyMigrations=false` et utilisez un compte applicatif en lecture/écriture. Ne désactivez pas les migrations sur une base vide ou dont le schéma n’est pas à jour.
4. Cliquez sur **Restart application**. Une fois le site démarré, vérifiez l’accueil et `/health` (HTTP 200, `Healthy`). Avant de tester l’inscription ou la connexion, [activez HTTPS pour le domaine](https://help.monsterasp.net/domains-https/https/lets-encrypt) : les cookies de connexion sont sécurisés en production.

[Aide MonsterASP : connexion MSSQL](https://help.monsterasp.net/databases/mssql/connection-string) · [Variables d’environnement](https://help.monsterasp.net/websites/configuration/environment-variables)

## 3. Choisir le correctif d’après le journal

| Message dans le journal | Action |
| --- | --- |
| `CREATE DATABASE permission denied in database 'master'` (SQL Server 262) | EF Core tente de créer la base indiquée par la chaîne de connexion, car il ne peut pas ouvrir cette base. Si le journal montre `CREATE DATABASE [AdGestionHub]` mais que la base provisionnée porte un autre nom, copiez à nouveau la chaîne **Local access** complète du panneau, sans remplacer `Database` / `Initial Catalog` par `AdGestionHub`. Créez la base depuis **Databases** si elle n’existe pas encore. Ne ciblez pas `master` et ne demandez pas de privilège de création de bases au niveau du serveur. |
| `LocalDB`, `Login failed for user`, `Cannot open database` ou timeout SQL | Vérifiez que la variable s’appelle exactement `ConnectionStrings__DefaultConnection` (deux `_`), que sa valeur est la chaîne interne complète du bon compte et de la bonne base, puis redémarrez le site. |
| `CREATE TABLE permission denied`, erreur DDL ou migrations en attente | Vérifiez la base cible et appliquez les migrations selon l’étape 2. Ne supprimez pas la base et ne modifiez pas manuellement l’historique des migrations. |
| `Microsoft.AspNetCore.App` `10.0` introuvable, `hostpolicy.dll`, `BadImageFormatException`, ou dépendance `QuestPdfSkia` manquante | Vérifiez la disponibilité du runtime ASP.NET Core 10 et la concordance de l’architecture du site (**Scripting → Bitness**) avec la publication. QuestPDF charge une dépendance native au démarrage ; lisez l’exception interne et republiez tout le dossier de sortie, pas uniquement la DLL. |
| `Failed to load configuration from file` | Corrigez le JSON de configuration publié. Préférez les variables du panneau pour la chaîne SQL plutôt que de la mettre dans un fichier versionné. |

[Republiez via Web Deploy](https://help.monsterasp.net/websites/deploy/visual-studio) ou préparez une publication en dossier avec le SDK .NET 10, depuis la racine du projet :

```powershell
dotnet publish .\AdGestionHub.csproj -c Release --self-contained false -o ..\adgh-publish
```

Pour un transfert manuel, envoyez **le contenu du dossier publié** vers la racine `/wwwroot` du site MonsterASP. `web.config`, `AdGestionHub.dll`, `AdGestionHub.runtimeconfig.json`, les dépendances et le sous-dossier statique `wwwroot` doivent provenir de la même publication. Ne transférez ni les sources, ni les fichiers de bases locales, ni les sauvegardes. Avant de republier, sauvegardez la configuration du site et ne partagez pas le profil Web Deploy.
