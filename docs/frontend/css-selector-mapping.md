# VĐ-Q2 CSS Selector Mapping (Incremental)

## Import order (locked)
1. `tokens.css`
2. `base.css`
3. `primitives.css`
4. `app-shell.css`
5. `feedback.css`
6. `data-display.css`
7. `forms-filters.css`
8. `responsive.css`

## Mapping by concern
| Group | File | Representative selectors |
|---|---|---|
| Tokens / design vars | `src/frontend/src/styles/global/tokens.css` | `:root`, `:root[data-theme='dark']`, `--font-size-*`, `--space-*`, `--color-*` |
| Base / reset / typography | `src/frontend/src/styles/global/base.css` | `*`, `*::before`, `*::after`, `body`, `a`, `#root`, `h1,h2,h3`, `.muted` |
| Primitives (buttons/cards/layout core) | `src/frontend/src/styles/global/primitives.css` | `.btn*`, `.card*`, `.page-stack`, `.page-header`, `.kpi-*`, `.stat-*`, `.table*`, `.chart*`, `.pill*`, `.chip*` |
| App shell + late primitives | `src/frontend/src/styles/global/app-shell.css` | `.tab*`, `.auth-*`, `.export-job*`, `.notification-settings*`, `.zalo-cell*`, `.center-page`, `.unit-toggle*` |
| Feedback / alerts / loading | `src/frontend/src/styles/global/feedback.css` | `.alert*`, `.empty-state*`, `.skeleton`, `.progress*`, `.toast*`, `@keyframes` |
| Data display / report blocks | `src/frontend/src/styles/global/data-display.css` | `.dashboard-*`, `.role-cockpit*`, `.line-chart*`, `.audit-*`, `.notification-*`, `.table-preview*` |
| Forms / filters / search | `src/frontend/src/styles/global/forms-filters.css` | `.field*`, `.filters-*`, `.search-row`, `.advanced-panel*`, `.filter-chip*`, `.form-grid-*`, `.upload-dropzone*` |
| Responsive overrides | `src/frontend/src/styles/global/responsive.css` | `@media (...)` blocks for header, grid, forms, tables, dashboard, notification |

## Duplicate selector scan note
- Duplicate selectors found are expected responsive/override patterns (same selector in `responsive.css`) or intentional state/variant declarations.
- No critical duplicate that indicates accidental conflict after this split pass.
