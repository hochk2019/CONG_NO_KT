# ROLE_MATRIX_UI - Cong No Golden

Legend: Y = allowed, N = not allowed, OWN = allowed for owned customers

| Action | Admin | Supervisor | Accountant | Viewer |
|---|---|---|---|---|
| View dashboards/reports | Y | Y | Y | Y |
| Manage users/roles | Y | N | N | N |
| Lock/unlock period | Y | Y | N | N |
| Import commit/rollback | Y | Y | N | N |
| Approve advance/receipt (owned) | Y | Y | OWN | N |
| Approve advance/receipt (all) | Y | Y | N | N |
| Void with reason | Y | Y | OWN | N |
| Edit master data | Y | Y | OWN | N |
