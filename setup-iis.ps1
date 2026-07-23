<#
  Configuration IIS pour Inventory.TPV (ASP.NET Core .NET 10, hébergement in-process)
  - Installe le ASP.NET Core Hosting Bundle si le module IIS est absent
  - Crée le pool d'applications « InventoryTPV » (No Managed Code)
  - Crée le site IIS « InventoryTPV » sur http://localhost:8080
  - Pose les ACL pour l'identité du pool
  - Ajoute le login SQL « IIS APPPOOL\InventoryTPV » (db_owner sur Inventaire)
  Doit être exécuté en administrateur.
#>

$ErrorActionPreference = 'Stop'

$repo     = 'C:\Users\Yveta\source\repos\Inventory.TPV'
$pub      = Join-Path $repo 'publish-iis'
$logsDir  = Join-Path $pub 'logs'
$log      = Join-Path $repo 'setup-iis.log'
$done     = Join-Path $repo 'setup-iis.done'
$site     = 'InventoryTPV'
$pool     = 'InventoryTPV'
$port     = 8080
$appcmd   = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
$hostUrl  = 'https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.9/dotnet-hosting-10.0.9-win.exe'

if (Test-Path $done) { Remove-Item $done -Force }
Start-Transcript -Path $log -Force | Out-Null
$status = 'ECHEC'

try {
    # 0. Vérifier l'élévation
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
                ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { throw "Ce script doit être exécuté en administrateur." }

    Write-Host "=== 1. ASP.NET Core Hosting Bundle (module IIS) ===" -ForegroundColor Cyan
    $ancm = Join-Path $env:windir 'System32\inetsrv\aspnetcorev2.dll'
    if (Test-Path $ancm) {
        Write-Host "Module ANCM déjà présent : $ancm"
    } else {
        $installer = Join-Path $env:TEMP 'dotnet-hosting-10.0.9-win.exe'
        Write-Host "Téléchargement du Hosting Bundle (~112 Mo)..."
        Invoke-WebRequest -Uri $hostUrl -OutFile $installer -UseBasicParsing
        Write-Host "Installation silencieuse..."
        $p = Start-Process $installer -ArgumentList '/install','/quiet','/norestart' -Wait -PassThru
        Write-Host "Code de sortie installateur : $($p.ExitCode)"
        if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) { throw "Installation du Hosting Bundle échouée (code $($p.ExitCode))." }
        Write-Host "Redémarrage d'IIS pour charger le module..."
        & iisreset | Write-Host
        if (-not (Test-Path $ancm)) { throw "Module ANCM toujours absent après installation." }
        Write-Host "Hosting Bundle installé."
    }

    Write-Host "`n=== 2. Pool d'applications ($pool) ===" -ForegroundColor Cyan
    $pools = & $appcmd list apppool /name:$pool
    if ($pools) {
        Write-Host "Pool déjà existant, mise à jour."
    } else {
        & $appcmd add apppool /name:$pool | Write-Host
    }
    # No Managed Code + identité ApplicationPoolIdentity
    & $appcmd set apppool $pool /managedRuntimeVersion:"" | Write-Host
    & $appcmd set apppool $pool /processModel.identityType:ApplicationPoolIdentity | Write-Host
    & $appcmd set apppool $pool /startMode:AlwaysRunning | Write-Host

    Write-Host "`n=== 3. Site IIS ($site) sur le port $port ===" -ForegroundColor Cyan
    $sites = & $appcmd list site /name:$site
    if ($sites) {
        Write-Host "Site déjà existant, mise à jour du chemin et du binding."
        & $appcmd set site /site.name:$site /[path=`'/`'].[path=`'/`'].physicalPath:"$pub" | Write-Host
    } else {
        & $appcmd add site /name:$site /physicalPath:"$pub" /bindings:"http/*:${port}:" | Write-Host
    }
    & $appcmd set app "$site/" /applicationPool:$pool | Write-Host

    Write-Host "`n=== 4. Dossiers et permissions (ACL) ===" -ForegroundColor Cyan
    if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }
    $idAcl = "IIS AppPool\$pool"
    & icacls "$pub" /grant "${idAcl}:(OI)(CI)(RX)" /T /C /Q | Out-Null
    & icacls "$logsDir" /grant "${idAcl}:(OI)(CI)(M)" /T /C /Q | Out-Null
    Write-Host "ACL accordées à $idAcl (lecture/exécution sur le site, modification sur logs)."

    Write-Host "`n=== 5. Accès SQL (IIS APPPOOL\$pool) ===" -ForegroundColor Cyan
    $sqlLogin = "IIS APPPOOL\$pool"
    $conn = New-Object System.Data.SqlClient.SqlConnection "Server=.\SQLEXPRESS01;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
    $conn.Open()
    function Invoke-Sql([string]$sql) { $c = $conn.CreateCommand(); $c.CommandText = $sql; [void]$c.ExecuteNonQuery() }
    Invoke-Sql "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$sqlLogin') CREATE LOGIN [$sqlLogin] FROM WINDOWS;"
    $conn.ChangeDatabase("Inventaire")
    Invoke-Sql "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$sqlLogin') CREATE USER [$sqlLogin] FOR LOGIN [$sqlLogin];"
    Invoke-Sql "ALTER ROLE db_owner ADD MEMBER [$sqlLogin];"
    $conn.Close()
    Write-Host "Login SQL créé et ajouté à db_owner sur la base Inventaire."

    Write-Host "`n=== 6. Démarrage ===" -ForegroundColor Cyan
    & $appcmd start apppool /apppool.name:$pool 2>$null | Write-Host
    & $appcmd start site /site.name:$site 2>$null | Write-Host

    $status = 'OK'
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host " SUCCÈS — application disponible sur :" -ForegroundColor Green
    Write-Host "   http://localhost:$port" -ForegroundColor Green
    Write-Host "   Connexion : admin@inventaire.local / Admin123!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}
catch {
    Write-Host "`nERREUR : $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
}
finally {
    Stop-Transcript | Out-Null
    Set-Content -Path $done -Value $status -Encoding utf8
}

Write-Host "`nJournal complet : $log"
Read-Host "`nAppuyez sur Entrée pour fermer cette fenêtre"
