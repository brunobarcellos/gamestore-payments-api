# GameStore - PaymentsAPI

Microsserviço responsável por **simular o processamento de pagamentos** de compras de jogos.
- Consome `OrderPlaceEvent` da fila MQ.
- Simula o processamento financeiro e publica o resultado na fila `PaymentProcessedEvent` com status `Approved` ou `Rejected`.

## Executar Localmente (.NET 10)
```bash
dotnet run --project src/GameStore.Payments.Api
```

## Build Docker (Multi-Stage)
```bash
docker build -t gamestore/payments-api:latest .
```

## Deploy Kubernetes
```bash
kubectl apply -f k8s/
```

## Executar Testes
```bash
dotnet test PaymentsAPI.slnx
```

