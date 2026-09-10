Feature: Seeding the class catalogue
  A fresh deployment ships the frozen seed corpus inside the application and
  publishes it at startup through the ordinary publish command, so a new
  store can adopt a competition class without a manual publishing step
  (kanban/in-progress/seed-class-corpus-at-startup.md). The host under test
  was pointed at the repo's canonical corpus before it started — the same
  wiring the Docker image gets with /app/seed.

  Scenario: A fresh store starts with the seed classes in its catalogue
    Given the class corpus the application ships
    When the class catalogue is queried
    Then every seed class is listed under its content hash
