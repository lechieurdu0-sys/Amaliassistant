🧠 Amaliassistant

Amaliassistant est une application desktop Windows (WPF / .NET) conçue pour analyser en temps réel les logs de jeu et fournir des outils de suivi, d’analyse et de visualisation pendant une session de jeu.

Le projet a été pensé avec trois objectifs clairs :

📊 Donner une lecture fiable et claire de ce qui se passe en jeu

🧩 Être modulaire, extensible et maintenable dans le temps

🎓 Servir de support d’apprentissage sérieux pour la programmation et l’architecture logicielle

Ce n’est pas une usine à gaz, et ce n’est pas un prototype jetable.

🚀 Fonctionnalités principales (côté utilisateur)
⚔️ Kikimeter (analyse de combat)

Le Kikimeter est le cœur historique de l’application.

Il permet de :

détecter automatiquement l’entrée et la sortie de combat

identifier les joueurs présents (personnage principal, groupe, adversaires)

afficher les statistiques de combat en temps réel

maintenir un affichage stable pendant le combat

figer les résultats une fois le combat terminé

👉 Le comportement est volontairement prévisible :

en combat → affichage dynamique

hors combat → affichage figé jusqu’au prochain combat

👥 Gestion des joueurs

L’application est capable de :

détecter automatiquement les joueurs via les logs ou via un fichier JSON

identifier le personnage principal

gérer un groupe limité (6 joueurs max)

conserver l’ordre de tour

gérer les changements de serveur (reset propre et sécurisé)

Tout est fait pour éviter :

les resets intempestifs

les disparitions de joueurs en plein combat

les incohérences d’état

🎒 Fenêtre de loot (session complète)

La fenêtre de loot permet de répertorier tous les objets obtenus sur une session de jeu, indépendamment des combats.

Fonctionnement :

chaque loot détecté est ajouté à la liste

si l’objet existe déjà → la quantité est incrémentée

la liste ne se reset jamais automatiquement

les suppressions sont manuelles et définitives

⭐ Système de favoris

un item peut être marqué comme favori (étoile)

les favoris remontent en haut de la liste

un item favori ne peut pas être supprimé

idéal pour suivre des drops importants

⚙️ Paramètres et configuration

sélection du personnage principal

gestion de l’affichage

comportement stable même après redémarrage de combat

aucune action destructrice automatique

🧩 Architecture générale (pour les curieux et les devs)
🧱 Philosophie

Amaliassistant repose sur quelques principes forts :

une seule source de vérité par système

pas de logique métier dans l’UI

séparation claire des responsabilités

logs détaillés pour comprendre ce qui se passe

Le projet est volontairement découpé en services plutôt qu’en “gros managers magiques”.

🔌 Système de providers (lecture des données)

Les données joueurs peuvent venir de plusieurs sources :

LogParserPlayerDataProvider
→ lecture directe des logs (système historique, toujours fonctionnel)

JsonPlayerDataProvider
→ polling d’un fichier JSON externe (source de vérité moderne)

Un fallback automatique est prévu :

si le JSON est absent ou invalide → retour au LogParser

👉 Le reste de l’application ne dépend pas de la source des données.

🧠 PlayerManagementService

Service central chargé de :

synchroniser les joueurs

gérer les états (combat actif / hors combat)

nettoyer intelligemment les joueurs inactifs

protéger les resets (serveur, UI, loot)

Il garantit :

aucune suppression pendant un combat

aucun reset parasite

cohérence entre affichage, logique et données

🎒 LootManagementService

contient la collection de loot de session

unique source de vérité

aucune reconstruction depuis les logs

logique de favoris et de suppression protégée

La fenêtre de loot est passive :
elle observe, elle n’invente rien.

🧪 Logs & debug

Le projet contient de nombreux logs explicites :

ajout / suppression de joueurs

détection de combat

synchronisation JSON

ajout / incrément de loot

refus de suppression (favoris)

Objectif :
👉 comprendre un bug sans “deviner”.

🛠️ Pour les développeurs
Pourquoi ce projet est intéressant à lire

vraie application WPF, pas un tuto

gestion d’état complexe (combat / hors combat)

synchronisation de données temps réel

fallback propre entre plusieurs sources

bugs réels, corrigés méthodiquement

architecture pensée pour évoluer

Ce que le projet n’est pas

❌ un framework générique

❌ un code généré sans réflexion

❌ un prototype jetable

📌 État du projet

application fonctionnelle

en amélioration continue

utilisée comme terrain d’apprentissage sérieux

ouverte aux retours et aux tests

❤️ Mot de la fin

Amaliassistant est né d’un besoin réel, a grandi avec des contraintes réelles, et continue d’évoluer avec une exigence simple :

que le logiciel fasse exactement ce qu’il dit, ni plus, ni moins.

Si tu es utilisateur : explore.
Si tu es développeur : lis le code, il a des choses à dire.
