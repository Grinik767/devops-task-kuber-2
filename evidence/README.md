# Результаты проверок

| Файл | Содержимое |
|---|---|
| [01-build-push.txt](01-build-push.txt) | Сборка и push приложения и клиента |
| [02-infra.txt](02-infra.txt) | Исходные Pod и endpoints |
| [03-exp1-liveness.txt](03-exp1-liveness.txt) | HTTP и Pod до и после отказа liveness |
| [04-exp1-diagnostics.txt](04-exp1-diagnostics.txt) | События kubelet и логи предыдущего запуска |
| [05-exp1-client.txt](05-exp1-client.txt) | Клиент до, во время и после отказа liveness |
| [06-exp2-readiness.txt](06-exp2-readiness.txt) | HTTP, Pod и endpoints в трёх состояниях readiness |
| [07-exp2-client.txt](07-exp2-client.txt) | Клиент при выключении и восстановлении readiness |
| [08-exp3-rollout.txt](08-exp3-rollout.txt) | Deployment, ReplicaSet и Pod при обновлении |
| [09-exp3-client.txt](09-exp3-client.txt) | Клиент до, во время и после обновления |
| [10-final-state.txt](10-final-state.txt) | Итоговое состояние приложения и клиента |

Снимки для проверки: [EndpointSlice](06-readiness-endpoints.yaml) и [Deployment](08-rollout-deployment.yaml).
