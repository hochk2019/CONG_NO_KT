# CODING_STANDARDS - Cong No Golden

## General
- File length <= 300 LOC, function length <= 50 LOC.
- No business logic in controllers; use Application services.
- Prefer pure functions for AllocationEngine.

## C#
- Use async/await for IO.
- Use `DateOnly` for date fields, `DateTimeOffset` for timestamps.
- Use `ProblemDetails` for API errors with stable error codes.
- Validate inputs via FluentValidation (or equivalent).
- Do not use `dynamic`.

## Frontend
- TypeScript strict mode.
- Centralized API client with error handling.
- Server-side pagination/filter/sort only.

## Testing
- Unit tests for AllocationEngine and validators.
- Integration tests for import commit and period lock.
