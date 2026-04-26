param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$SourceRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "SlayTheSpire2.LAN.Multiplayer")
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

        foreach ($parameter in @("GameRoot", "SourceRoot")) {
            if ($PSBoundParameters.ContainsKey($parameter)) {
                $args += @("-$parameter", $PSBoundParameters[$parameter])
            }
        }

        & $pwsh.Source @args
        exit $LASTEXITCODE
    }
}

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = "<Slay the Spire 2 install folder>"
}

$dataDir = Join-Path $GameRoot "data_sts2_windows_x86_64"
$sourceDir = Join-Path $SourceRoot "SlayTheSpire2.LAN.Multiplayer"
$targetDll = Join-Path $GameRoot "mods\SlayTheSpire2.LAN.Multiplayer\SlayTheSpire2.LAN.Multiplayer.dll"
$runtimeDir = Split-Path -Parent ([System.Text.Json.JsonSerializer].Assembly.Location)

if (!(Test-Path -LiteralPath $sourceDir)) {
    throw "LAN multiplayer source folder not found: $sourceDir"
}

if (!(Test-Path -LiteralPath (Join-Path $dataDir "sts2.dll"))) {
    throw "Could not find sts2.dll under: $dataDir"
}

if (!(Test-Path -LiteralPath (Split-Path -Parent $targetDll))) {
    throw "LAN multiplayer mod is not installed under: $(Split-Path -Parent $targetDll)"
}

function Update-SourceText {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][scriptblock]$Transform
    )

    $text = Get-Content -LiteralPath $Path -Raw
    $updated = & $Transform $text
    if ($updated -ne $text) {
        Set-Content -LiteralPath $Path -Value $updated -NoNewline
    }
}

$invalidCharsSource = @'
        private static readonly Regex InvalidChars = new Regex(
            @"[\x00-\x1F\x7F<>:""/\\|?*\x0B\x0C\x0D&;`#$%^+={}]",
            RegexOptions.Compiled);
'@

$generatedRegexSource = @'
        [GeneratedRegex(@"[\x00-\x1F\x7F<>:""/\\|?*\x0B\x0C\x0D&;`#$%^+={}]")]
        private static partial Regex InvalidCharsRegex();
'@

Update-SourceText -Path (Join-Path $sourceDir "Components\IPAddressLabel.cs") -Transform {
    param($text)
    $text.Replace(
        "                this.ApplyLocaleFontSubstitution(FontType.Regular, ThemeConstants.Label.font);`r`n",
        "").Replace(
        "                this.ApplyLocaleFontSubstitution(FontType.Regular, ThemeConstants.Label.font);`n",
        "")
}

Update-SourceText -Path (Join-Path $sourceDir "Components\CopiedLabel.cs") -Transform {
    param($text)
    $text.Replace(
        "            this.ApplyLocaleFontSubstitution(FontType.Regular, ThemeConstants.Label.font);`r`n",
        "").Replace(
        "            this.ApplyLocaleFontSubstitution(FontType.Regular, ThemeConstants.Label.font);`n",
        "")
}

Update-SourceText -Path (Join-Path $sourceDir "Components\PlayerNameLineEdit.cs") -Transform {
    param($text)
    $text.Replace($generatedRegexSource, $invalidCharsSource).
        Replace("InvalidCharsRegex().IsMatch(playerName)", "InvalidChars.IsMatch(playerName)")
}

Update-SourceText -Path (Join-Path $sourceDir "Models\SettingsModel.cs") -Transform {
    param($text)
    $text.Replace(
        "    [JsonSerializable(typeof(SettingsModel))]`r`n    public partial class SettingsModelContext : JsonSerializerContext;`r`n`r`n",
        "").Replace(
        "    [JsonSerializable(typeof(SettingsModel))]`n    public partial class SettingsModelContext : JsonSerializerContext;`n`n",
        "")
}

Update-SourceText -Path (Join-Path $sourceDir "Models\PlayerNames.cs") -Transform {
    param($text)
    $text.Replace(
        "    [JsonSerializable(typeof(PlayerNames))]`r`n    public partial class PlayerNamesContext : JsonSerializerContext;`r`n`r`n",
        "").Replace(
        "    [JsonSerializable(typeof(PlayerNames))]`n    public partial class PlayerNamesContext : JsonSerializerContext;`n`n",
        "")
}

Update-SourceText -Path (Join-Path $sourceDir "Services\SettingsService.cs") -Transform {
    param($text)
    $text.Replace(
        "            _modsDir.WriteFile(`"lan_settings.json`",`r`n                JsonSerializer.Serialize(SettingsModel, SettingsModelContext.Default.SettingsModel));",
        "            _modsDir.WriteFile(`"lan_settings.json`", JsonSerializer.Serialize(SettingsModel));").Replace(
        "            _modsDir.WriteFile(`"lan_settings.json`",`n                JsonSerializer.Serialize(SettingsModel, SettingsModelContext.Default.SettingsModel));",
        "            _modsDir.WriteFile(`"lan_settings.json`", JsonSerializer.Serialize(SettingsModel));")
}

Update-SourceText -Path (Join-Path $sourceDir "Patchs\RunSaveManagerPatch.cs") -Transform {
    param($text)
    $text.Replace(
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames,`r`n                        PlayerNamesContext.Default.PlayerNames, CancellationToken.None);",
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames,`r`n                        cancellationToken: CancellationToken.None);").Replace(
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames,`n                        PlayerNamesContext.Default.PlayerNames, CancellationToken.None);",
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames,`n                        cancellationToken: CancellationToken.None);").Replace(
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames,`r`n                        PlayerNamesContext.Default.PlayerNames);",
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames);").Replace(
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames,`n                        PlayerNamesContext.Default.PlayerNames);",
        "                    await JsonSerializer.SerializeAsync(playerNamesStream, lanPlayerNameService.PlayerNames);")
}

$codeAnalysisPath = Join-Path $PSHOME "Microsoft.CodeAnalysis.dll"
$codeAnalysisCSharpPath = Join-Path $PSHOME "Microsoft.CodeAnalysis.CSharp.dll"
if (!(Test-Path -LiteralPath $codeAnalysisPath) -or !(Test-Path -LiteralPath $codeAnalysisCSharpPath)) {
    throw "Could not find Roslyn assemblies under $PSHOME. Run this script with pwsh."
}

[System.Reflection.Assembly]::LoadFrom($codeAnalysisPath) | Out-Null
[System.Reflection.Assembly]::LoadFrom($codeAnalysisCSharpPath) | Out-Null

$sourceFiles = Get-ChildItem -LiteralPath $sourceDir -Recurse -Filter *.cs | Select-Object -ExpandProperty FullName
$globalUsings = @"
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net;
global using System.Threading;
global using System.Threading.Tasks;
"@

$parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Latest)

$syntaxTrees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
[void]$syntaxTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    $globalUsings, $parseOptions, "GlobalUsings.cs"))

foreach ($file in $sourceFiles) {
    $source = Get-Content -LiteralPath $file -Raw
    [void]$syntaxTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        $source, $parseOptions, $file))
}

$explicitReferences = @(
    [object].Assembly.Location,
    [System.Text.Json.JsonSerializer].Assembly.Location,
    [System.Text.RegularExpressions.Regex].Assembly.Location,
    [System.Net.IPAddress].Assembly.Location,
    (Join-Path $runtimeDir "System.Runtime.dll"),
    (Join-Path $runtimeDir "System.Collections.dll"),
    (Join-Path $runtimeDir "System.Linq.dll"),
    (Join-Path $runtimeDir "System.Net.Primitives.dll"),
    (Join-Path $runtimeDir "System.Net.NetworkInformation.dll"),
    (Join-Path $runtimeDir "System.Net.Sockets.dll"),
    (Join-Path $runtimeDir "System.Text.Json.dll"),
    (Join-Path $runtimeDir "netstandard.dll"),
    (Join-Path $dataDir "0Harmony.dll"),
    (Join-Path $dataDir "GodotSharp.dll"),
    (Join-Path $dataDir "Steamworks.NET.dll"),
    (Join-Path $dataDir "sts2.dll")
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

$platformReferences = ([AppContext]::GetData("TRUSTED_PLATFORM_ASSEMBLIES") -split ";") |
    Where-Object { $_ }
$allReferences = @($platformReferences + $explicitReferences) | Select-Object -Unique

$metadataReferences = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
foreach ($referencePath in $allReferences) {
    [void]$metadataReferences.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($referencePath))
}

$compilationOptions = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new(
        [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).
    WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).
    WithNullableContextOptions([Microsoft.CodeAnalysis.NullableContextOptions]::Enable)

$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
    "SlayTheSpire2.LAN.Multiplayer",
    $syntaxTrees,
    $metadataReferences,
    $compilationOptions)

$tempDll = "$targetDll.tmp"
$stream = [System.IO.File]::Open(
    $tempDll,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::Read)

try {
    $emit = $compilation.Emit($stream)
}
finally {
    $stream.Dispose()
}

$diagnostics = @($emit.Diagnostics |
    Where-Object { $_.Severity -ge [Microsoft.CodeAnalysis.DiagnosticSeverity]::Warning })
foreach ($diagnostic in $diagnostics) {
    Write-Host $diagnostic.ToString()
}

if (!$emit.Success) {
    Remove-Item -LiteralPath $tempDll -ErrorAction SilentlyContinue
    throw "LAN multiplayer compilation failed."
}

Move-Item -LiteralPath $tempDll -Destination $targetDll -Force
Write-Host "Built patched LAN multiplayer DLL: $targetDll"
