# Устанавливает Node.js LTS (версия 22 или новее), если его нет.
# Сначала пробует winget, потом скачивает MSI с nodejs.org.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Test-NodeOk {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) { return $false }
    $major = [int](((& node -v) -replace '^v', '').Split('.')[0])
    return $major -ge 22
}

if (Test-NodeOk) { Write-Host 'Node.js уже установлен.'; exit 0 }

if (Get-Command winget -ErrorAction SilentlyContinue) {
    Write-Host 'Устанавливаю Node.js через winget...'
    & winget install -e --id OpenJS.NodeJS.LTS --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -eq 0 -and (Test-Path "$env:ProgramFiles\nodejs\node.exe")) { exit 0 }
    Write-Host 'winget не справился, скачиваю установщик напрямую...'
}

try {
    Write-Host 'Ищу последнюю LTS-версию Node.js 22...'
    $index = Invoke-RestMethod -Uri 'https://nodejs.org/dist/index.json'
    $version = ($index | Where-Object { $_.lts -and $_.version -like 'v22.*' } | Select-Object -First 1).version
    if (-not $version) { throw 'Не удалось найти версию Node.js 22.' }

    $msi = Join-Path $env:TEMP "node-$version-x64.msi"
    Write-Host "Скачиваю Node.js $version..."
    Invoke-WebRequest -Uri "https://nodejs.org/dist/$version/node-$version-x64.msi" -OutFile $msi

    Write-Host 'Запускаю установку (может появиться запрос прав администратора)...'
    $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList '/i', "`"$msi`"", '/passive', '/norestart' -Verb RunAs -Wait -PassThru
    if ($proc.ExitCode -ne 0) { throw "Установщик вернул код $($proc.ExitCode)." }
    exit 0
} catch {
    Write-Host "Ошибка установки: $($_.Exception.Message)"
    Write-Host 'Установите Node.js LTS вручную: https://nodejs.org/'
    exit 1
}
