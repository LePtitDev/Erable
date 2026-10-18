# Politique de sécurité

## Signaler une vulnérabilité

**N'ouvrez pas d'issue publique** pour une faille de sécurité. Utilisez le signalement privé de GitHub : onglet *Security* du dépôt, puis *Report a vulnerability* (« Privately report a vulnerability »).

Indiquez si possible :

- la version d'Erable (`/api/info`, ou *Aide* dans l'application) et le mode (bureau ou serveur) ;
- les étapes pour reproduire, et ce qu'un attaquant obtient (lecture, écriture, exécution, déni de service) ;
- toute preuve de concept utile.

Nous accusons réception sous 7 jours et vous tenons informé de la correction. Une fois la version corrigée publiée, la faille est décrite dans une *security advisory* GitHub, avec votre nom si vous le souhaitez.

## Versions suivies

Seule la **dernière version publiée** reçoit des correctifs de sécurité : l'application de bureau se met à jour seule, et l'image Docker se met à jour en retirant la nouvelle étiquette (`docs/deployment.md`).

## Périmètre

Les mesures en place et leurs limites sont décrites dans `docs/architecture.md` §9 (sécurité) et `docs/deployment.md` §4 (ce que le serveur protège). Sont hors périmètre : un serveur exposé sans HTTPS, un administrateur malveillant, et l'accès au disque de la machine qui héberge les projets.
