# Journal des décisions

Une entrée par choix important : le contexte, les options, la décision et ce qu'elle implique. À compléter à chaque phase.

---

## D1 · Tablette plutôt que téléphone · 18/11/2026
- **Contexte** : le premier prototype était prévu pour un téléphone.
- **Options** : téléphone ; tablette Samsung déjà possédée ; vrai écran Xeneon Edge (~250 €).
- **Décision** : tablette.
- **Conséquences** : assez de place pour 6 widgets au lieu de 4, aucun achat. L'affichage téléphone reste disponible.

## D2 · Page web plutôt qu'appli Android native · 18/11/2026
- **Contexte** : il faut afficher quelque chose sur la tablette.
- **Options** : appli Android native ; page web servie par le PC.
- **Décision** : page web.
- **Conséquences** : rien à installer sur la tablette, le prototype existant est réutilisé, chaque modification se voit tout de suite. En contrepartie, pas de vraie appli plein écran sans HTTPS (options étudiées en phase 6).

## D3 · Spotify plutôt que les températures · 18/11/2026
- **Contexte** : l'idée de départ copiait le Xeneon Edge, avec les températures CPU et GPU.
- **Options** : températures (pilote système + droits admin) ; Spotify et stats légères.
- **Décision** : Spotify et stats légères, pas de températures.
- **Conséquences** : plus utile au quotidien, plus de pilote noyau, installation plus simple et plus acceptable sur un poste encadré.

## D4 · API .NET locale plutôt que connecteurs Claude seuls · 18/11/2026
- **Contexte** : la page publiée sur claude.ai lit déjà l'agenda via un connecteur.
- **Options** : tout faire via les connecteurs Claude ; un petit service .NET sur le PC.
- **Décision** : service .NET.
- **Conséquences** : accès aux fichiers du PC (journal, sessions Claude Code), commandes Spotify, fonctionne hors de l'appli Claude. Il faut en revanche installer et maintenir un service.

## D5 · Séances de sport lues dans Google Agenda · 18/11/2026
- **Contexte** : le planning sport change souvent (déplacements, annulations).
- **Options** : choisir la séance à la main chaque jour ; lire l'agenda.
- **Décision** : lire l'agenda, avec un choix manuel en secours.
- **Conséquences** : une seule source de vérité, zéro saisie. Dépend des emojis et mots-clés des titres, donc rendus configurables.

## D6 · Journal écrit par des commandes Claude Code · 18/11/2026
- **Contexte** : recommandation de la coach : un rapport en 3 parties (problématique, pourquoi, choix) dans un document daté.
- **Options** : saisie dans une appli ; commande lancée là où le travail se fait.
- **Décision** : commandes `/rapport`, `/prep-reunions` et `/decisions`, qui écrivent des fichiers Markdown datés.
- **Conséquences** : le rapport est rédigé à partir de la session réelle, en quelques secondes. Le fichier sert aussi de source à l'écran Réunions.

## D7 · Projet public, données fictives · 18/11/2026
- **Contexte** : le projet doit servir de vitrine.
- **Décision** : dépôt GitHub public, config perso hors du dépôt, données de démo inventées.
- **Conséquences** : rien de personnel ni de professionnel n'est exposé, et n'importe qui peut l'essayer en mode démo. Les commits utilisent l'adresse noreply de GitHub, l'adresse perso n'apparaît nulle part.

## D8 · Autoriser Claude Code depuis la tablette · 18/11/2026
- **Contexte** : Claude Code s'arrête souvent pour demander une autorisation, et je ne le vois pas si je regarde ailleurs. Je perds du temps, ou je perds le fil en surveillant la fenêtre.
- **Options** : surveiller VS Code ; notifications Windows ; carte sur la tablette avec Autoriser/Refuser.
- **Décision** : carte sur la tablette, via les hooks HTTP de Claude Code.
- **Conséquences** : je peux rester concentrée sur autre chose. Garde-fous : sans réponse, la question revient sur le PC, et les commandes sensibles ne peuvent pas être autorisées depuis la tablette.

## D9 · Sessions Claude Code renommées avec le ticket · 24/11/2026
- **Contexte** : l'écran doit afficher la tâche en cours à partir de la session Claude Code active. Sur 13 vraies sessions, une seule avait un nom choisi. Les autres n'avaient qu'un titre généré automatiquement, et une n'en avait aucun.
- **Options** : afficher le titre généré ; renommer chaque session avec `/rename`.
- **Décision** : je renomme chaque session avec le nom du ticket. Le titre généré, puis le nom du dossier, servent de secours.
- **Conséquences** : la tuile « Une seule chose » et la page Réunions affichent le bon ticket. Ça me demande une habitude, mais si j'oublie, l'écran affiche quand même quelque chose.

## D10 · Session active : dernier message plutôt que date du fichier · 24/11/2026
- **Contexte** : le plan considérait comme active la session dont le fichier a été modifié il y a moins de 15 minutes. Sur les vrais fichiers, Claude Code continue d'y écrire longtemps après le dernier message : jusqu'à 9 jours d'écart.
- **Options** : date du fichier seule ; heure du dernier message.
- **Décision** : une session est active si son dernier message date de moins de 15 minutes. La date du fichier ne sert qu'à trier vite.
- **Conséquences** : une session terminée ne s'affiche plus comme en cours. Le service doit lire les horodatages dans des fichiers qui vont jusqu'à 28 Mo, donc il ne lira que les lignes ajoutées depuis sa dernière lecture.

## D11 · .NET 10 plutôt que .NET 8 · 24/11/2026
- **Contexte** : le plan visait .NET 8 « ou plus récent ».
- **Options** : .NET 8 ; .NET 10.
- **Décision** : .NET 10.
- **Conséquences** : .NET 8 n'est plus maintenu depuis le 10 novembre 2026, alors que .NET 10 l'est jusqu'en novembre 2028. La version du SDK est fixée dans `global.json`. Pour essayer le projet, il faut installer .NET 10.

## D12 · Mode console dans le service plutôt qu'une appli à part · 24/11/2026
- **Contexte** : la phase 1 prévoyait une application console qui affiche les stats du PC et le morceau Spotify, pour vérifier ces lectures avant de construire l'écran.
- **Options** : un projet console séparé ; une option `--console` du service.
- **Décision** : l'option `--console` du service.
- **Conséquences** : la console et l'écran lisent les stats et Spotify avec le même code, il n'y a qu'un projet à maintenir. La console reste utile pour tester sans la tablette.

## D13 · Spotify par les contrôles média de Windows · 24/11/2026
- **Contexte** : l'écran doit afficher le morceau en cours et piloter la lecture.
- **Options** : l'API Web Spotify ; les contrôles média de Windows, où l'appli Spotify pour PC s'affiche déjà.
- **Décision** : les contrôles média de Windows.
- **Conséquences** : ni compte développeur, ni connexion OAuth, ni Premium, et ça marche sans internet. En contrepartie, aimer un morceau ou choisir l'appareil n'est pas possible. L'API Web pourra s'ajouter plus tard si je le demande.

## D14 · Service limité à ce PC par défaut · 24/11/2026
- **Contexte** : la tablette joint le PC par le Wi-Fi, mais un service ouvert au réseau est joignable par tous les appareils connectés, y compris sur un Wi-Fi public.
- **Options** : écouter sur le réseau dès l'installation ; n'écouter que sur ce PC, et ouvrir au réseau par un réglage.
- **Décision** : ce PC seulement par défaut. Le réglage `Server:ListenOnNetwork` ouvre le service à la tablette.
- **Conséquences** : une installation neuve n'expose rien. Pour la tablette, il faudra activer ce réglage, classer le Wi-Fi en « Privé » et autoriser le port 5080 dans le pare-feu pour le profil Privé seulement.

## D15 · Jeton donné une fois dans l'adresse, puis gardé dans un cookie · 24/11/2026
- **Contexte** : la tablette n'a pas de compte. Il faut empêcher un autre appareil du réseau de lire l'écran ou de piloter le PC.
- **Options** : aucune protection ; le jeton dans chaque adresse ; le jeton donné une fois, puis gardé par le navigateur.
- **Décision** : le jeton est donné à la première ouverture (`?k=`), puis gardé dans un cookie inaccessible aux scripts et limité au site, valable 400 jours et renouvelé à chaque appel. Les commandes envoyées depuis un autre site sont refusées.
- **Conséquences** : l'adresse affichée est nettoyée tout de suite. L'adresse tapée avec le jeton peut quand même rester dans les suggestions du navigateur de la tablette. Pour les hooks de Claude Code (phase 4c), il restera à décider s'ils utilisent le même jeton et si le service n'en garde que l'empreinte.

## D16 · Agenda lu par le PC avec l'adresse iCal secrète · 24/11/2026
- **Contexte** : le prototype lisait l'agenda par le connecteur Claude, qui ne fonctionne que dans l'appli Claude.
- **Options** : le connecteur Claude ; l'API Google Agenda avec une connexion OAuth ; l'adresse secrète au format iCal.
- **Décision** : l'adresse secrète iCal, téléchargée par le PC quand l'écran la demande, au plus toutes les 5 minutes, ou chaque minute après un échec.
- **Conséquences** : aucun compte Google à connecter, et l'écran fonctionne sans Claude. Cette adresse donne accès à tout l'agenda : elle reste dans les user-secrets ou le fichier local, n'est jamais écrite dans les journaux, et devra être réinitialisée dans Google Agenda si elle fuit. Si le téléchargement échoue, l'écran garde la dernière copie lisible pendant un jour au plus et affiche son heure de lecture.

## D17 · Une branche par phase, relue par deux agents · 24/11/2026
- **Contexte** : il fallait fixer comment le code arrive dans `main`.
- **Options** : une branche par fonctionnalité ; une branche par phase.
- **Décision** : une branche par phase, créée depuis `main`. Une PR, relue par deux agents qui challengent le code, puis les corrections, puis une fusion par commit de fusion. Les branches de phase sont gardées. Une petite suite entre deux phases a sa propre branche, avec le même circuit (par exemple `logs-and-fonts`).
- **Conséquences** : chaque phase arrive d'un bloc, avec ses relectures et leurs corrections visibles dans la PR. Le découpage par fonctionnalité était trop fin : on aurait dit des tâches, pas des user stories.

## D18 · Journaux du service dans un fichier, avec Serilog · 24/11/2026
- **Contexte** : le plan prévoit des journaux dans un fichier tournant sous `%LOCALAPPDATA%\EdgeHub\logs`, pour comprendre un problème après coup. Le logging intégré de .NET n'écrit pas dans un fichier.
- **Options** : écrire un petit fournisseur maison ; Serilog, une bibliothèque répandue et maintenue.
- **Décision** : Serilog, avec un fichier par jour, gardé 14 jours, et 1 Go au plus en tout.
- **Conséquences** : une dépendance de plus, mais connue. Les règles actuelles s'appliquent aussi au fichier : ni l'adresse iCal, ni le jeton, ni l'adresse des appareils qui se connectent. Le fichier applique lui-même le filtre des adresses, quels que soient les réglages.

## D19 · Polices servies par le PC · 24/11/2026
- **Contexte** : l'écran charge ses polices, Chakra Petch et IBM Plex Sans, depuis Google Fonts.
- **Options** : garder Google Fonts ; servir les fichiers depuis le PC.
- **Décision** : les servir depuis le PC.
- **Conséquences** : l'écran s'affiche pareil sans internet, et la tablette ne contacte plus Google à chaque ouverture. Les deux polices sont sous licence libre (SIL Open Font License) : leurs fichiers peuvent aller dans le dépôt, avec la licence. IBM réserve le nom « Plex » : seuls ses fichiers d'origine, non modifiés, gardent ce nom, donc ceux d'IBM Plex Sans viennent d'IBM et pas de Google Fonts.

## D20 · La tâche venue de Claude Code démarre à son arrivée sur l'écran · 25/11/2026
- **Contexte** : « Une seule chose » se remplit avec le nom de la session Claude Code active. Une session peut durer toute une journée : celle de la phase 4 avait commencé la veille.
- **Options** : compter le temps depuis le premier message de la session ; le compter depuis l'arrivée de la tâche sur l'écran.
- **Décision** : depuis l'arrivée de la tâche sur l'écran. La page Réunions garde, elle, la durée de la session.
- **Conséquences** : le rappel de pause garde son sens et ne sonne pas dès l'ouverture de l'écran. Si l'écran ne voit pas la session pendant 10 minutes (écran éteint, rechargement), la tâche peut repartir de zéro.

## D21 · Relire toutes les 30 secondes plutôt que surveiller les fichiers · 25/11/2026
- **Contexte** : le plan prévoyait de surveiller le fichier du journal pour mettre à jour la page Réunions dès qu'il change.
- **Options** : surveiller les fichiers et prévenir l'écran à chaque changement ; relire à intervalle régulier.
- **Décision** : l'écran demande le journal et la session toutes les 30 secondes. Le PC ne relit le journal que si sa date ou sa taille a changé, et cherche la session active toutes les 30 secondes en tâche de fond.
- **Conséquences** : plus simple et plus robuste, puisqu'aucun changement ne peut être manqué. En contrepartie, la page se met à jour en 30 secondes au plus après un `/prep-reunions`.

## D22 · Journal lu d'après la définition des commandes, en attendant un vrai fichier · 25/11/2026
- **Contexte** : le plan demande de s'aligner sur de vrais fichiers. Mon seul journal réel ne contenait qu'un bloc `/rapport` : `/prep-reunions` et `/decisions` n'avaient encore rien écrit.
- **Options** : attendre un vrai fichier avant de coder la lecture ; lire d'après le format défini dans les commandes, en restant tolérant.
- **Décision** : lire d'après les commandes, de façon tolérante : titres reconnus par leur début, sous-puces, niveaux de titres variables, états en gras.
- **Conséquences** : la page Réunions marche dès le premier `/prep-reunions`. Le format sera confirmé sur mon premier vrai fichier, puis `docs/formats/journal.md` mis à jour. Quelques mises en forme hors définition, comme un rapport sans heure ou les décisions du jour écrites dans la préparation, ne sont pas lues.

## D23 · Mesurer les indicateurs à partir de ce que l'écran voit vraiment · 26/11/2026
- **Contexte** : `indicateurs.md` fixe les indicateurs, mais trois définitions restaient à rendre mesurables : une tâche terminée, une préparation faite avant le daily, un rappel de pause respecté.
- **Options** : des gestes dédiés sur l'écran (un bouton « Pause prise », une saisie de l'heure de préparation) ; des mesures tirées de ce que l'écran et le PC voient déjà.
- **Décision** : une tâche est terminée quand je touche « Terminé ». Elle a son rapport si un `/rapport` du même jour porte le même ticket, ou le même titre. « Avant le daily » compare l'heure à laquelle le PC voit la préparation pour la première fois à l'heure du daily réglée dans la config. Un rappel est suivi si une pause, une tâche terminée ou un changement de tâche arrive dans les 10 minutes.
- **Conséquences** : aucun geste de plus. En contrepartie, la mesure ne voit que ce qui passe par l'écran et le journal, et un jour où le PC n'a pas vu la préparation arriver reste « inconnu » pour l'avant-daily.

## D24 · Un jour en cours n'est pas un jour manqué · 26/11/2026
- **Contexte** : le matin, le jour même n'est encore ni tenu ni manqué. Le compter dès minuit affichait « 0/1 » le lundi matin et faisait baisser les ratios toute la matinée.
- **Options** : compter le jour même dès le matin ; ne le compter qu'une fois acquis.
- **Décision** : le jour même n'entre dans un ratio, des deux côtés, qu'une fois acquis, ou une fois qu'il ne peut plus l'être (l'heure du daily passée pour la préparation, l'heure de la séance passée pour le sport). Sur aucun jour, la tuile affiche « — ».
- **Conséquences** : les chiffres du matin ne sont pas tirés vers le bas, mais le dénominateur peut changer au cours de la journée.

## D25 · Aucun événement perdu ni compté deux fois · 26/11/2026
- **Contexte** : l'écran envoie ses événements au PC, qui peut dormir ou être injoignable. Une réponse perdue fait renvoyer un événement.
- **Options** : envoyer et oublier ; garder sur la tablette ce qui n'est pas parti et le renvoyer avec un identifiant.
- **Décision** : les événements non envoyés attendent sur la tablette, avec leur heure d'origine et un identifiant, et repartent plus tard. Le PC garde chaque identifiant une seule fois et refuse les événements de plus de 8 jours.
- **Conséquences** : les mesures restent justes quand le PC dort. La tablette garde quelques jours d'événements, textes des tâches compris, jusqu'à leur envoi.
