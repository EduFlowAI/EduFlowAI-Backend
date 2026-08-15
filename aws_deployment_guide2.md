# EduFlowAI Backend - Complete AWS Deployment & CI/CD Guide

This document provides a complete, step-by-step procedure to deploy the **EduFlowAI Backend** (`EduFlowAI.Api` & `EduFlowAI.Worker`) to **Amazon Web Services (AWS)** using **Amazon ECR**, **AWS ECS Fargate**, **Amazon MQ (RabbitMQ)**, and **GitHub Actions CI/CD**.

---

## 🏗️ Deployment Architecture

```
                        +----------------------+
                        |   Client Frontend    |
                        +----------+-----------+
                                   | (HTTPS)
                                   v
                    +------------------------------+
                    |  AWS ALB (Load Balancer)     |
                    +--------------+---------------+
                                   |
                     +-------------v--------------+
                     |    AWS ECS Fargate Cluster |
                     |                            |
                     |  +----------------------+  |
                     |  |    EduFlowAI.Api     |  |
                     |  +----------+-----------+  |
                     |             |              |
                     |             v              |
                     |  +----------------------+  |
                     |  |    EduFlowAI.Worker  |  |
                     |  +----------------------+  |
                     +-------------+--------------+
                                   |
            +----------------------+----------------------+
            |                                             |
            v AMQP (5671/5672)                            v PostgreSQL (5432)
  +--------------------+                      +-----------------------+
  | Amazon MQ RabbitMQ |                      | Hosted PostgreSQL DB  |
  |  (or CloudAMQP)    |                      | (e.g. Neon / RDS)     |
  +--------------------+                      +-----------------------+
```

---

## STEP 1: Create Amazon ECR Repositories

AWS Elastic Container Registry (ECR) will store your compiled Docker images for both `EduFlowAI.Api` and `EduFlowAI.Worker`.

### Via AWS Management Console:
1. Open **AWS Console** -> Search for **Elastic Container Registry (ECR)**.
2. Click **Create Repository**.
3. Create Repository 1:
   - Name: `eduflowai-api`
   - Visibility: `Private`
4. Create Repository 2:
   - Name: `eduflowai-worker`
   - Visibility: `Private`

### Via AWS CLI:
```bash
aws ecr create-repository --repository-name eduflowai-api --region us-east-1
aws ecr create-repository --repository-name eduflowai-worker --region us-east-1
```

*(Copy your 12-digit AWS Account ID, e.g., `123456789012`). Your ECR image URIs will be:*
- API Image: `<AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/eduflowai-api:latest`
- Worker Image: `<AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/eduflowai-worker:latest`

---

## STEP 2: Configure GitHub Secrets for AWS Access

1. Open your GitHub Repository in your browser.
2. Go to **Settings** -> **Secrets and variables** -> **Actions**.
3. Click **New repository secret** and add:
   * **`AWS_ACCESS_KEY_ID`**: Your AWS IAM Access Key ID.
   * **`AWS_SECRET_ACCESS_KEY`**: Your AWS IAM Secret Access Key.

*(Ensure the IAM User has permissions for ECR (`AmazonEC2ContainerRegistryFullAccess`) and ECS (`AmazonECS_FullAccess`)).*

---

## STEP 3: Push Code to GitHub to Build & Push Docker Images to ECR

Before setting up AWS ECS services, let GitHub Actions build the Docker images and push them to your ECR repositories automatically!

1. Open your terminal in the solution root directory:
```bash
git add .
git commit -m "Add Dockerfiles and AWS CI/CD pipeline"
git push origin main
```
2. Go to GitHub -> **Actions** tab. Watch the **Deploy EduFlowAI Backend to AWS ECS** workflow run.
3. Once the workflow completes the **Build, Tag & Push** step, check your ECR repositories in the AWS Console. You will see `eduflowai-api:latest` and `eduflowai-worker:latest` uploaded!

---

## STEP 4: Setup RabbitMQ in the Cloud

Since `EduFlowAI.Api` and `EduFlowAI.Worker` communicate via Wolverine + RabbitMQ (`Messaging:Enabled=true`), RabbitMQ must be accessible on AWS.

### Option A: Amazon MQ for RabbitMQ (Managed AWS Service)
1. Open **AWS Console** -> Search for **Amazon MQ**.
2. Click **Create Broker** -> Select **RabbitMQ**.
3. Deployment Mode: **Single-instance** (for dev) or **Cluster/Active-Standby** (for prod).
4. Broker Name: `eduflowai-rabbitmq`.
5. Configure Master Credentials:
   - User: `eduflowai`
   - Password: `YourSecurePassword123!`
6. Access type: Select **Publicly accessible**.
7. Once created, copy your AMQP connection endpoint:
   `amqps://eduflowai:YourSecurePassword123!@b-xxx-xxx.mq.us-east-1.amazonaws.com:5671`

### Option B: CloudAMQP (Alternative Free-Tier Managed RabbitMQ)
1. Go to [CloudAMQP.com](https://www.cloudamqp.com/) -> Create a free instance (Little Lemur plan).
2. Copy the generated connection string:
   `amqps://user:password@rat.rmq.cloudamqp.com/vhost`

---

## STEP 5: Create AWS ECS Cluster & Task Definitions

### 1. Create ECS Cluster
1. Open **AWS Console** -> **Elastic Container Service (ECS)** -> **Clusters**.
2. Click **Create Cluster**.
3. Cluster Name: `eduflowai-cluster`
4. Infrastructure: **AWS Fargate (serverless)**.

---

### 2. Create API Task Definition (`eduflowai-api-task`)
1. In ECS -> Go to **Task Definitions** -> **Create new Task Definition**.
2. Task Definition Family: `eduflowai-api-task`
3. Launch Type: **AWS Fargate** (CPU: `0.5 vCPU`, Memory: `1 GB`).
4. Container details:
   - Container Name: `eduflowai-api`
   - Image URI: `<AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/eduflowai-api:latest`
   - Port Mapping: `8080` (TCP, HTTP).
5. Environment Variables:
   | Key | Example Value |
   | --- | --- |
   | `ConnectionStrings__DefaultConnection` | `Host=ep-xxx.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...` |
   | `ConnectionStrings__RabbitMq` | `amqps://eduflowai:YourSecurePassword123!@b-xxx.mq.us-east-1.amazonaws.com:5671` |
   | `Messaging__Enabled` | `true` |
   | `Jwt__SigningKey` | `OurEduFlowAI.SigningKey__32bytes` |
   | `Jwt__Issuer` | `EduFlowAI.Api` |
   | `Jwt__Audience` | `EduFlowAI.Client` |
   | `Gemini__ApiKey` | `AIzaSy...` |
   | `Smtp__FromAddress` | `masafetelsekkaa@gmail.com` |
   | `Smtp__Host` | `smtp.gmail.com` |
   | `Smtp__Port` | `587` |
   | `Smtp__Password` | `abci ekaf pjva gaos` |

---

### 3. Create Worker Task Definition (`eduflowai-worker-task`)
1. Go to **Task Definitions** -> **Create new Task Definition**.
2. Task Definition Family: `eduflowai-worker-task`
3. Launch Type: **AWS Fargate** (CPU: `0.25 vCPU`, Memory: `512 MB`).
4. Container details:
   - Container Name: `eduflowai-worker`
   - Image URI: `<AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/eduflowai-worker:latest`
   - Port Mapping: None required.
5. Environment Variables:
   | Key | Example Value |
   | --- | --- |
   | `ConnectionStrings__DefaultConnection` | `Host=ep-xxx.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...` |
   | `ConnectionStrings__RabbitMq` | `amqps://eduflowai:YourSecurePassword123!@b-xxx.mq.us-east-1.amazonaws.com:5671` |
   | `Messaging__Enabled` | `true` |
   | `Smtp__FromAddress` | `masafetelsekkaa@gmail.com` |
   | `Smtp__Host` | `smtp.gmail.com` |
   | `Smtp__Port` | `587` |
   | `Smtp__Password` | `abci ekaf pjva gaos` |

---

## STEP 6: Create ECS Services & Application Load Balancer (ALB)

### 1. Create Target Group & Load Balancer for API
1. Open **EC2 Console** -> **Target Groups** -> **Create Target Group**.
   - Target Type: **IP addresses**
   - Target Group Name: `eduflowai-api-tg`
   - Protocol / Port: `HTTP` / `8080`
   - Health check path: `/health` or `/swagger/index.html`
2. Open **EC2 Console** -> **Load Balancers** -> **Create Load Balancer**.
   - Type: **Application Load Balancer (ALB)**
   - Name: `eduflowai-alb`
   - Scheme: Internet-facing
   - Listener: HTTP (80) -> forwarding to `eduflowai-api-tg` (and HTTPS 443 with ACM SSL Certificate).

### 2. Create API Service on ECS
1. Go to **ECS** -> **Clusters** -> `eduflowai-cluster` -> **Services** -> **Create**.
2. Service Name: `eduflowai-api-service`
3. Task Definition: `eduflowai-api-task` (LATEST)
4. Desired Tasks: `1`
5. Networking: Attach Security Group allowing inbound port `8080` from ALB Security Group.
6. Load Balancing: Attach `eduflowai-alb` and select target group `eduflowai-api-tg`.

### 3. Create Worker Service on ECS
1. Go to **ECS** -> **Clusters** -> `eduflowai-cluster` -> **Services** -> **Create**.
2. Service Name: `eduflowai-worker-service`
3. Task Definition: `eduflowai-worker-task` (LATEST)
4. Desired Tasks: `1`
5. Load Balancing: Select **None** (Worker runs in background).

---

## STEP 7: Continuous Deployment (How it works automatically)

Now your full CI/CD pipeline is live!

Every time you commit and push changes to the `main` branch:
1. **GitHub Actions** runs unit tests (`dotnet test`).
2. Builds updated Docker containers for API and Worker.
3. Pushes updated images with `latest` tag to Amazon ECR.
4. Triggers AWS ECS to restart tasks with the new container images automatically!

---

## 🔮 STEP 8: Deploying the Frontend (Preview)

Once your backend deployment is verified:
1. Get your public ALB / HTTPS domain URL (e.g. `https://api.yourdomain.com`).
2. Set `apiUrl` in your frontend application.
3. Deploy frontend using **AWS S3 + CloudFront**, **AWS Amplify**, or **Vercel**.
