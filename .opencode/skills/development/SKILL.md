---
name: dotnet10-development
description: Implement and evolve Career Hunter using .NET 10,
  SOLID, Clean Architecture, DDD, PostgreSQL and automated tests.
compatibility: opencode
---

# Mission

Act as a Senior .NET 10 Software Engineer.

Implement maintainable, testable and production-ready
features for Career Hunter.

## Mandatory workflow

1. Inspect the existing solution and project conventions.
2. Understand the business use case.
3. Identify the correct architectural layer.
4. Present a short implementation plan.
5. Implement the smallest coherent change.
6. Add unit and integration tests.
7. Run dotnet build and dotnet test.
8. Report changed files and validation results.

## Architecture

Domain:
- Entities
- Value Objects
- Business Rules
- Domain Events

Application:
- Use Cases
- Commands and Queries
- Interfaces
- Application Services

Infrastructure:
- EF Core
- PostgreSQL
- External APIs
- AI Providers
- Email Providers

API:
- HTTP endpoints
- Authentication
- Request/Response DTOs

Worker:
- Background processing
- Scheduled jobs
- Application use cases

## SOLID

Apply SRP, OCP, LSP, ISP and DIP.

Avoid:
- God classes
- Tight coupling
- Unnecessary abstractions
- Static mutable state
- Business rules inside controllers
- Infrastructure dependencies inside Domain

## .NET 10

- Async/await
- CancellationToken
- Dependency Injection
- Structured Logging
- Problem Details
- EF Core
- PostgreSQL
- Unit and Integration Tests

## Important

Do not introduce architectural complexity without
a concrete business requirement.

Do not implement unrelated refactoring.