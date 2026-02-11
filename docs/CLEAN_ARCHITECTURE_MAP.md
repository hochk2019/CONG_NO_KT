# CLEAN_ARCHITECTURE_MAP - Cong No Golden

## Layer map (high level)

[API]
  - Controllers, DTOs, auth filters
        |
        v
[Application]
  - Use cases, validators, policies, interfaces
        |
        v
[Domain]
  - Entities, value objects, allocation rules

[Infrastructure]
  - EF Core, Dapper, file import, external services
  - Implements Application interfaces

## Key rules
- Domain has no dependencies.
- Application has no framework code.
- Infrastructure is replaceable.
