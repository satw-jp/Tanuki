# dev-reload.ps1
# Rhinoが閉じるのを待ってビルドし、同じファイルで再起動する。
# 使い方:
#   1. このスクリプトをターミナルで実行しておく
#   2. Rhinoで "TanukiDevReload" を実行（保存→終了）
#   3. ビルド完了後、Rhinoが自動的に再起動する

$rhinoExe  = "C:\Program Files\Rhino 8\System\Rhino.exe"
$projectDir = "C:\Users\as\Tanuki"
$tmpFile   = "$env:TEMP\tanuki_reload.txt"

Write-Host ""
Write-Host "=== Tanuki Dev Reload ===" -ForegroundColor Cyan
Write-Host "Rhinoで 'TanukiDevReload' コマンドを実行するとビルド→再起動します。"
Write-Host "Ctrl+C で終了。"
Write-Host ""

while ($true) {
    # Rhinoプロセスが起動するまで待つ
    $rhinoRunning = $false
    while (-not $rhinoRunning) {
        $procs = Get-Process -Name "Rhino" -ErrorAction SilentlyContinue
        if ($procs) { $rhinoRunning = $true } else { Start-Sleep -Milliseconds 500 }
    }

    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Rhinoを検出。TanukiDevReloadを待機中..." -ForegroundColor Gray

    # Rhinoが終了するまで待つ
    Wait-Process -Name "Rhino" -ErrorAction SilentlyContinue
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Rhino終了。ビルド開始..." -ForegroundColor Yellow

    # ビルド
    Push-Location $projectDir
    dotnet build -c Release --nologo -v q
    $buildOk = $LASTEXITCODE -eq 0
    Pop-Location

    if ($buildOk) {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ビルド成功。Rhinoを再起動..." -ForegroundColor Green

        # 前回ファイルを読む
        $lastFile = ""
        if (Test-Path $tmpFile) {
            $lastFile = (Get-Content $tmpFile -ErrorAction SilentlyContinue).Trim()
        }

        if ($lastFile -and (Test-Path $lastFile)) {
            Start-Process $rhinoExe -ArgumentList "`"$lastFile`"", '/runscript=_mcpstart'
        } else {
            Start-Process $rhinoExe -ArgumentList '/runscript=_mcpstart'
        }
    } else {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ビルド失敗。エラーを修正してRhinoを閉じると再試行します。" -ForegroundColor Red
        # ビルド失敗時はRhinoを再起動せず、次のループで待機
    }

    Write-Host ""
}
