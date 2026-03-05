# 📈 Itaú Corretora – Desafio Técnico

![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Kafka](https://img.shields.io/badge/Kafka-Event%20Streaming-black)
![MySQL](https://img.shields.io/badge/MySQL-Database-blue)
![Docker](https://img.shields.io/badge/Docker-Container-blue)
![License](https://img.shields.io/badge/license-MIT-green)

Projeto desenvolvido como parte do **Desafio Técnico da Itaú Corretora – Engenharia de Software**.

A aplicação simula uma **plataforma de investimento automatizado**, permitindo:

* adesão de clientes
* compras programadas de ativos
* rebalanceamento automático de carteira
* cálculo de imposto de renda
* comunicação assíncrona via **Apache Kafka**

---

# 🧠 Visão Geral da Arquitetura

A aplicação segue princípios de **Clean Architecture / Separation of Concerns**, utilizando eventos para desacoplar processos.

```
Cliente → API → Services → Database (MySQL)
                    ↓
                 Kafka
                    ↓
              Background Workers
```

Componentes principais:

* **API REST** → controle de clientes e operações
* **MySQL** → persistência de dados
* **Kafka** → comunicação assíncrona de eventos
* **Workers** → execução automática de tarefas
* **Docker** → ambiente de infraestrutura

---

# 🏗️ Arquitetura do Sistema

```
               +-------------------+
               |      CLIENT       |
               |   (HTTP REST)     |
               +---------+---------+
                         |
                         v
               +-------------------+
               |   ASP.NET API     |
               |    Controllers    |
               +---------+---------+
                         |
                         v
               +-------------------+
               |     Services      |
               |  Business Logic   |
               +---------+---------+
                         |
            +------------+-------------+
            |                          |
            v                          v
    +--------------+          +----------------+
    |    MySQL     |          |     Kafka      |
    |  Persistence |          | Event Streaming|
    +--------------+          +----------------+
                                       |
                                       v
                               +---------------+
                               |    Workers    |
                               | BackgroundJobs|
                               +---------------+
```

---

# 🚀 Tecnologias Utilizadas

### Backend

* **.NET 8**
* **ASP.NET Core Web API**
* **Entity Framework Core**

### Infraestrutura

* **MySQL**
* **Apache Kafka**
* **Docker**
* **Docker Compose**

### Testes

* **xUnit**
* **Code Coverage**

---

# ⚙️ Configuração do Ambiente

## 1️⃣ Clonar o repositório

```bash
git clone https://github.com/seu-usuario/itau-corretora-desafio.git

cd itau-corretora-desafio
```

---

# 🗄️ Configurar Banco de Dados

Crie o banco:

```
ItauCorretoraDesafio
```

Edite **appsettings.json**

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=ItauCorretoraDesafio;User=root;Password=sua_senha;"
}
```

Execute as migrations:

```bash
dotnet ef database update
```

---

# 🐳 Subir Kafka com Docker

Execute:

```bash
docker-compose up -d
```

Verifique se os containers estão rodando:

```bash
docker ps
```

---

# 📨 Criar Tópicos Kafka

```bash
docker exec -it kafka kafka-topics --create \
--topic ordens-criadas \
--bootstrap-server localhost:9092 \
--partitions 1 \
--replication-factor 1
```

```bash
docker exec -it kafka kafka-topics --create \
--topic orders-executed \
--bootstrap-server localhost:9092 \
--partitions 1 \
--replication-factor 1
```

```bash
docker exec -it kafka kafka-topics --create \
--topic ir-dedo-duro \
--bootstrap-server localhost:9092 \
--partitions 1 \
--replication-factor 1
```

---

# ▶️ Executar a Aplicação

```bash
dotnet run
```

API disponível em:

```
http://localhost:5046
```

Swagger:

```
http://localhost:5046/swagger
```

---

# 📡 Endpoints da API

---

# 👤 Clientes

| Método | Endpoint                            | Descrição                            |
| ------ | ----------------------------------- | ------------------------------------ |
| POST   | `/api/customers/subscribe`          | Cria cliente e conta de investimento |
| POST   | `/api/customers/{id}/exit`          | Cliente sai do produto               |
| PUT    | `/api/customers/{id}/monthly-value` | Atualiza valor de aporte             |
| GET    | `/api/customers/{id}/portfolio`     | Consulta carteira                    |
| GET    | `/api/customers/{id}/performance`   | Histórico de aportes                 |

---

# 🛡️ Administração (Cestas)

| Método | Endpoint                     | Descrição           |
| ------ | ---------------------------- | ------------------- |
| POST   | `/api/admin/wallets`         | Cria nova cesta     |
| GET    | `/api/admin/wallets/current` | Retorna cesta ativa |
| GET    | `/api/admin/wallets/history` | Histórico de cestas |

Regras:

* mínimo **5 ativos**
* soma **100% da carteira**

---

# 💰 Compra Programada

| Método | Endpoint                 | Descrição             |
| ------ | ------------------------ | --------------------- |
| POST   | `/api/Purchase/executar` | Executa compra manual |

Worker automático executa nos dias:

* **5**
* **15**
* **25**

---

# 🔄 Rebalanceamento

| Método | Endpoint                           | Descrição                          |
| ------ | ---------------------------------- | ---------------------------------- |
| POST   | `/api/Rebalancement/customer/{id}` | Rebalanceia carteira de um cliente |
| POST   | `/api/Rebalancement/all`           | Rebalanceia todos clientes         |

---

# 📊 Imposto de Renda

| Método | Endpoint                               | Descrição                      |
| ------ | -------------------------------------- | ------------------------------ |
| POST   | `/api/Tax/calculate?year=2026&month=3` | Calcula IR para todos clientes |
| POST   | `/api/Tax/calculate/{id}`              | Calcula IR para cliente        |

---

# 🔍 Consultas

| Método | Endpoint                                     |
| ------ | -------------------------------------------- |
| GET    | `/api/Position/{customerId}`                 |
| GET    | `/api/Order?customerId=1&page=1&pageSize=10` |
| GET    | `/api/Quote/{stockCode}`                     |

Exemplo:

```
/api/Quote/PETR4?startDate=2024-01-01&endDate=2024-12-31
```

---

# 🔁 Fluxo de Compra Programada

```
Scheduler Worker
       ↓
Consulta clientes ativos
       ↓
Gera ordens de compra
       ↓
Publica evento no Kafka
       ↓
Consumer processa execução
       ↓
Atualiza posições do cliente
```

---

# 🔄 Fluxo de Rebalanceamento

```
Mudança de Cesta
      ↓
Detecta desvio da carteira
      ↓
Gera ordens de ajuste
      ↓
Publica evento no Kafka
      ↓
Atualiza posições
```

---

# 🧪 Testes

Executar testes:

```bash
dotnet test
```

Executar testes com cobertura:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

# 📁 Estrutura do Projeto

```
ItauCorretora.Desafio
│
├── Controllers
├── Data
├── DTOs
├── Kafka
│   ├── Producers
│   └── Consumers
├── Models
├── Services
│   ├── Implementations
│   └── Interfaces
├── Workers
├── Migrations
│
├── docker-compose.yml
├── appsettings.json
├── Program.cs
└── README.md
```

---

# 🐳 Comandos Docker Úteis

Subir containers

```
docker-compose up -d
```

Parar containers

```
docker-compose down
```

Ver logs Kafka

```
docker logs kafka
```

Listar tópicos

```
docker exec -it kafka kafka-topics --list --bootstrap-server localhost:9092
```

Enviar mensagem teste

```
echo '{"orderId":1,"status":"EXECUTED","executedQuantity":9}' \
| docker exec -i kafka kafka-console-producer \
--topic orders-executed \
--bootstrap-server localhost:9092
```


# 👨‍💻 Autor

**Mateus Lemos do Nascimento**

LinkedIn
[www.linkedin.com/in/mateus-lemos-dev](https://linkedin.com/in/seu-perfil)

Email
[mateus.lemos.developer@gmail.com](mailto:seu-email@exemplo.com)

---

# 📅 Entrega

Desafio Técnico — **Itaú Corretora**

Engenharia de Software

Março • 2026
