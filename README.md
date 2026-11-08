# Bazar.com: A Multi-tier Online Book Store

## Distributed and Operating Systems - Lab 1

This project is a fully functional multi-tier, microservices-based online book store built using **ASP.NET Core Web API** and **Docker**. It demonstrates core distributed systems concepts such as replication, load balancing, caching, and eventual consistency.

## System Architecture

The application is composed of three main microservices, deployed across 5 different Docker containers:

1. **Frontend Service (Port 5000)**:
   - Acts as the entry point for clients.
   - Implements **Round Robin** load balancing to distribute requests across replicas.
   - Implements an **In-Memory Cache** (LRU-style) to speed up read requests and handles cache invalidation.

2. **Catalog Service (Replicas on Ports 4001 & 4002)**:
   - Manages the book catalog.
   - Supports querying by topic or item ID.
   - Supports updating stock/price.
   - Ensures **Consistency** by replicating updates to the other replica.

3. **Order Service (Replicas on Ports 3001 & 3002)**:
   - Handles purchasing.
   - Verifies stock by communicating with the Catalog service.
   - Records all successful and failed orders in a persistent CSV file.

## Technologies Used
- **Framework:** C# .NET 9 / ASP.NET Core Web API
- **Containerization:** Docker & Docker Compose
- **Data Storage:** CSV files (persistent storage on disk)
- **Communication:** HTTP REST API

## Prerequisites
Before running the project, make sure you have installed:
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## How to Run the Project

1. Clone the repository:
   ```bash
   git clone https://github.com/Eyasbusaeed/Dos-project-pt1.git
   cd Dos-project-pt1
