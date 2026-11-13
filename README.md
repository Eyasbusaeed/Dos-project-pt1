# Bazar.com: A Multi-tier Online Book Store

Distributed and Operating Systems - Lab 1

Bazar.com is a small online bookstore built from **three microservices** written in C# / ASP.NET Core (.NET 9).
Each service runs in its own Docker container and the services talk to each other over **HTTP REST with JSON** responses.
A Python command-line client runs on the host machine.

## Architecture

```
                         +-------------------+
   client (host) ------> |  frontend  :5000  | ---- GET /search, /info ----> +-----------------+
   client.py             +-------------------+                               |  catalog :4000  |
                                  |                                          |  catalog.csv    |
                                  | POST /purchase                           +-----------------+
                                  v                                                  ^
                         +-------------------+   GET /info/{id}, POST /decrement/{id}|
                         |   order    :3000  | -------------------------------------+
                         |   orders.csv      |
                         +-------------------+
```

| Service | Port | Responsibility |
|---------|------|----------------|
| **frontend** | 5000 | Entry point for clients. `search` and `info` are forwarded to the catalog, `purchase` to the order service. Status codes and JSON are passed through; answers 503 if a backend is unreachable. |
| **catalog** | 4000 | Owns the book data (id, title, stock quantity, price, topic). Query by topic, query by item, update price / stock, atomic stock decrement. |
| **order** | 3000 | `purchase`: checks the book exists, atomically decrements stock in the catalog (fails if out of stock) and appends the result to a persistent order log. |

Initial catalog (10 copies each):

| Id | Title | Topic | Price |
|----|-------|-------|-------|
| 1 | How to get a good grade in DOS in 40 minutes a day | distributed systems | 30 |
| 2 | RPCs for Noobs | distributed systems | 50 |
| 3 | Xen and the Art of Surviving Undergraduate School | undergraduate school | 15 |
| 4 | Cooking for the Impatient Undergrad | undergraduate school | 15 |

### Concurrency

* All access to a CSV file happens under a single lock inside the owning service.
* Every catalog modification is a read-modify-write executed **entirely inside that lock** (`CatalogRepository.Update`), so concurrent updates cannot overwrite each other.
* A purchase does not "read stock, then write stock - 1" (that would be a race). The order service calls `POST /decrement/{id}` and the catalog checks and decrements stock in one atomic step, returning `409` when the stock is 0. Overselling is therefore impossible, even with many simultaneous buyers.
* The catalog file is written to a temporary file and then moved over the real one, so a crash cannot leave a half-written file.

### Persistence

* Catalog data: `catalog.csv` (`Id,Title,Quantity,Price,Topic`), created with the four books on first start.
* Order log: `orders.csv` (`OrderId,TimestampUtc,BookId,Title,Price,Status`), status is `succeeded` or `failed`.
* In Docker, both live in named volumes mounted at `/app/data`, so data survives container restarts (`docker compose down` keeps it; `docker compose down -v` deletes it).

## REST API

All responses are JSON.

### Frontend (port 5000) - the API clients use

| Method | Path | Description |
|--------|------|-------------|
| GET | `/search/{topic}` | Books for a topic, e.g. `/search/distributed%20systems` -> `[{"id":1,"title":"..."},{"id":2,"title":"..."}]` (empty list if none) |
| GET | `/info/{id}` | `{"id":2,"title":"RPCs for Noobs","quantity":10,"price":50,"topic":"distributed systems"}`; 404 if unknown |
| POST | `/purchase/{id}` | `200 {"message","orderId","title"}`; `409` out of stock; `404` unknown book; `503` backend down |

### Catalog (port 4000)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/search/{topic}` | `[{id,title}]` for the topic (case-insensitive), `[]` if none |
| GET | `/info/{id}` | `{id,title,quantity,price,topic}`; 404 `{"message":...}` if unknown |
| PUT or POST | `/update/{id}` | Body `{"price":n, "quantity":n, "delta":n}` (all optional, at least one). `quantity` sets stock, `delta` adds/subtracts stock. Rejected with 400 if stock would become negative. |
| POST | `/decrement/{id}` | Atomically reduce stock by one; `409` if out of stock, `404` if unknown |

### Order (port 3000)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/purchase/{id}` | Verify via catalog `GET /info/{id}`, then catalog `POST /decrement/{id}`. Logs `bought book <title>` on success. |
| GET | `/orders` | The persistent order log |

## Configuration (environment variables)

| Variable | Service | Default |
|----------|---------|---------|
| `CatalogFilePath` | catalog | `/app/data/catalog.csv` |
| `OrdersFilePath` | order | `/app/data/orders.csv` |
| `CATALOG_URL` | order, frontend | `http://catalog:4000` |
| `ORDER_URL` | frontend | `http://order:3000` |

## How to run

Prerequisites: Docker with Docker Compose, and Python 3 for the client (standard library only).

```bash
git clone https://github.com/Eyasbusaeed/Dos-project-pt1.git
cd Dos-project-pt1

docker compose up --build          # starts exactly 3 containers: catalog, order, frontend
```

Then, in another terminal on the host:

```bash
python3 client/client.py demo                      # scripted end-to-end demonstration
python3 client/client.py search "distributed systems"
python3 client/client.py info 2
python3 client/client.py purchase 2                # prints: bought book RPCs for Noobs
python3 client/client.py restock 2 5               # adds 5 copies (calls the catalog on :4000 directly)
```

The client uses `http://localhost:5000` (frontend) and `http://localhost:4000` (catalog, for `restock` and the demo's setup steps). Override with `--frontend URL` / `--catalog URL` or the `FRONTEND_URL` / `CATALOG_URL` environment variables.

The `demo` command searches both topics, shows info for all four books, makes purchases, buys an unknown item (404), sells a book out and shows the failure, restocks, and finally fires 20 simultaneous purchases at a book with 5 copies in stock to show that exactly 5 succeed and stock never goes negative.

Every component prints log messages to its console (`docker compose logs -f`), and the order service prints `bought book <title>` for every successful purchase.

Stop with `Ctrl+C` or `docker compose down`.

### Running without Docker (optional)

```bash
CatalogFilePath=/tmp/catalog.csv dotnet run --project src/CatalogService
CATALOG_URL=http://localhost:4000 OrdersFilePath=/tmp/orders.csv dotnet run --project src/OrderService
CATALOG_URL=http://localhost:4000 ORDER_URL=http://localhost:3000 dotnet run --project src/FrontendService
```

(one terminal each; requires the .NET 9 SDK).

## Repository layout

```
src/CatalogService/    catalog microservice (+ Dockerfile)
src/OrderService/      order microservice (+ Dockerfile)
src/FrontendService/   frontend microservice (+ Dockerfile)
client/client.py       command-line client
docker-compose.yml     the three containers and their volumes
docs/                  design document, sample client output (output.txt), service logs (service-logs.txt)
```

## Technologies

C# / .NET 9 / ASP.NET Core Web API, Docker & Docker Compose, CSV files for storage, HTTP REST + JSON, Python 3 client.
