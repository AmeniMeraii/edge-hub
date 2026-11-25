# Activity journal: format

The meetings page reads the activity journal written by three optional Claude Code commands (`integrations/claude-code/commands/`): `/rapport`, `/prep-reunions` and `/decisions`. They write in French, so the headings and keys below are French.

All examples on this page are invented (tickets `DEMO-101` and `DEMO-102`, project "Bookshelf").

## What was observed, and what was not yet

- **`/rapport` blocks: observed** on one real file (November 2026). The block matches the command's format exactly.
- **The `/prep-reunions` block and the `/decisions` block: not observed yet.** No real file had them when the reader was written, so the reader follows the format the commands define, and is tolerant (see below). To be confirmed on the first real files, and this page updated.

## Where the files are

```
%USERPROFILE%\Documents\Journal-activite\
  2026-11-25.md        one file per day, named after the PC's local date
```

The commands write to `~/Documents/Journal-activite/`; the real file observed was in `%USERPROFILE%\Documents\Journal-activite`. The folder is the `Journal:Folder` setting.

## A day's file

```markdown
# Journal du 25/11/2026            ← title, written by whichever command creates the file

# Préparation du 25/11/2026        ← /prep-reunions, inserted just below the title (so the latest one is first)
## Daily (PO et testeurs)
## Réunion technique
### Suivi des décisions précédentes
### Mes tickets, côté technique
### À soulever

## 09:10 · DEMO-101 · Export CSV   ← /rapport, appended at the end, one block per task

## Décisions de la réunion technique   ← /decisions, appended at the end
```

The preparation's sections are `##` headings, like the report blocks: the preparation ends at the first `#` or `##` heading that is not one of its own.

### Preparation (`/prep-reunions`)

```markdown
# Préparation du 25/11/2026

## Daily (PO et testeurs)
- DEMO-101 Export CSV de la bibliothèque · Fait : colonnes auteur et ISBN · Reste : tri par date d'ajout · Testable : oui
- DEMO-102 Recherche par auteur · Fait : index créé · Reste : à compléter · Testable : jeudi

## Réunion technique
### Suivi des décisions précédentes
- Paginer l'API des livres par curseur → appliquée
- Mettre le cache des couvertures derrière un flag → en cours (d'après le journal)

### Mes tickets, côté technique
- **DEMO-101 Export CSV de la bibliothèque** : écriture en flux, pour ne pas charger toute la bibliothèque en mémoire.

### À soulever
- Le PO veut aussi un export Excel : à arbitrer.
```

- A daily line is `{ticket} {title} · Fait : … · Reste : … · Testable : …`. The ticket is the first word.
- A followed-up decision ends with `→` and its state: `appliquée`, `en cours` or `pas commencée`, sometimes followed by a remark.
- A missing piece of information is written « à compléter ».

### Report (`/rapport`)

```markdown
## 09:10 · DEMO-101 · Export CSV de la bibliothèque

**1. Problématique**
…
**Pour le daily**
- DEMO-101 Export CSV · Fait : … · Reste : …

**Pour la réunion technique**
- À expliquer : …
```

The meetings page does not read the reports: they are the material `/prep-reunions` works from the next morning. Their `Pour le daily` lines look like daily lines, which is one more reason the preparation must end at the report's heading.

### Meeting decisions (`/decisions`)

```markdown
## Décisions de la réunion technique
- L'export Excel attendra la version 2 · Qui : PO · Pour quand : à préciser
```

## What Edge Hub reads

| Block | Read | Shown on the meetings page |
|---|---|---|
| Preparation (the first one of the day) | daily lines (ticket, title, done, remaining, testable), followed-up decisions (decision, state), technical choices (ticket, text), points to raise | Daily, Réunion technique (points to raise in orange), Décisions d'hier |
| Meeting decisions (every block of the day) | decision, who, when | Décisions du jour, with or without a preparation |
| Reports | nothing | nothing |

Without a file for the day, or without a preparation in it, the page says « Lance /prep-reunions dans Claude Code ».

## Tolerance

The blocks are written by a model, so the reader:

- matches headings by their start, whatever the case, the accents and a symbol in front (`## daily`, `## Réunion Technique`, `### 📋 Daily`, `### Suivi des décisions`, `### Points à soulever`);
- accepts a preparation written one level lower (`## Préparation du …`, as « just below the title » can be read), with its sections one level below it or at its own level. It ends at a heading of its level or above that is not one of its sections, at a report (`## 09:10 · …`), or at the day's decisions written at its sections' level; an unknown heading inside it only opens no section;
- keeps a heading deeper than its section inside that section: a ticket written as a heading (`### DEMO-101 Export`) with its fields as bullets below it is one daily line, or one technical choice, and a heading inside the day's decisions only groups them;
- accepts `-`, `*`, `+` and numbered bullets, `·` or `•` between fields, any space before `:` (French typography uses a no-break space), and `→`, `->` or `=>` before a state, in bold or after a symbol (`→ **appliquée**`, `→ ✅ en cours`);
- reads a bullet more indented than the one above it as its continuation, so a ticket whose fields come as sub-bullets stays one daily line; under « À soulever », each sub-bullet stays its own point, and a line that only introduces them (« Questions et blocages : ») is dropped;
- ignores separators (`* * *`) and anything inside a code block; a code block never closed hides nothing;
- keeps a state it does not know as written (`→ abandonnée`), and a line whose fields are missing;
- skips anything else, so a missing section gives an empty list, never an error;
- reads the file again only when its write time or size changes, and refuses a file over 1 MB.

## Privacy

The real journal holds work information: ticket numbers and titles, technical choices, decisions. It never goes in git; the tests read an invented day (`tests/EdgeHub.Tests/Fixtures/journal/`). The service sends the screen the preparation and the decisions, which is the point of the page, over the local network and behind the access token.
