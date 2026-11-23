---
description: Rapport de fin de tâche (problématique / pourquoi / choix de résolution), ajouté au journal d'activité du jour
argument-hint: [numéro ou nom du ticket, facultatif]
---

Rédige le rapport de la tâche travaillée dans cette session, en français, à partir de la conversation, des fichiers modifiés, des commandes lancées et de l'historique git de la branche.

Ticket : $ARGUMENTS
S'il est vide, déduis le numéro et l'intitulé du nom de la session ou de la branche git.

## Règles

- Factuel : constate ce qui a été fait. Pas de jugement sur moi, pas d'excuses, pas de « enfin ».
- Concis : 2 à 5 lignes par partie.
- Si une information manque (par exemple la raison d'un choix), écris « à compléter » au lieu d'inventer.
- Termes techniques précis : composant, service, endpoint, requête SQL, table, test.

## Format exact du bloc

## {HH:mm} · {numéro du ticket} · {intitulé}

**1. Problématique**
Le besoin ou le problème traité, d'abord côté métier, puis côté technique.

**2. Pourquoi c'est un problème**
L'impact : pour qui (utilisateur, client, équipe) et quel risque (bug, performance, données, sécurité, dette technique).

**3. Choix de résolution**
La solution retenue, les alternatives écartées et pourquoi, les fichiers ou zones modifiés, comment c'est testé, ce qui reste à faire.

**Ce que j'ai mis en place**
- Liste concrète des efforts et solutions : tests ajoutés, requête optimisée, cas limite traité, point clarifié avec le PO, documentation écrite…

**Pour le daily**
- {numéro} {intitulé} · Fait : … · Reste : …

**Pour la réunion technique**
- À expliquer : le choix technique principal en 2 phrases (quoi, pourquoi, impact sur le reste du code).
- Questions ou blocages à soulever : … (ou « aucun »)
- Décisions à faire valider par l'équipe : … (ou « aucune »)

## Enregistrement

1. Ajoute ce bloc à la fin du fichier `~/Documents/Journal-activite/AAAA-MM-JJ.md`, avec la date du jour.
2. Si le dossier ou le fichier n'existe pas, crée-les. Le fichier commence par le titre `# Journal du JJ/MM/AAAA`.
3. N'écrase et ne modifie jamais un contenu déjà présent : ajoute seulement à la fin.
4. Affiche-moi le bloc ajouté et le chemin du fichier.
