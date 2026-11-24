# Backlog

Chaque story deviendra une issue GitHub, rattachée au milestone de sa version. Priorités : **M** Must, **S** Should, **C** Could, **W** Won't (pour cette version).

## E1 · Socle et confiance
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-101 | En tant que développeuse, je veux que le service démarre avec une route `/health`, afin de savoir d'un coup d'œil s'il tourne. | M | 0 |
| US-102 | En tant que mainteneuse, je veux une CI qui compile et teste chaque push, afin de ne jamais casser `main` sans le voir. | M | 0 |
| US-103 | En tant qu'utilisatrice, je veux que mes valeurs perso restent hors de git, afin de publier le dépôt sans risque. | M | 0 |
| US-104 | En tant qu'utilisatrice, je veux que la tablette n'accède au service qu'avec un jeton, afin que personne d'autre sur le réseau ne le pilote. | M | 2 |

**Critères US-104**
- Étant donné une requête `/api/*` sans jeton valide, quand elle arrive, alors le service répond 401.
- Étant donné l'URL avec `?k=<jeton>` ouverte une fois, quand je recharge la page, alors je reste connectée (cookie).

## E2 · L'écran sur la tablette
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-201 | En tant qu'utilisatrice, je veux ouvrir l'écran sur la tablette depuis le PC, afin de ne plus dépendre de l'appli Claude. | M | 2 |
| US-202 | En tant qu'utilisatrice, je veux que les données se mettent à jour seules, afin de ne jamais avoir à recharger. | M | 2 |
| US-203 | En tant qu'utilisatrice, je veux voir « PC hors ligne » sans perdre l'affichage, afin de comprendre ce qui se passe. | S | 2 |
| US-204 | En tant qu'utilisatrice, je veux passer d'une page à l'autre en glissant, afin d'avoir plus d'infos sans surcharger l'écran. | S | 4 |

**Critères US-203**
- Étant donné le service arrêté, quand la tablette perd la connexion, alors les dernières données restent affichées et un indicateur « PC hors ligne » apparaît en moins de 10 s.

## E3 · Agenda et sport
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-301 | En tant qu'utilisatrice, je veux voir ma séance de sport du jour tirée de mon agenda, afin de ne rien saisir à la main. | M | 3 |
| US-302 | En tant qu'utilisatrice, je veux être prévenue 30 min avant ma séance, afin d'avoir le temps de me préparer. | M | 3 |
| US-303 | En tant qu'utilisatrice, je veux qu'une séance annulée soit affichée comme telle, afin de ne pas me préparer pour rien. | M | 3 |
| US-304 | En tant qu'utilisatrice, je veux voir mon prochain événement sous l'horloge, afin de garder la notion du temps. | S | 3 |

**Critères US-301 à US-303**
- Étant donné un événement « 🏃 … » à 12:15, quand j'ouvre l'écran le matin, alors la ligne Sport affiche « Course 12:15 ».
- Étant donné l'heure 11:45, quand la ligne se met à jour, alors elle passe en orange avec le message de préparation, et un seul bip retentit.
- Étant donné un événement « ❌ Full B annulée … », quand c'est la seule séance du jour, alors la ligne affiche « Pas de séance aujourd'hui » suivi du titre.

## E4 · Réunions et journal
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-401 | En tant que développeuse, je veux voir ma préparation du daily sur la tablette, afin de la lire en 2 minutes avant la réunion. | M | 4 |
| US-402 | En tant que développeuse, je veux voir les points techniques à soulever, afin de ne rien oublier en réunion technique. | M | 4 |
| US-403 | En tant que développeuse, je veux voir l'état des décisions d'hier, afin d'en assurer le suivi. | S | 4 |
| US-404 | En tant que développeuse, je veux que la tâche en cours vienne de ma session Claude Code, afin de ne pas la retaper. | S | 4 |

**Critères US-401**
- Étant donné un fichier du jour contenant `# Préparation du …`, quand j'ouvre la page Réunions, alors je vois une ligne par ticket avec « fait », « reste » et « testable ».
- Étant donné aucun fichier du jour, alors la page affiche « Lance /prep-reunions dans Claude Code ».

## E5 · Claude Code sur la tablette
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-501 | En tant que développeuse, je veux autoriser ou refuser une commande de Claude Code depuis la tablette, afin de ne pas surveiller VS Code. | M | 4c |
| US-502 | En tant que développeuse, je veux être prévenue quand Claude Code a fini ou attend une réponse, afin de reprendre au bon moment. | S | 4c |
| US-503 | En tant que développeuse, je veux que les commandes risquées ne puissent pas être autorisées depuis la tablette, afin de garder la main sur l'irréversible. | M | 4c |

**Critères US-501 et US-503**
- Étant donné une demande pour `git push origin main`, quand j'appuie sur Autoriser, alors Claude Code continue sans que je touche au PC.
- Étant donné aucune réponse dans le délai, alors la question revient normalement dans VS Code.
- Étant donné une demande pour `git push --force`, alors la carte est rouge et n'a pas de bouton Autoriser.

## E6 · Concentration
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-601 | En tant qu'utilisatrice, je veux un minuteur focus avec pauses, afin de travailler par blocs. | M | déjà là |
| US-602 | En tant qu'utilisatrice, je veux un rappel de pause quand je reste trop longtemps sur la même tâche, afin d'éviter l'hyperfocus. | M | déjà là |
| US-603 | En tant qu'utilisatrice, je veux une mascotte qui réagit à ce qui se passe, afin que l'écran attire mon regard au bon moment, sans stress. | S | 4c |
| US-604 | En tant qu'utilisatrice, je veux voir mes indicateurs de la semaine, afin de constater ce que j'ai fait. | S | 4b |

## E7 · Musique et PC
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-701 | En tant qu'utilisatrice, je veux voir et piloter Spotify depuis la tablette, afin de ne pas changer de fenêtre. | S | 5 |
| US-702 | En tant qu'utilisatrice, je veux lancer ma playlist de concentration avec le focus, afin d'entrer plus vite dans le travail. | C | 5 |
| US-703 | En tant qu'utilisatrice, je veux voir CPU, RAM et réseau, afin de repérer un PC qui rame. | C | 5 |
| US-704 | Températures CPU et GPU. | W | — (D3) |

## E8 · Installation et publication
| ID | Story | Prio | Phase |
|---|---|---|---|
| US-801 | En tant qu'utilisatrice, je veux que tout redémarre seul avec le PC, afin de ne plus y penser. | M | 6 |
| US-802 | En tant que visiteuse du dépôt, je veux essayer le projet en mode démo, afin de le découvrir sans mes données. | S | 7 |
| US-803 | En tant que visiteuse du dépôt, je veux un README clair avec des captures, afin de comprendre et installer le projet. | M | 7 |
