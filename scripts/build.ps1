param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [switch]$Install
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

if ($PSVersionTable.PSEdition -ne "Core") {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) {
        $args = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $PSCommandPath
        )

        foreach ($parameter in @("GameRoot", "BuildRoot")) {
            if ($PSBoundParameters.ContainsKey($parameter)) {
                $args += @("-$parameter", $PSBoundParameters[$parameter])
            }
        }

        if ($Install) {
            $args += "-Install"
        }

        & $pwsh.Source @args
        exit $LASTEXITCODE
    }
}

$GameRoot = Resolve-Sts2GameRoot $GameRoot
$repoRoot = Split-Path -Parent $PSScriptRoot
$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
$dataDir = Join-Path $GameRoot "data_sts2_windows_x86_64"
$sourcePath = Join-Path $repoRoot "src\HeyListen.cs"
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$translationsSourceDir = Join-Path $repoRoot "mod\heylisten\translations"
$distModDir = Join-Path $BuildRoot "heylisten"
$outputPath = Join-Path $distModDir "heylisten.dll"
$runtimeDir = Split-Path -Parent ([System.Text.RegularExpressions.Regex].Assembly.Location)

if (!(Test-Path -LiteralPath $GameRoot)) {
    throw "GameRoot does not exist: $GameRoot"
}

if (!(Test-Path -LiteralPath (Join-Path $dataDir "sts2.dll"))) {
    throw "Could not find sts2.dll under: $dataDir"
}

if (!(Test-Path -LiteralPath $sourcePath)) {
    throw "Source file missing: $sourcePath"
}

if (!(Test-Path -LiteralPath $manifestPath)) {
    throw "Manifest file missing: $manifestPath"
}

$references = @(
    [object].Assembly.Location,
    (Join-Path $runtimeDir "System.Runtime.dll"),
    (Join-Path $runtimeDir "System.Collections.dll"),
    (Join-Path $runtimeDir "System.Linq.dll"),
    (Join-Path $runtimeDir "netstandard.dll"),
    (Join-Path $dataDir "0Harmony.dll"),
    (Join-Path $dataDir "GodotSharp.dll"),
    (Join-Path $dataDir "sts2.dll"),
    [System.Text.RegularExpressions.Regex].Assembly.Location
) | Where-Object { Test-Path $_ } | Select-Object -Unique

function Invoke-RoslynCompile {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$OutputAssemblyPath,
        [Parameter(Mandatory = $true)][string[]]$ReferencePaths
    )

    $codeAnalysisPath = Join-Path $PSHOME "Microsoft.CodeAnalysis.dll"
    $codeAnalysisCSharpPath = Join-Path $PSHOME "Microsoft.CodeAnalysis.CSharp.dll"
    if (!(Test-Path $codeAnalysisPath) -or !(Test-Path $codeAnalysisCSharpPath)) {
        return $false
    }

    [System.Reflection.Assembly]::LoadFrom($codeAnalysisPath) | Out-Null
    [System.Reflection.Assembly]::LoadFrom($codeAnalysisCSharpPath) | Out-Null

    $platformReferences = ([AppContext]::GetData("TRUSTED_PLATFORM_ASSEMBLIES") -split ";") |
        Where-Object { $_ }
    $allReferences = ($platformReferences + $ReferencePaths) | Select-Object -Unique

    $metadataReferences = New-Object "System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]"
    foreach ($refPath in $allReferences) {
        [void]$metadataReferences.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($refPath))
    }

    $source = Get-Content -LiteralPath $SourcePath -Raw
    $parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
        [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Latest)
    $syntaxTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source, $parseOptions, $SourcePath)
    $syntaxTrees = New-Object "System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]"
    [void]$syntaxTrees.Add($syntaxTree)

    $compilationOptions = New-Object Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
        [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)
    $compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
        [System.IO.Path]::GetFileNameWithoutExtension($OutputAssemblyPath),
        $syntaxTrees,
        $metadataReferences,
        $compilationOptions)

    $stream = [System.IO.File]::Open(
        $OutputAssemblyPath,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::Read)

    try {
        $emit = $compilation.Emit($stream)
    }
    finally {
        $stream.Dispose()
    }

    if (-not $emit.Success) {
        $diagnostics = $emit.Diagnostics |
            Where-Object { $_.Severity -ne [Microsoft.CodeAnalysis.DiagnosticSeverity]::Hidden }
        foreach ($diagnostic in $diagnostics) {
            Write-Error $diagnostic.ToString()
        }

        throw "Roslyn compilation failed."
    }

    return $true
}

New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null
Assert-SafeBuildRootPath -BuildRoot $BuildRoot -Path $distModDir
if (Test-Path -LiteralPath $distModDir) {
    Remove-Item -LiteralPath $distModDir -Recurse -Force
}

New-Item -ItemType Directory -Force $distModDir | Out-Null
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $distModDir "heylisten.json") -Force
if (Test-Path -LiteralPath $translationsSourceDir) {
    Copy-Item -LiteralPath $translationsSourceDir -Destination $distModDir -Recurse -Force
}

if (-not (Invoke-RoslynCompile -SourcePath $sourcePath -OutputAssemblyPath $outputPath -ReferencePaths $references)) {
    Add-Type `
        -OutputAssembly $outputPath `
        -OutputType Library `
        -IgnoreWarnings `
        -ReferencedAssemblies $references `
        -TypeDefinition (Get-Content $sourcePath -Raw)
}

Write-Host "Built $outputPath"

if ($Install) {
    $targetModDir = Join-Path $GameRoot "mods\heylisten"
    $legacyTargetModDir = Join-Path $GameRoot ("mods\" + "Coop" + "Callouts")
    if (Test-Path -LiteralPath $legacyTargetModDir) {
        Remove-Item -LiteralPath $legacyTargetModDir -Recurse -Force
        Write-Host "Removed legacy install $legacyTargetModDir"
    }

    New-Item -ItemType Directory -Force $targetModDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $distModDir "heylisten.json") -Destination (Join-Path $targetModDir "heylisten.json") -Force
    Copy-Item -LiteralPath (Join-Path $distModDir "heylisten.dll") -Destination (Join-Path $targetModDir "heylisten.dll") -Force
    $distTranslationsDir = Join-Path $distModDir "translations"
    if (Test-Path -LiteralPath $distTranslationsDir) {
        $targetTranslationsDir = Join-Path $targetModDir "translations"
        $resolvedTargetModDir = [System.IO.Path]::GetFullPath($targetModDir)
        $resolvedTargetTranslationsDir = [System.IO.Path]::GetFullPath($targetTranslationsDir)
        $relativeTargetTranslationsDir = [System.IO.Path]::GetRelativePath($resolvedTargetModDir, $resolvedTargetTranslationsDir)
        if ($relativeTargetTranslationsDir -ne "translations") {
            throw "Refusing to clean an unexpected translations path: $targetTranslationsDir"
        }

        if (Test-Path -LiteralPath $targetTranslationsDir) {
            Remove-Item -LiteralPath $targetTranslationsDir -Recurse -Force
        }

        Copy-Item -LiteralPath $distTranslationsDir -Destination $targetModDir -Recurse -Force
    }

    Write-Host "Installed $targetModDir"
}
