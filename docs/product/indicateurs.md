# Indicateurs

Mesurés **en local uniquement** par Edge Hub (fichier `%LOCALAPPDATA%\EdgeHub\events.jsonl`), jamais envoyés ailleurs. On peut désactiver la mesure dans la config.

## Indicateur principal
**Jours « tenus » par semaine** : un jour de travail compte si la préparation des réunions a été faite et si chaque tâche terminée a son rapport.

## Indicateurs secondaires
| Indicateur | Source | Objectif v1 |
|---|---|---|
| Préparation des réunions faite avant le daily | journal (`# Préparation du …` daté du jour) | 4 jours sur 5 |
| Tâches avec un rapport | journal (blocs `/rapport`) | au moins 1 par jour travaillé |
| Sessions focus terminées | événements de l'écran | à observer, pas d'objectif au départ |
| Séances de sport faites / prévues | bouton « Séance faite » + agenda | ≥ 80 % |
| Rappels de pause respectés | événements de l'écran | à observer |

## Point de départ
Avant la mise en service, noter pendant une semaine, à la main : combien de dailys préparés à l'avance, combien de séances de sport faites sur celles prévues. C'est la base de comparaison.

## Suivi
Une route `GET /api/metrics/week` et une petite tuile « Ma semaine » sur l'écran. Bilan chaque vendredi, noté dans le journal des décisions si ça mène à un changement.
