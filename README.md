# Currency Converter API

## Stack
- .NET 8
- ASP.NET Core Web API
- Redis
- Serilog + Seq
- OpenTelemetry + Jaeger
- JWT Authentication

## Architecture

### Design Patterns
- Factory pattern for exchange rate providers
- Strategy pattern for rate providers
- Decorator pattern for caching
- Dependency Injection with interface abstractions

### Resilience
- Redis cache with 30-day retention
- Retry policy: 3 attempts with exponential backoff
- Circuit breaker: 3 failures, 30s break

### Security
- JWT authentication with role-based claims
- RBAC: User/Admin roles
- Excluded currencies(via appsettings)
- Rate limiting: 30 requests/minute per client
  - Sliding window: 1 minute
  - Per-client tracking via IP + JWT client ID
  - 429 Too Many Requests response

### Monitoring
- Structured logging
- Correlation IDs for request tracing
- Serilog → Seq (dev), CloudWatch (prod)
- OpenTelemetry → Jaeger (dev), X-Ray (prod)

### Testing
- Unit tests: 90%+ coverage
  - [Coverage Report](https://artemshadrunov.github.io/currency-api/coverage/html/index.html)
- Integration tests with Frankfurter API
- Coverage reports: Cobertura XML + HTML

### Deployment
- AWS Lambda + API Gateway
- ElastiCache (Redis)
- Multi-environment: dev/prod
- API versioning: v1
- Horizontal scaling via Lambda

## API Endpoints

### Auth
```http
POST /auth/token
Content-Type: application/json

{
    "username": "testuser",
    "role": "User(or Admin)"
}
```

### Currency
```http
GET /latest
GET /convert?from=USD&to=EUR&amount=100
GET /history?from=USD&to=EUR&startDate=2024-01-01&endDate=2024-03-01
Authorization: Bearer {jwt_token}
```

## Development

### Prerequisites
- .NET 8 SDK
- Docker
- Docker Compose

### VSCode Tasks
1. **Start Local App**
   - Starts Redis, Seq, Jaeger
   - Runs API in Development mode

2. **Run tests with coverage**
   - Generates Cobertura XML report

3. **Generate coverage HTML report**
   - Available in `coverage/html`

4. **CDK Deploy**
   - Deploys to AWS using test-account profile

### Configuration
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "CurrencyConverter_"
  },
  "Seq": {
    "ServerUrl": "http://localhost:5341"
  },
  "OpenTelemetry": {
    "Endpoint": "http://localhost:4317"
  }
}
```