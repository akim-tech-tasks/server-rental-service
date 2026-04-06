# ServerRentalService

REST API для распределения и аренды вычислительных серверов.

## Что реализовано

- Добавление сервера в пул.
- Поиск свободных серверов по параметрам.
- Аренда сервера по `id`.
- Освобождение сервера.
- Если сервер включен, аренда выдается сразу.
- Если сервер выключен, он переходит в загрузку на 5 минут, затем становится выданным.
- Проверка статуса готовности сервера к выдаче.
- Автоматическое освобождение и выключение через 20 минут после начала аренды.
- Хранение данных в SQLite.
- Логирование ключевых операций.
- Защита от конкурентных запросов при аренде/освобождении.

## Технологии

- .NET 10 Web API
- EF Core + SQLite
- Фоновые `HostedService` для обработки жизненного цикла аренды
- MSTest для тестов

## Запуск

```bash
dotnet run --project ServerRentalService/ServerRentalService.csproj
```

## Эндпоинты API

- `POST /api/servers`
- `GET /api/servers/available`
- `POST /api/servers/{serverId}/rent`
- `POST /api/servers/{serverId}/release`
- `GET /api/servers/{serverId}/status`

## Примеры запросов

Базовый URL:

```text
http://localhost:5136
```

Добавить включенный сервер:

```bash
curl -X POST "http://localhost:5136/api/servers" ^
  -H "Content-Type: application/json" ^
  -d "{\"operatingSystem\":\"Ubuntu 24.04\",\"memoryGb\":32,\"diskGb\":512,\"cpuCores\":16,\"initiallyPoweredOn\":true}"
```

Добавить выключенный сервер:

```bash
curl -X POST "http://localhost:5136/api/servers" ^
  -H "Content-Type: application/json" ^
  -d "{\"operatingSystem\":\"Windows Server 2022\",\"memoryGb\":16,\"diskGb\":250,\"cpuCores\":8,\"initiallyPoweredOn\":false}"
```

Найти свободные серверы:

```bash
curl "http://localhost:5136/api/servers/available?operatingSystem=Ubuntu&minMemoryGb=16&minDiskGb=200&minCpuCores=8"
```

Взять сервер в аренду:

```bash
curl -X POST "http://localhost:5136/api/servers/{serverId}/rent"
```

Проверить статус сервера:

```bash
curl "http://localhost:5136/api/servers/{serverId}/status"
```

Освободить сервер:

```bash
curl -X POST "http://localhost:5136/api/servers/{serverId}/release"
```

Также готовые сценарии есть в файле:

- `ServerRentalService/ServerRentalService.http`

## Тесты

```bash
dotnet test ServerRentalService.Tests/ServerRentalService.Tests.csproj
```
