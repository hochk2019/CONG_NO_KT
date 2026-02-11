# REPO_STRUCTURE - Cong No Golden

## Proposed layout

/ (repo root)
- /docs/                (phase documents and specs)
- /scripts/             (db, ops, checks)
- /src/
  - /backend/
    - CongNoGolden.sln
    - /Api/
    - /Application/
    - /Domain/
    - /Infrastructure/
    - /Tests.Unit/
    - /Tests.Integration/
  - /frontend/
    - /src/
    - /public/
    - /tests/
- /artifacts/           (build outputs, optional)

## Conventions
- One module per folder in Application and Domain.
- Avoid large files; split by feature.
