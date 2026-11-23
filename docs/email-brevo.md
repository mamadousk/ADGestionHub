# Configurer l’envoi d’e-mails avec Brevo

L’application envoie les e-mails transactionnels de compte via SMTP :

- confirmation de l’adresse e-mail à l’inscription ;
- réinitialisation du mot de passe ;
- code de connexion à 6 chiffres ;
- code de double authentification par e-mail.

Tant que la section `Smtp` n’est pas renseignée, l’envoi échoue et l’application le signale dans les journaux. Aucun e-mail n’est expédié en silence.

## 1. Créer le compte et vérifier le domaine

1. Créez un compte gratuit sur [brevo.com](https://www.brevo.com/).
2. Dans **Senders, Domains & Dedicated IPs**, ajoutez votre domaine (par exemple `votre-entreprise.cd`) et non une adresse `gmail.com`.
3. Brevo fournit les enregistrements **SPF** et **DKIM** à ajouter chez votre registrar DNS. Laissez la propagation se faire (quelques minutes à quelques heures).
4. Ajoutez aussi un enregistrement **DMARC**, par exemple :

```dns
_dmarc.votre-entreprise.cd. 3600 IN TXT "v=DMARC1; p=none; rua=mailto:postmaster@votre-entreprise.cd"
```

Un DMARC en `p=none` suffit pour débuter et surveiller. Passez à `p=quarantine` ou `p=reject` après vérification des scores.

## 2. Créer une clé SMTP

1. Dans **SMTP & API → SMTP**, créez une clé SMTP.
2. Conservez le **login SMTP** et la **clé**. La clé n’est affichée qu’une fois.
3. Vérifiez que l’adresse expéditrice (`Smtp:From`) correspond à un expéditeur ou domaine vérifié.

## 3. Configurer l’application

Renseignez la section `Smtp` par variables d’environnement, jamais dans `appsettings.json` versionné :

| Variable | Valeur |
|---|---|
| `Smtp__Host` | `smtp-relay.brevo.net` |
| `Smtp__Port` | `587` |
| `Smtp__UserName` | login SMTP Brevo |
| `Smtp__Password` | clé SMTP Brevo |
| `Smtp__From` | `noreply@votre-entreprise.cd` |
| `Smtp__FromName` | `AD Gestion Hub` |
| `Smtp__EnableSsl` | `true` |
| `Smtp__TimeoutSeconds` | `30` |

Le port **587** utilise STARTTLS. Le port 465 (SSL implicite) n’est pas recommandé avec `System.Net.Mail.SmtpClient`.

### Sous IIS

Dans `web.config`, ajoutez les variables dans `<environmentVariables>` :

```xml
<aspNetCore processPath="dotnet" arguments=".\AdGestionHub.dll" stdoutLogEnabled="true">
  <environmentVariables>
    <environmentVariable name="Smtp__Host" value="smtp-relay.brevo.net" />
    <environmentVariable name="Smtp__Port" value="587" />
    <environmentVariable name="Smtp__UserName" value="..." />
    <environmentVariable name="Smtp__Password" value="..." />
    <environmentVariable name="Smtp__From" value="noreply@votre-entreprise.cd" />
    <environmentVariable name="Smtp__FromName" value="AD Gestion Hub" />
  </environmentVariables>
</aspNetCore>
```

### Sous Linux

```bash
export Smtp__Host=smtp-relay.brevo.net
export Smtp__Port=587
export Smtp__UserName='...'
export Smtp__Password='...'
export Smtp__From='noreply@votre-entreprise.cd'
export Smtp__FromName='AD Gestion Hub'
export Smtp__EnableSsl=true
```

## 4. Tester

1. Créez un compte avec une adresse que vous contrôlez et vérifiez la réception du lien de confirmation.
2. Confirmez l’adresse, déconnectez-vous, puis testez **Mot de passe oublié**.
3. Testez la **connexion par code e-mail** depuis la page de connexion.
4. Activez la 2FA TOTP, déconnectez-vous, puis utilisez **Recevoir un code par e-mail** sur l’écran de double authentification.
5. Vérifiez les journaux applicatifs et les logs Brevo (deliveries, bounces, blocages).

## 5. Délivrabilité et limites

- Le plan gratuit de Brevo permet **300 e-mails par jour**. Au-delà, les plans payants commencent à 9 $/mois.
- Le plan gratuit peut ajouter le logo Brevo dans certains e-mails ; pour une marque parfaitement neutre, comparez avec Resend (3 000 e-mails/mois, 100/jour) ou Mailgun (100/jour).
- Ne modifiez jamais le `From` pour un domaine non vérifié : les messages iraient en spam ou seraient refusés.
- Surveillez le taux de rebond et les plaintes. Supprimez les adresses invalides, ne relancez pas indéfiniment une adresse qui rebondit.
- Les codes de connexion sont volontairement courts (6 chiffres, quelques minutes) et limités par adresse et par IP. Ne les journalisez jamais.
- Conservez les clés SMTP dans un coffre de secrets et rotez-les si elles sont exposées.

## 6. Dépannage

| Symptôme | Vérification |
|---|---|
| Aucun e-mail ne part | Section `Smtp` absente ou `From` non vérifié ; consulter les journaux applicatifs. |
| Erreur d’authentification | Clé SMTP régénérée, login et mot de passe corrects. |
| E-mails en spam | SPF, DKIM et DMARC du domaine ; `From` aligné sur le domaine vérifié. |
| Délai important | Port 587 sortant ouvert, pare-feu, latence réseau. |
| Code non reçu | Dossier spam, adresse saisie, délai de quelques minutes, quota Brevo. |

La configuration est indépendante du fournisseur : un autre SMTP (Microsoft 365, SendGrid, Mailgun, Amazon SES) fonctionne avec les mêmes clés `Smtp__*`.
