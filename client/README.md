# Client → Service → реплики web

Client раз в 2 секунды выполняет GET `/` через Service web и выводит время, HTTP-статус и имя ответившего Pod. Первый запрос выполняется сразу. Таймаут запроса — 1 секунда; ошибки выводятся в консоль и не останавливают цикл.

Web получает `POD_NAME` из `metadata.name` через [Downward API](https://kubernetes.io/docs/concepts/workloads/pods/downward-api/). Service выбирает готовые Pod с меткой `app: web`. Порт Service — 80, порт приложения — 8080. Client не выбирает реплики самостоятельно.

Для демонстрации распределения каждый HTTP/1.1 запрос закрывает соединение через `Connection: close`. На стандартном стенде kind балансировка происходит при установлении соединения; повторное использование одного соединения могло бы закрепить все запросы за одной репликой. Строгое чередование и равномерность на короткой серии не гарантируются. [Механизм Service](https://kubernetes.io/docs/reference/networking/virtual-ips/).

## Сборка и запуск

Из корня практики, при запущенном локальном Registry:

```bash
docker build -t localhost:5001/probes-web:v2 ./local/probes-app
docker push localhost:5001/probes-web:v2
docker build -t localhost:5001/client:v1 ./local/client
docker push localhost:5001/client:v1

kubectl --context kind-hosting-lab apply -f namespace.yaml
kubectl --context kind-hosting-lab apply -f deployment-probes.yaml
kubectl --context kind-hosting-lab apply -f service-probes.yaml
kubectl --context kind-hosting-lab apply -f configmap-client.yaml
kubectl --context kind-hosting-lab apply -f deployment-client.yaml
kubectl --context kind-hosting-lab -n hosting-lab rollout status deployment/web --timeout=180s
kubectl --context kind-hosting-lab -n hosting-lab rollout status deployment/client --timeout=180s
kubectl --context kind-hosting-lab -n hosting-lab logs -f deployment/client
```

Пример формата вывода (имена и время будут другими):

```text
2026-10-14T10:00:00.0000000+00:00 GET http://web.hosting-lab.svc.cluster.local/ -> 200 pod=web-abc123-x7k2m
2026-10-14T10:00:02.0000000+00:00 GET http://web.hosting-lab.svc.cluster.local/ -> 200 pod=web-abc123-p9r4n
```

Если web ещё не готов, client может выводить ошибки; после появления готовых endpoints запросы продолжатся автоматически. Поддерживайте не менее двух реплик web для наблюдения распределения. Манифест сейчас задаёт три реплики; его применение заменяет предыдущее значение, заданное командой scale.

Проверить соответствие имён и адресов:

```bash
kubectl --context kind-hosting-lab -n hosting-lab get pods -l app=web -o wide
kubectl --context kind-hosting-lab -n hosting-lab get endpointslices -l kubernetes.io/service-name=web
```

Используйте `service-probes.yaml`, а не старый `service.yaml` для nginx: они описывают один и тот же Service web, но старый файл задаёт targetPort 80. Их нельзя применять по очереди в этом упражнении.

## ConfigMap

`configmap-client.yaml` передаёт `WEB_SERVICE_HOST` и `WEB_SERVICE_PORT` как переменные среды через `envFrom`. Порт должен быть портом Service (80), а не контейнера (8080). Для стандартного kind используется DNS-имя `web.hosting-lab.svc.cluster.local`; в том же namespace допустимо короткое `web`.

После изменения ConfigMap уже запущенный client не получит новые переменные среды. Выполните:

```bash
kubectl --context kind-hosting-lab apply -f configmap-client.yaml
kubectl --context kind-hosting-lab -n hosting-lab rollout restart deployment/client
```

Вне Kubernetes по умолчанию клиент обращается к `http://localhost:8080/`. Для запуска в Docker с пробросом настроек используйте `-e WEB_SERVICE_HOST=... -e WEB_SERVICE_PORT=...`: localhost контейнера не является компьютером или другим контейнером.

## Проверка

При подготовке статически проверены связи selector/labels, имена портов и переменные среды. Компиляция, сборка Docker и живые запросы в kind не проверены: среда агента не имеет доступа к Docker socket и локального .NET SDK.
