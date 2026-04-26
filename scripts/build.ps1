param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [switch]$Install
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSEdition -ne "Core") {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) {
        $args = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $PSCommandPath
        )

        if ($PSBoundParameters.ContainsKey("GameRoot")) {
            $args += @("-GameRoot", $GameRoot)
        }

        if ($Install) {
            $args += "-Install"
        }

        & $pwsh.Source @args
        exit $LASTEXITCODE
    }
}

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = "<Slay the Spire 2 install folder>"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$dataDir = Join-Path $GameRoot "data_sts2_windows_x86_64"
$sourcePath = Join-Path $repoRoot "src\CoopStatusBubbles.cs"
$manifestPath = Join-Path $repoRoot "mod\CoopStatusBubbles\CoopStatusBubbles.json"
$distRoot = Join-Path $repoRoot "dist"
$distModDir = Join-Path $distRoot "CoopStatusBubbles"
$outputPath = Join-Path $distModDir "CoopStatusBubbles.dll"
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

$resolvedDistRoot = [System.IO.Path]::GetFullPath($distRoot)
$resolvedRepoRoot = [System.IO.Path]::GetFullPath($repoRoot)
if (!$resolvedDistRoot.StartsWith($resolvedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a dist path outside the repository."
}

if (Test-Path -LiteralPath $distModDir) {
    Remove-Item -LiteralPath $distModDir -Recurse -Force
}

New-Item -ItemType Directory -Force $distModDir | Out-Null
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $distModDir "CoopStatusBubbles.json") -Force

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
    $targetModDir = Join-Path $GameRoot "mods\CoopStatusBubbles"
    New-Item -ItemType Directory -Force $targetModDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $distModDir "CoopStatusBubbles.json") -Destination (Join-Path $targetModDir "CoopStatusBubbles.json") -Force
    Copy-Item -LiteralPath (Join-Path $distModDir "CoopStatusBubbles.dll") -Destination (Join-Path $targetModDir "CoopStatusBubbles.dll") -Force
    Write-Host "Installed $targetModDir"
}
