param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Script,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ScriptArgs
)

$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$rutaScript = Join-Path $raiz $Script
if (-not (Test-Path $rutaScript -PathType Leaf)) {
    Write-Error "No existe el script '$Script' dentro del repositorio."
    exit 2
}

$candidatos = @()
$git = Get-Command git.exe -ErrorAction SilentlyContinue
if ($git) {
    $raizGit = Split-Path (Split-Path $git.Source -Parent) -Parent
    $candidatos += Join-Path $raizGit 'bin\bash.exe'
}

if ($env:ProgramFiles) {
    $candidatos += Join-Path $env:ProgramFiles 'Git\bin\bash.exe'
}
if (${env:ProgramFiles(x86)}) {
    $candidatos += Join-Path ${env:ProgramFiles(x86)} 'Git\bin\bash.exe'
}

$bash = $candidatos | Where-Object { Test-Path $_ -PathType Leaf } | Select-Object -First 1
if (-not $bash) {
    Write-Error 'No se encontró Git Bash. Instala Git for Windows y vuelve a intentarlo.'
    exit 127
}

$msysNoPathConvAnterior = $env:MSYS_NO_PATHCONV
Push-Location $raiz
try {
    $env:MSYS_NO_PATHCONV = '1'
    & $bash $rutaScript @ScriptArgs
    $codigoSalida = $LASTEXITCODE
}
finally {
    Pop-Location
    if ($null -eq $msysNoPathConvAnterior) {
        Remove-Item Env:MSYS_NO_PATHCONV -ErrorAction SilentlyContinue
    }
    else {
        $env:MSYS_NO_PATHCONV = $msysNoPathConvAnterior
    }
}

exit $codigoSalida
