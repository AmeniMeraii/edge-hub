---
description: Prépare le daily et la réunion technique à partir du journal d'activité
---

Prépare mes deux réunions du matin, en français, à partir de mon journal d'activité dans `~/Documents/Journal-activite/`.

## Sources

- Le fichier du dernier jour travaillé avant aujourd'hui (le plus récent dont la date est antérieure à aujourd'hui), plus celui d'aujourd'hui s'il existe.
- La dernière section « Décisions de la réunion technique » trouvée dans les fichiers du journal, en remontant au plus 5 jours.
- Si tu es dans un dépôt git, les commits de ces jours-là pour compléter, sans rien inventer.

## Règles

- Factuel et court : je dois pouvoir le lire en 2 minutes.
- Regroupe par ticket. Si un même ticket apparaît plusieurs fois, fusionne.
- Si une information manque, écris « à compléter ».

## Format

# Préparation du {JJ/MM/AAAA}

## Daily (PO et testeurs)
- {numéro} {intitulé} · Fait : … · Reste : … · Testable : oui / non / quand

## Réunion technique
### Suivi des décisions précédentes
- {décision} → appliquée / en cours / pas commencée (d'après le journal)

### Mes tickets, côté technique
- **{numéro} {intitulé}** : le choix principal en 2 phrases (quoi, pourquoi, impact).

### À soulever
- Questions et blocages, du plus urgent au moins urgent.
- Décisions que je dois faire valider.

## Enregistrement

Affiche la préparation, puis ajoute-la au début du fichier d'aujourd'hui `~/Documents/Journal-activite/AAAA-MM-JJ.md`, juste sous le titre. Crée le fichier s'il n'existe pas, avec le titre `# Journal du JJ/MM/AAAA`. Ne modifie rien d'autre.
