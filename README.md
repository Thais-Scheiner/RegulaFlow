# RegulaFlow - Gestor de Reclamações Regulatórias

![Status](https://img.shields.io/badge/status-funcional-brightgreen)
![.NET](https://img.shields.io/badge/.NET-8-blueviolet)
![C#](https://img.shields.io/badge/C%23-12-blue)
![AWS](https://img.shields.io/badge/AWS-SQS%20%26%20SNS-orange)
![Docker](https://img.shields.io/badge/Docker-blue)
![Microservices](https://img.shields.io/badge/arquitetura-microserviços-lightgrey)

## 📄 Índice

- [🎯 Sobre o Projeto](#-sobre-o-projeto)
- [🏛️ Arquitetura e Funcionamento](#️-arquitetura-e-funcionamento)
- [✨ Os Microserviços](#-os-microserviços)
- [🛠️ Tecnologias Utilizadas](#️-tecnologias-utilizadas)
- [🚀 Como Rodar o Projeto](#-como-rodar-o-projeto)
  - [Pré-requisitos](#pré-requisitos)
  - [1. Configuração na Nuvem (AWS)](#1-configuração-na-nuvem-aws)
  - [2. Configuração Local](#2-configuração-local)
  - [3. Executando a Aplicação](#3-executando-a-aplicação)
- [🧪 Testando o Fluxo Completo](#-testando-o-fluxo-completo)
- [🔮 Próximos Passos (Melhorias)](#-próximos-passos-melhorias)
- [✍️ Autora](#️-autora)

## 🎯 Sobre o Projeto

O **RegulaFlow** foi idealizado como uma resposta direta aos desafios de engenharia de software encontrados em **sistemas de missão crítica**, como os de gestão de reclamações regulatórias (Bacen, Procon) no setor financeiro. O objetivo foi construir uma solução em **.NET 8** que não apenas funcione, mas que seja **resiliente, escalável e auditável**, utilizando uma arquitetura de microserviços e serviços de nuvem da **AWS**.

Este projeto demonstra na prática a construção de um sistema assíncrono e desacoplado, focado em garantir que nenhuma informação crítica seja perdida, mesmo em caso de falhas parciais.

## 🏛️ Arquitetura e Funcionamento

A arquitetura foi projetada para garantir alta disponibilidade e desacoplamento. O fluxo de uma nova reclamação é o seguinte:

1.  Um cliente externo envia uma reclamação para a **API de Ingestão**.
2.  A API valida os dados e imediatamente publica a reclamação em uma fila **AWS SQS**, respondendo com sucesso (`202 Accepted`). Isso torna o ponto de entrada extremamente rápido e resiliente.
3.  O **Worker de Processamento** consome a mensagem da fila SQS, aplica as regras de negócio e salva a reclamação no banco de dados **MySQL**.
4.  Após salvar, o Worker publica um evento `ComplaintProcessed` em um tópico **AWS SNS**.
5.  O **Worker de Notificação**, que está inscrito no tópico SNS, recebe o evento e simula o envio de uma notificação para a equipe responsável.

```
+----------+   (1. POST)   +-----------------+   (2. Publica)   +-----------------+
| Cliente  | ------------> | API de Ingestão | ---------------> | Fila SQS        |
+----------+               +-----------------+                  +--------+--------+
                                                                         | (3. Consome)
                                                                         |
                                                               +---------v---------+
                                                               | Worker de Proc.   |
                                                               +---------+---------+
                                                                         |
                                         (4. Salva no DB)                | (5. Publica Evento)
                                                    |                    |
                                     +--------------v--------------+     |
                                     | Banco de Dados (MySQL)      |     |
                                     +-----------------------------+     |
                                                                         |
                                                               +---------v---------+
                                                               | Tópico SNS        |
                                                               +---------+---------+
                                                                         | (6. Assina e Recebe)
                                                                         |
                                                               +---------v---------+
                                                               | Worker de Notif.  |
                                                               +-------------------+
```

## ✨ Os Microserviços

O sistema é composto por três serviços independentes:

* **`ComplaintIngestion.API`:**
    * **Responsabilidade:** Ponto de entrada (API RESTful) para receber novas reclamações. Sua única função é validar e enfileirar a requisição no SQS, garantindo uma resposta rápida ao cliente.
    * **Tecnologias:** ASP.NET Core, AWSSDK.SQS, Serilog.

* **`ComplaintProcessor.Worker`:**
    * **Responsabilidade:** O "cérebro" do sistema. Consome as reclamações da fila SQS, processa as regras de negócio, salva a reclamação no banco de dados MySQL e publica um evento de sucesso no SNS.
    * **Tecnologias:** .NET Worker Service, Entity Framework Core, Pomelo (MySQL), AWSSDK.SQS, AWSSDK.SNS.

* **`Notification.Worker`:**
    * **Responsabilidade:** Reagir a eventos do sistema. Consome os eventos `ComplaintProcessed` (via uma fila SQS inscrita no tópico SNS) e simula o envio de notificações para as equipes internas.
    * **Tecnologias:** .NET Worker Service, AWSSDK.SQS.

## 🛠️ Tecnologias Utilizadas

* **Backend:** C# 12, .NET 8, ASP.NET Core, Entity Framework Core 8
* **Cloud (AWS):**
    * **Simple Queue Service (SQS):** Para enfileiramento de mensagens e desacoplamento.
    * **Simple Notification Service (SNS):** Para o padrão de publicação/assinatura (Pub/Sub) de eventos.
    * **Identity and Access Management (IAM):** Para gerenciamento de permissões de acesso aos serviços.
* **Banco de Dados:** MySQL 8.0
* **Infraestrutura como Código:** Docker & Docker Compose (para o ambiente de banco de dados local).
* **Logging:** Serilog.

## 🚀 Como Rodar o Projeto

### Pré-requisitos
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/)
* [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html) com uma conta AWS configurada (`aws configure`).

### 1. Configuração na Nuvem (AWS)
É necessário criar os recursos de SQS e SNS no console da AWS:
1.  **Crie as Filas SQS:**
    * `complaint-ingestion-queue`
    * `notification-queue`
2.  **Crie o Tópico SNS:**
    * `complaint-processed-topic`
3.  **Inscreva a Fila de Notificação:** Na página do tópico SNS, crie uma "Subscription" para a `notification-queue`.
4.  **Configure as Permissões:**
    * Adicione uma política de acesso na `notification-queue` para permitir que o tópico SNS envie mensagens para ela.
    * No **IAM**, edite a política do seu usuário de desenvolvimento para permitir as ações `sqs:SendMessage`, `sqs:ReceiveMessage`, `sqs:DeleteMessage` nas duas filas, e a ação `sns:Publish` no tópico.

### 2. Configuração Local
1.  Clone este repositório.
2.  Na raiz do projeto, crie um arquivo `.env` com a senha do MySQL:
    ```env
    MYSQL_ROOT_PASSWORD=MySecretPassword123!
    ```
3.  **Atualize os arquivos `appsettings.Development.json`** em cada um dos três projetos com as suas URLs de fila SQS, ARN do tópico SNS e a senha do banco de dados.
4.  Execute o Docker Compose para iniciar o banco de dados:
    ```bash
    docker-compose up -d
    ```
5.  Aplique a migração do banco de dados para criar a tabela:
    ```bash
    cd src/ComplaintProcessor.Worker
    dotnet ef database update
    ```

### 3. Executando a Aplicação
Você precisará de **três terminais** abertos na raiz do projeto (`RegulaFlow`).

* **Terminal 1 (API de Ingestão):**
    ```bash
    cd src/ComplaintIngestion.API
    dotnet run
    ```

* **Terminal 2 (Worker de Processamento):**
    ```bash
    cd src/ComplaintProcessor.Worker
    dotnet run
    ```

* **Terminal 3 (Worker de Notificação):**
    ```bash
    cd src/Notification.Worker
    dotnet run
    ```

## 🧪 Testando o Fluxo Completo
1.  Acesse a interface do Swagger da API de ingestão (geralmente em `http://localhost:5160/swagger`).
2.  Use o endpoint `POST /api/Complaints` para enviar uma nova reclamação.
3.  **Observe os terminais:** você verá os logs aparecerem em sequência, da API para o Processor e, finalmente, para o Notification, confirmando que o fluxo assíncrono funcionou.
4.  Verifique seu banco de dados MySQL para ver o novo registro salvo na tabela `Complaints`.

## 🔮 Próximos Passos (Melhorias) em andamento...
* [ ] Implementar um **API Gateway** com Ocelot para ser o ponto de entrada único.
* [ ] Adicionar **logging estruturado** com Serilog para o **AWS CloudWatch**.
* [ ] Integrar com o **SonarCloud** para análise estática e garantia de qualidade de código.
* [ ] Implementar políticas de resiliência (Retry, Circuit Breaker) com **Polly**.
* [ ] Adicionar uma **integração com IA** (ex: Amazon Comprehend ou API externa) para categorizar ou analisar o sentimento das reclamações.

## ✍️ Autora

**Thais Scheiner**

* LinkedIn: `https://www.linkedin.com/in/thaisscheiner`
