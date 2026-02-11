# Notifications Improvements

## Goal
Fix severity mismatch and add import notifications while improving unread badge sync.

## Tasks
- [x] Task 1: Update backend tests for default severities and add import-commit notification test → Verify: `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter FullyQualifiedName~NotificationPreferencesTests|FullyQualifiedName~ImportCommitNotificationTests`
- [x] Task 2: Update frontend tests for ALERT critical modal + notifications page mark-read sync → Verify: `node ./node_modules/vitest/vitest.mjs run src/components/notifications/__tests__/notification-toast-host.test.tsx src/pages/__tests__/notifications-page.test.tsx`
- [x] Task 3: Implement backend fixes (default severities + import notifications) → Verify: integration tests pass
- [x] Task 4: Implement frontend fixes (severity mapping + refreshUnread) → Verify: frontend tests pass
- [x] Task 5: Run focused test suite and note results → Verify: tests green

## Done When
- [x] ALERT severity is treated as critical and defaults are consistent in FE/BE
- [x] Import commit creates IMPORT notifications for the acting user
- [x] Notifications page updates unread badge after marking read
