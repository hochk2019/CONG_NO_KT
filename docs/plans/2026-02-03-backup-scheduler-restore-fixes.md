# Backup Scheduler and Restore Flow Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prevent duplicate scheduled backups, avoid queued jobs getting stuck on lock contention, and clear restore-in-progress notices on UI failures.

**Architecture:** Add a scheduler guard that checks for pending scheduled jobs before enqueueing, and mark jobs as skipped with proper timestamps when advisory lock acquisition fails. Update the admin UI restore handlers to clear transient notices when the API call fails. Keep storage schema unchanged.

**Tech Stack:** .NET 8, EF Core (Npgsql), xUnit, React, Vitest.

---

### Task 1: Guard scheduler enqueue with pending-job check

**Files:**
- Modify: `src/backend/Application/Backups/IBackupService.cs`
- Modify: `src/backend/Infrastructure/Services/BackupService.cs`
- Modify: `src/backend/Api/Services/BackupSchedulerHostedService.cs`
- Modify: `src/backend/Tests.Unit/BackupEndpointAntiforgeryTests.cs`
- Create: `src/backend/Tests.Unit/BackupServicePendingScheduledTests.cs`

**Step 1: Write the failing unit test**

Create `src/backend/Tests.Unit/BackupServicePendingScheduledTests.cs`:

```csharp
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using CongNoGolden.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupServicePendingScheduledTests
{
    [Fact]
    public async Task HasPendingScheduledBackupAsync_ReturnsTrue_WhenQueuedOrRunningExists()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase("backup_pending_scheduled")
            .Options;

        await using var db = new ConGNoDbContext(options);
        db.BackupJobs.Add(new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "scheduled",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BackupService(
            db,
            new TestCurrentUser(),
            new TestMaintenanceState(),
            new BackupQueue(),
            new BackupProcessRunner(),
            NullLogger<BackupService>.Instance,
            new ConfigurationBuilder().Build());

        var hasPending = await service.HasPendingScheduledBackupAsync(CancellationToken.None);

        Assert.True(hasPending);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Username => "test";
        public IReadOnlyList<string> Roles => new[] { "Admin" };
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class TestMaintenanceState : IMaintenanceState
    {
        public bool IsActive => false;
        public string? Message => null;
        public void SetActive(bool active, string? message = null) { }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter FullyQualifiedName~BackupServicePendingScheduledTests`
Expected: FAIL (interface/method missing).

**Step 3: Implement pending scheduled job check**

Update `src/backend/Application/Backups/IBackupService.cs`:

```csharp
Task<bool> HasPendingScheduledBackupAsync(CancellationToken ct);
```

Update `src/backend/Infrastructure/Services/BackupService.cs`:

```csharp
public async Task<bool> HasPendingScheduledBackupAsync(CancellationToken ct)
{
    return await _db.BackupJobs.AnyAsync(
        j => j.Type == "scheduled" && (j.Status == "queued" || j.Status == "running"),
        ct);
}
```

Update `src/backend/Api/Services/BackupSchedulerHostedService.cs`:

```csharp
if (await service.HasPendingScheduledBackupAsync(stoppingToken))
{
    continue;
}

if (now >= nextRun)
{
    await service.EnqueueScheduledBackupAsync(stoppingToken);
}
```

Update stub in `src/backend/Tests.Unit/BackupEndpointAntiforgeryTests.cs`:

```csharp
public Task<bool> HasPendingScheduledBackupAsync(CancellationToken ct) => Task.FromResult(false);
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter FullyQualifiedName~BackupServicePendingScheduledTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add src/backend/Application/Backups/IBackupService.cs \
  src/backend/Infrastructure/Services/BackupService.cs \
  src/backend/Api/Services/BackupSchedulerHostedService.cs \
  src/backend/Tests.Unit/BackupEndpointAntiforgeryTests.cs \
  src/backend/Tests.Unit/BackupServicePendingScheduledTests.cs
git commit -m "fix: guard scheduled backup enqueue"
```

### Task 2: Mark lock-contended jobs as skipped instead of leaving queued

**Files:**
- Modify: `src/backend/Infrastructure/Services/BackupService.cs`
- Create: `src/backend/Tests.Integration/BackupJobLockTests.cs`

**Step 1: Write the failing integration test**

Create `src/backend/Tests.Integration/BackupJobLockTests.cs`:

```csharp
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class BackupJobLockTests
{
    private readonly TestDatabaseFixture _fixture;

    public BackupJobLockTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessJobAsync_WhenLockHeld_MarksJobSkipped()
    {
        await using var db = _fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE congno.backup_jobs, congno.backup_settings RESTART IDENTITY CASCADE;");

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "scheduled",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.BackupJobs.Add(job);
        await db.SaveChangesAsync();

        await using var lockConn = new NpgsqlConnection(_fixture.ConnectionString);
        await lockConn.OpenAsync();
        await using (var lockCmd = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(hashtext('backup_job'))", lockConn))
        {
            var acquired = (bool)(await lockCmd.ExecuteScalarAsync())!;
            Assert.True(acquired);
        }

        var service = new BackupService(
            db,
            new TestCurrentUser(),
            new TestMaintenanceState(),
            new BackupQueue(),
            new BackupProcessRunner(),
            NullLogger<BackupService>.Instance,
            new ConfigurationBuilder().Build());

        await service.ProcessJobAsync(job.Id, CancellationToken.None);

        var refreshed = await db.BackupJobs.AsNoTracking().FirstAsync(j => j.Id == job.Id);
        Assert.Equal("skipped", refreshed.Status);
        Assert.NotNull(refreshed.FinishedAt);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Username => "test";
        public IReadOnlyList<string> Roles => new[] { "Admin" };
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class TestMaintenanceState : IMaintenanceState
    {
        public bool IsActive => false;
        public string? Message => null;
        public void SetActive(bool active, string? message = null) { }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter FullyQualifiedName~BackupJobLockTests`
Expected: FAIL (status still queued).

**Step 3: Implement lock-failure handling**

Update `src/backend/Infrastructure/Services/BackupService.cs`:

```csharp
if (!await TryAcquireAdvisoryLockAsync(BackupLockKey, ct))
{
    _logger.LogInformation("Backup job {JobId} skipped due to advisory lock.", jobId);
    job.Status = "skipped";
    job.FinishedAt = DateTimeOffset.UtcNow;
    job.ErrorMessage = "Skipped due to advisory lock.";

    if (job.Type == "scheduled")
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        settings.LastRunAt = DateTimeOffset.UtcNow;
    }

    await _db.SaveChangesAsync(ct);
    await WriteAuditAsync("backup_" + job.Type, "skipped", new { job.Id, job.Status, job.ErrorMessage }, ct);
    return;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter FullyQualifiedName~BackupJobLockTests`
Expected: PASS.

**Step 5: Commit**

```bash
git add src/backend/Infrastructure/Services/BackupService.cs \
  src/backend/Tests.Integration/BackupJobLockTests.cs
git commit -m "fix: mark lock-contended backups as skipped"
```

### Task 3: Clear restore notice on UI failure

**Files:**
- Modify: `src/frontend/src/pages/AdminBackupPage.tsx`
- Modify: `src/frontend/src/pages/admin/__tests__/admin-backup-page.test.tsx`

**Step 1: Write the failing UI tests**

Update `src/frontend/src/pages/admin/__tests__/admin-backup-page.test.tsx`:

```tsx
it('clears restore notice on job restore failure', async () => {
  const user = userEvent.setup()
  vi.mocked(fetchBackupJobs).mockResolvedValueOnce({
    items: [
      {
        id: 'job-1',
        type: 'manual',
        status: 'success',
        createdAt: new Date().toISOString(),
        fileName: 'backup.dump',
        fileSize: 1024,
        errorMessage: null,
        createdBy: null,
      },
    ],
    page: 1,
    pageSize: 20,
    total: 1,
  })
  vi.mocked(restoreBackup).mockRejectedValueOnce(new ApiError('Restore failed'))
  vi.spyOn(window, 'prompt').mockReturnValue('RESTORE')

  render(
    <MemoryRouter>
      <AuthContext.Provider value={baseAuth}>
        <AdminBackupPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )

  await waitFor(() => {
    expect(screen.getByText('Phục hồi')).toBeInTheDocument()
  })

  await user.click(screen.getByRole('button', { name: 'Phục hồi' }))

  await waitFor(() => {
    expect(screen.queryByText('Đang phục hồi dữ liệu. Vui lòng chờ.')).not.toBeInTheDocument()
  })
  expect(screen.getByText('Restore failed')).toBeInTheDocument()
})
```

**Step 2: Run test to verify it fails**

Run: `npm run test -- --run src/frontend/src/pages/admin/__tests__/admin-backup-page.test.tsx`
Expected: FAIL (notice still visible).

**Step 3: Clear notice in error path**

Update `src/frontend/src/pages/AdminBackupPage.tsx` in both restore handlers:

```tsx
} catch (err) {
  setNotice(null)
  if (err instanceof ApiError) {
    setError(err.message)
  } else {
    setError('Không phục hồi được dữ liệu.')
  }
}
```

**Step 4: Run test to verify it passes**

Run: `npm run test -- --run src/frontend/src/pages/admin/__tests__/admin-backup-page.test.tsx`
Expected: PASS.

**Step 5: Commit**

```bash
git add src/frontend/src/pages/AdminBackupPage.tsx \
  src/frontend/src/pages/admin/__tests__/admin-backup-page.test.tsx
git commit -m "fix: clear restore notice on failure"
```

---

Plan complete and saved to `docs/plans/2026-02-03-backup-scheduler-restore-fixes.md`. Two execution options:

1. Subagent-Driven (this session)
2. Parallel Session (separate)

Which approach?
