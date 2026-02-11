# REVIEW_CHECKLIST - Cong No Golden

- [ ] No file > 300 LOC or function > 50 LOC.
- [ ] Controller thin; business logic in Application.
- [ ] Ownership and role checks enforced.
- [ ] Period lock enforced where required.
- [ ] Idempotency key honored on import commit.
- [ ] Cached balances updated in same transaction.
- [ ] Audit log written for sensitive actions.
- [ ] No raw SQL without parameters.
- [ ] Tests updated for new logic.
