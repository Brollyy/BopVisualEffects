@echo off
setlocal

for %%I in ("%~dp0..") do set "REPO_ROOT=%%~fI"
cd /d "%REPO_ROOT%" || exit /b 1

git config core.hooksPath .githooks || exit /b 1

echo Git hooks enabled: .githooks/pre-commit
