@echo off
setlocal

REM Backup script for congno_golden (customize paths and user)
set PGBIN=C:\Program Files\PostgreSQL\16\bin
set BACKUPDIR=C:\apps\congno\backup\dumps
set DB=congno_golden
set PGUSER=congno_admin

set DATESTAMP=%DATE:~-4%%DATE:~3,2%%DATE:~0,2%_%TIME:~0,2%%TIME:~3,2%
set DATESTAMP=%DATESTAMP: =0%

if not exist "%BACKUPDIR%" mkdir "%BACKUPDIR%"

"%PGBIN%\pg_dump.exe" -h localhost -p 5432 -U %PGUSER% -F c -b -f "%BACKUPDIR%\%DB%_%DATESTAMP%.dump" %DB%
forfiles /p "%BACKUPDIR%" /m *.dump /d -30 /c "cmd /c del @path"

endlocal
