# Côté produit

Ce dossier rassemble le travail de PM/PO sur Edge Hub : pourquoi on construit, quoi, dans quel ordre, et comment la réflexion a évolué. Le code dit *comment* ; ces documents disent *pourquoi*.

## Les documents

| Document | Question à laquelle il répond | Mis à jour |
|---|---|---|
| [fiche-produit.md](fiche-produit.md) | Quel problème, pour qui, quelle valeur, quoi est hors périmètre ? | à chaque fin de phase si ça change |
| [roadmap.md](roadmap.md) | Dans quel ordre, et qu'est-ce qui sort dans chaque version ? | à chaque fin de phase |
| [backlog.md](backlog.md) | Quelles user stories, avec quelle priorité et quels critères d'acceptation ? | en continu |
| [journal-des-decisions.md](journal-des-decisions.md) | Qu'a-t-on décidé, pourquoi, et qu'est-ce que ça implique ? | à chaque décision |
| [journal-de-bord.md](journal-de-bord.md) | Comment la réflexion a évolué, étape par étape ? | à chaque étape marquante |
| [indicateurs.md](indicateurs.md) | Comment sait-on que le produit marche ? | phase 4b, puis chaque semaine |
| [retours-utilisateurs.md](retours-utilisateurs.md) | Qu'ont dit les testeurs, et qu'a-t-on changé ? | phase 6b |
| [revue-de-phase.md](revue-de-phase.md) | Modèle de bilan à remplir à la fin de chaque phase | à chaque fin de phase |
| [etude-de-cas.md](etude-de-cas.md) | Le résumé d'une page pour présenter le projet | phase 7 |

## Rôles
- **PM / PO (Ameni)** : définit le problème, priorise, écrit les user stories et leurs critères, accepte ou refuse chaque livraison, tient les journaux.
- **Développeur (Claude Code)** : propose des solutions techniques, développe, teste, propose des entrées pour le journal des décisions. Ne rédige pas les documents produit à la place de la PM.

## Comment on travaille

### Le cycle d'une phase
1. **Cadrage** : la PM choisit les stories de la phase dans le backlog (milestone GitHub).
2. **Prêt ?** : chaque story respecte la *Definition of Ready* ci-dessous.
3. **Développement** : une branche par phase, une PR par story ou groupe de stories.
4. **Revue** : la PM teste sur la vraie tablette et vérifie chaque critère d'acceptation.
5. **Bilan** : la PM remplit une [revue de phase](revue-de-phase.md), les décisions vont au journal, la roadmap est ajustée.

### Definition of Ready (une story peut être développée si…)
- elle suit le format « En tant que …, je veux …, afin de … » ;
- elle a des critères d'acceptation vérifiables (Étant donné / Quand / Alors) ;
- sa priorité (MoSCoW) et sa phase sont fixées ;
- les questions ouvertes sont tranchées, ou notées comme hypothèses.

### Definition of Done (une story est terminée si…)
- tous ses critères d'acceptation passent, vérifiés par la PM ;
- la logique pure est couverte par des tests xUnit, la CI est verte ;
- aucune donnée perso ni professionnelle n'est entrée dans git ;
- le CHANGELOG est à jour, et la doc aussi si le comportement change ;
- les décisions prises en route sont dans le journal.

### Conventions
- **Priorités MoSCoW** : *Must* (sans ça, la version n'a pas de sens), *Should* (important, mais on peut livrer sans), *Could* (bonus), *Won't* (exclu pour cette version, gardé pour plus tard).
- **Identifiants** : `E1`… pour les epics, `US-101`… pour les stories (centaine = epic), `D1`… pour les décisions. Les issues GitHub reprennent l'identifiant dans leur titre.
- **Traçabilité** : une PR cite les stories qu'elle livre (`Closes #12`) ; une décision cite la story ou la phase qui l'a déclenchée.
- **On ne réécrit pas l'histoire** : une décision dépassée n'est pas supprimée, on ajoute une nouvelle entrée qui la remplace (« remplace D5 »). Le journal de bord garde la trace des changements de cap.
