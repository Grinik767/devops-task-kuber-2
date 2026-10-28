# Пробы и обновление приложения в Kubernetes

| Параметр | Значение |
|---|---|
| ФИО | Киселев Григорий Александрович |
| Группа | ФТ-402-2 |
| ОС | Windows, WSL2 |
| Кластер | kind `hosting-lab` |
| Registry | `localhost:5001` |
| Namespace | `homework-2` |
| Версии приложения | `kiselev-homework:hw2-v1`, `kiselev-homework:hw2-v2` |
| Сетевой вариант | client → Service → Pod |

Использовал приложение из первой домашней работы на C# (.NET 10). На `/` оно возвращает сообщение, имя Pod и версию. Версия записывается в файл внутри образа при сборке.

## Сборка и развёртывание

Все команды запускал из корня проекта.

```bash
docker build --build-arg APP_VERSION=hw2-v1 -t localhost:5001/kiselev-homework:hw2-v1 .
docker push localhost:5001/kiselev-homework:hw2-v1
docker build -t localhost:5001/homework-client:hw2-v1 ./client
docker push localhost:5001/homework-client:hw2-v1

kubectl --context kind-hosting-lab apply -f k8s/namespace.yaml
kubectl --context kind-hosting-lab apply -f k8s/configmap.yaml -f k8s/configmap-client-direct.yaml -f k8s/service.yaml -f k8s/deployment.yaml
kubectl --context kind-hosting-lab -n homework-2 rollout status deployment/hw2-app --timeout=300s
kubectl --context kind-hosting-lab apply -f k8s/deployment-client.yaml
kubectl --context kind-hosting-lab -n homework-2 rollout status deployment/client --timeout=180s
```

Результаты сборки и push: [01-build-push.txt](evidence/01-build-push.txt). Исходные три готовые реплики и endpoints: [02-infra.txt](evidence/02-infra.txt).

Service `hw2-app` имеет тип ClusterIP, порт 80 и направляет запросы на порт 8080 контейнеров с меткой `app=hw2-app`. В ConfigMap клиента указал `WEB_SERVICE_HOST=hw2-app.homework-2.svc.cluster.local` и `WEB_SERVICE_PORT=80`, подключил через `envFrom`.

Для каждого контейнера указал ресурсы:

| Ресурс | requests | limits |
|---|---|---|
| CPU | 100m | 500m |
| RAM | 64Mi | 256Mi |
| ephemeral-storage | 64Mi | 256Mi |

## Пробы

Первые 10 секунд после запуска `/live` и `/ready` возвращают 503, затем 200. `/you-are-death` включает отказ liveness до перезапуска процесса. `/not-ready` выключает readiness, `/ready-again` возвращает её. Состояния независимы и хранятся в памяти. Основной путь `/` продолжает работать.

| Проба | Путь | periodSeconds | timeoutSeconds | failureThreshold |
|---|---|---|---|---|
| startup | `/live` | 2 | 1 | 30 |
| liveness | `/live` | 2 | 1 | 3 |
| readiness | `/ready` | 2 | 1 | 1 |

Startup проверяет `/live`, поэтому выключение readiness не вызывает перезапуск. Liveness и readiness начинают работать после успешной startup-пробы.

## Эксперимент 1: liveness

Выбрал Pod `hw2-app-7f75bc9cb7-7dpvb`. В отдельном терминале открыл туннель:

```bash
kubectl --context kind-hosting-lab -n homework-2 port-forward pod/hw2-app-7f75bc9cb7-7dpvb 8082:8080
```

Проверил ответы и включил отказ:

```bash
curl -i http://localhost:8082/
curl -i http://localhost:8082/live
curl -i http://localhost:8082/ready
curl -i http://localhost:8082/you-are-death
curl -i http://localhost:8082/live
```

До отказа `/live` и `/ready` вернули 200. После `/you-are-death` получил 503 на `/live`. Kubernetes перезапустил контейнер; после подготовки `/live` снова вернул 200.

| Состояние | Pod | UID | RESTARTS |
|---|---|---|---|
| До | `hw2-app-7f75bc9cb7-7dpvb` | `12f7a072-bb90-4fee-b76b-8c93a9e7f2da` | 0 |
| После | `hw2-app-7f75bc9cb7-7dpvb` | `12f7a072-bb90-4fee-b76b-8c93a9e7f2da` | 1 |

Ответы HTTP и состояние Pod: [03-exp1-liveness.txt](evidence/03-exp1-liveness.txt). События и логи предыдущего запуска: [04-exp1-diagnostics.txt](evidence/04-exp1-diagnostics.txt). Логи клиента до, во время и после отказа: [05-exp1-client.txt](evidence/05-exp1-client.txt).

В событиях есть `Liveness probe failed: HTTP probe failed with statuscode: 503` и `Container web failed liveness probe, will be restarted`.

Имя и UID сохранились, потому что контейнер перезапустился в том же Pod. При удалении Pod был бы создан новый объект с другим UID. Рабочий `/` не мешает перезапуску, потому что kubelet проверяет `/live`. У клиента ошибок не было: `/` продолжал отвечать, а при перезапуске запросы обслуживали другие реплики.

## Эксперимент 2: readiness

После восстановления приложения через туннель к тому же Pod выполнил:

```bash
curl -i http://localhost:8082/not-ready
curl -i http://localhost:8082/ready
curl -i http://localhost:8082/live
curl -i http://localhost:8082/
```

Получил `/ready` → 503, `/live` → 200, `/` → 200. Pod стал `0/1`, его endpoint остался в EndpointSlice с `ready: false`.

| Состояние | IP Pod | READY | conditions.ready | RESTARTS |
|---|---|---|---|---|
| До | 10.244.0.6 | 1/1 | true | 1 |
| После `/not-ready` | 10.244.0.6 | 0/1 | false | 1 |
| После `/ready-again` | 10.244.0.6 | 1/1 | true | 1 |

Имя Pod и UID остались прежними. После исключения endpoint клиент получил 15 успешных ответов от двух остальных реплик. Выбранный Pod в этих ответах не встречался.

Готовность вернул через тот же port-forward:

```bash
curl -i http://localhost:8082/ready-again
curl -i http://localhost:8082/ready
```

`/ready` снова вернул 200. Endpoint стал готовым, ответы выбранной реплики снова появились у клиента.

HTTP, Pod и EndpointSlice во всех трёх состояниях: [06-exp2-readiness.txt](evidence/06-exp2-readiness.txt). Логи клиента: [07-exp2-client.txt](evidence/07-exp2-client.txt). Снимки EndpointSlice: [06-readiness-endpoints.yaml](evidence/06-readiness-endpoints.yaml).

При отказе readiness контейнер продолжил работать, но Service перестал отправлять ему запросы. Через port-forward доступ сохранился, потому что туннель ведёт прямо к выбранному Pod. Через Service команда восстановления попала бы к другой готовой реплике.

## Эксперимент 3: RollingUpdate

Перед обновлением были готовы три реплики. Стратегия: `maxSurge: 1`, `maxUnavailable: 0`, `minReadySeconds: 5`. Собрал и отправил новый тег:

```bash
docker build --build-arg APP_VERSION=hw2-v2 -t localhost:5001/kiselev-homework:hw2-v2 .
docker push localhost:5001/kiselev-homework:hw2-v2
```

В Deployment изменил только image. Число реплик, ресурсы и пробы сохранил.

```bash
kubectl --context kind-hosting-lab apply -f k8s/deployment-v2.yaml
kubectl --context kind-hosting-lab -n homework-2 rollout status deployment/hw2-app --timeout=300s
```

| Состояние | replicas | updatedReplicas | availableReplicas | Старый RS | Новый RS |
|---|---|---|---|---|---|
| До | 3 | 3 | 3 | 3 | 0 |
| Обновление 1 | 4 | 1 | 3 | 3 | 1 |
| Обновление 2 | 4 | 2 | 3 | 2 | 2 |
| Обновление 3 | 4 | 3 | 3 | 1 | 3 |
| После | 3 | 3 | 3 | 0 | 3 |

Старый ReplicaSet — `hw2-app-7f75bc9cb7`, новый — `hw2-app-7f448676d`. Созданы три новых Pod с новыми UID и образом `hw2-v2`. Старый ReplicaSet остался с нулём реплик.

Снимки Deployment, ReplicaSet и Pod с UID и образами, и вывод наблюдения: [08-exp3-rollout.txt](evidence/08-exp3-rollout.txt). Ответы клиента до, во время и после обновления: [09-exp3-client.txt](evidence/09-exp3-client.txt). Снимки Deployment: [08-rollout-deployment.yaml](evidence/08-rollout-deployment.yaml).

В снимках было максимум четыре Pod и не меньше трёх доступных реплик. В выводе `kubectl get pods -w` старые Pod переходили в Terminating, но в отдельные YAML-снимки это состояние не попало.

`maxSurge: 1` разрешает одну дополнительную реплику, поэтому все три Pod заменились по очереди. Новый Pod сначала проходит startup и readiness, затем должен оставаться готовым ещё 5 секунд (`minReadySeconds`). Только после этого Deployment убирает старую реплику. При liveness перезапускается контейнер в прежнем Pod, при обновлении создаются новые Pod через новый ReplicaSet.

## Результаты

| Опыт | Ожидал | Наблюдал | Объяснение |
|---|---|---|---|
| Liveness | Перезапуск контейнера | Прежний UID, RESTARTS 0 → 1 | kubelet обнаружил отказ `/live` |
| Readiness | Исключение из Service | ready true → false → true, без перезапуска | Pod перестал получать обычный трафик |
| RollingUpdate | Замена трёх Pod новым образом | Новые UID и ReplicaSet, версия hw2-v2 | Изменён шаблон Deployment |

В сохранённых фрагментах логов клиента 183 ответа HTTP 200, ошибок нет. Итоговое состояние: [10-final-state.txt](evidence/10-final-state.txt) — сервер `3/3`, клиент `1/1`.

Для взаимодействия внутри кластера достаточно Service. Он сохраняет DNS-имя и адрес при замене Pod, выбирает их по labels. `port` — порт Service, `targetPort` — порт приложения. Port-forward выбирает один Pod и не показывает балансировку Service. Ingress в этой работе не использовал: для запросов между приложениями хватило Service. `localhost` внутри Pod указывает на сам Pod, а не на компьютер.

## Повторение и удаление ресурсов

`k8s/deployment.yaml` содержит начальную версию hw2-v1, `k8s/deployment-v2.yaml` — итоговую hw2-v2. Применять их по очереди. При изменении исходников использовать новые теги образов.

После опытов остановил port-forward. Приложение оставил работающим на hw2-v2.

Удалить только ресурсы этой работы:

```bash
kubectl --context kind-hosting-lab delete namespace homework-2
```

Кластер и Registry сохраняются.
