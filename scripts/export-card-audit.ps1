param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$GameRoot = Resolve-Sts2GameRoot $GameRoot
$repoRoot = Get-HeyListenRepoRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "docs\card-audit"
}
elseif (![System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $repoRoot $OutputDir
}

$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$dataDir = Join-Path $GameRoot "data_sts2_windows_x86_64"
$sts2DllPath = Join-Path $dataDir "sts2.dll"
$godotDllPath = Join-Path $dataDir "GodotSharp.dll"
$pckPath = Join-Path $GameRoot "SlayTheSpire2.pck"
$csvPath = Join-Path $OutputDir "cards.csv"
$jsonPath = Join-Path $OutputDir "cards.json"
$summaryPath = Join-Path $OutputDir "status-detection-summary.md"

if (!(Test-Path -LiteralPath $sts2DllPath)) {
    throw "Could not find sts2.dll under: $dataDir"
}

if (!(Test-Path -LiteralPath $godotDllPath)) {
    throw "Could not find GodotSharp.dll under: $dataDir"
}

if (!(Test-Path -LiteralPath $pckPath)) {
    throw "Could not find SlayTheSpire2.pck under: $GameRoot"
}

function ConvertFrom-JsonEscapedString {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) {
        return ""
    }

    return ('"' + $Value + '"') | ConvertFrom-Json
}

function Get-EnglishLocalization {
    param([Parameter(Mandatory = $true)][string]$PackagePath)

    $rg = Get-Command rg -ErrorAction SilentlyContinue
    if (!$rg) {
        throw "ripgrep (rg) is required for scanning SlayTheSpire2.pck."
    }

    $pattern = '"[A-Z0-9_]+\.(title|description)"\s*:\s*"([^"\\]|\\.)*"'
    $occurrenceCounts = @{}
    $english = @{}

    foreach ($line in & $rg.Source "-a" "-o" $pattern $PackagePath) {
        if ($line -notmatch '"(?<key>[A-Z0-9_]+\.(?:title|description))"\s*:\s*"(?<value>(?:\\.|[^"\\])*)"') {
            continue
        }

        $key = $Matches["key"]
        if (!$occurrenceCounts.ContainsKey($key)) {
            $occurrenceCounts[$key] = 0
        }

        $occurrenceCounts[$key]++

        # The package currently stores German first, English second, then the
        # remaining translations. Keeping the second occurrence gives us the
        # readable card text used for this audit.
        if ($occurrenceCounts[$key] -eq 2) {
            $english[$key] = ConvertFrom-JsonEscapedString $Matches["value"]
        }
    }

    return $english
}

function Remove-Sts2Markup {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    $plain = [regex]::Replace($Text, "\[[^\]]+\]", "")
    $plain = [regex]::Replace($plain, "\{([^}:]+)(?::[^}]*)?\}", '$1')
    $plain = [regex]::Replace($plain, "\s+", " ")
    return $plain.Trim()
}

function Split-CardClauses {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return @()
    }

    $expanded = $Text -replace "\\n", "`n"
    return ($expanded -split "[`r`n.;]+") |
        ForEach-Object { Remove-Sts2Markup $_ } |
        Where-Object { ![string]::IsNullOrWhiteSpace($_) }
}

function Test-StatusMention {
    param(
        [string]$PlainText,
        [string]$Status
    )

    if ([string]::IsNullOrWhiteSpace($PlainText)) {
        return $false
    }

    return [regex]::IsMatch($PlainText, "\b" + [regex]::Escape($Status) + "\b", "IgnoreCase")
}

function Test-EnemyStatusApplication {
    param(
        [string[]]$Clauses,
        [string]$Status
    )

    foreach ($clause in $Clauses) {
        $trimmed = $clause.Trim()
        $applyMatches = [regex]::Matches($trimmed, "\b(?:apply|applies)\b", "IgnoreCase")
        foreach ($applyMatch in $applyMatches) {
            $beforeApply = $trimmed.Substring(0, $applyMatch.Index)
            $afterApply = $trimmed.Substring($applyMatch.Index)

            if ([regex]::IsMatch($beforeApply, "\byou\s*$", "IgnoreCase")) {
                continue
            }

            if ([regex]::IsMatch($afterApply, "\b" + [regex]::Escape($Status) + "\b", "IgnoreCase")) {
                return $true
            }
        }
    }

    return $false
}

function Test-SelfStatusGain {
    param(
        [string[]]$Clauses,
        [string]$Status
    )

    foreach ($clause in $Clauses) {
        if ([regex]::IsMatch($clause, "\b(Gain|Give)\b", "IgnoreCase") -and
            [regex]::IsMatch($clause, "\b" + [regex]::Escape($Status) + "\b", "IgnoreCase")) {
            return $true
        }
    }

    return $false
}

function Test-DoubleDamageSignal {
    param(
        [string]$PlainText,
        [string[]]$Clauses
    )

    if ($Clauses.Count -eq 0) {
        return [regex]::IsMatch($PlainText, "\bDouble Damage\b", "IgnoreCase")
    }

    foreach ($clause in $Clauses) {
        if (Test-IgnoredDoubleDamageSignal $clause) {
            continue
        }

        if ([regex]::IsMatch($clause, "\bdouble\b", "IgnoreCase") -and
            [regex]::IsMatch($clause, "\bdamage\b", "IgnoreCase")) {
            return $true
        }
    }

    return $false
}

function Test-IgnoredDoubleDamageSignal {
    param([string]$Clause)

    return [regex]::IsMatch($Clause, "\bdouble\s+the\s+damage\b.{0,120}\bcards?\s+deal\b", "IgnoreCase") -or
        [regex]::IsMatch($Clause, "^\s*take\s+double\s+damage\b", "IgnoreCase") -or
        [regex]::IsMatch($Clause, "\byou\s+take\s+double\s+damage\b", "IgnoreCase")
}

function Get-PropertyValueText {
    param(
        [object]$Object,
        [string]$PropertyName
    )

    try {
        $value = $Object.$PropertyName
        if ($null -eq $value) {
            return ""
        }

        return $value.ToString()
    }
    catch {
        return ""
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "Reading English localization from $pckPath"
$localization = Get-EnglishLocalization -PackagePath $pckPath

[System.Reflection.Assembly]::LoadFrom($godotDllPath) | Out-Null
$sts2Assembly = [System.Reflection.Assembly]::LoadFrom($sts2DllPath)
$cardModelType = $sts2Assembly.GetType("MegaCrit.Sts2.Core.Models.CardModel")
if (!$cardModelType) {
    throw "Could not find MegaCrit.Sts2.Core.Models.CardModel in sts2.dll."
}

$cardTypes = $sts2Assembly.GetTypes() |
    Where-Object {
        $_.IsClass -and
        !$_.IsAbstract -and
        $_.Namespace -eq "MegaCrit.Sts2.Core.Models.Cards" -and
        $cardModelType.IsAssignableFrom($_)
    } |
    Sort-Object Name

$rows = New-Object "System.Collections.Generic.List[object]"
foreach ($type in $cardTypes) {
    try {
        $card = [Activator]::CreateInstance($type)
    }
    catch {
        Write-Warning "Could not instantiate $($type.FullName): $($_.Exception.Message)"
        continue
    }

    $titleKey = Get-PropertyValueText $card "TitleLocString"
    try {
        if ($card.TitleLocString -and $card.TitleLocString.LocEntryKey) {
            $titleKey = $card.TitleLocString.LocEntryKey
        }
    }
    catch {
    }

    $descriptionKey = ""
    try {
        if ($card.Description -and $card.Description.LocEntryKey) {
            $descriptionKey = $card.Description.LocEntryKey
        }
    }
    catch {
    }

    $title = if ($localization.ContainsKey($titleKey)) { $localization[$titleKey] } else { Get-PropertyValueText $card "Title" }
    $description = if ($localization.ContainsKey($descriptionKey)) { $localization[$descriptionKey] } else { "" }
    $plainDescription = Remove-Sts2Markup $description
    $clauses = Split-CardClauses $description
    $targetType = Get-PropertyValueText $card "TargetType"
    $multiplayerConstraint = Get-PropertyValueText $card "MultiplayerConstraint"
    $cardType = Get-PropertyValueText $card "Type"
    $rarity = Get-PropertyValueText $card "Rarity"
    $supportSignal =
        $targetType -in @("AnyPlayer", "AnyAlly", "AllAllies") -or
        $multiplayerConstraint -eq "MultiplayerOnly" -or
        [regex]::IsMatch(
            $plainDescription,
            "\b(?:another|other)\s+players?\b|\ball\s+(?:players|allies)\b|\b(?:ally|allies|teammates?|support)\b",
            "IgnoreCase")

    $strictCallouts = New-Object "System.Collections.Generic.List[string]"
    if (Test-EnemyStatusApplication $clauses "Vulnerable") { [void]$strictCallouts.Add("Vulnerable") }
    if (Test-EnemyStatusApplication $clauses "Weak") { [void]$strictCallouts.Add("Weak") }
    if (Test-SelfStatusGain $clauses "Strength") { [void]$strictCallouts.Add("Strength") }
    if (Test-SelfStatusGain $clauses "Vigor") { [void]$strictCallouts.Add("Vigor") }
    if (Test-DoubleDamageSignal $plainDescription $clauses) { [void]$strictCallouts.Add("DoubleDamage") }
    if (Test-SelfStatusGain $clauses "Focus") { [void]$strictCallouts.Add("Focus") }
    if (Test-EnemyStatusApplication $clauses "Poison") { [void]$strictCallouts.Add("Poison") }
    if ($supportSignal) { [void]$strictCallouts.Add("Support") }

    $mentionedStatuses = New-Object "System.Collections.Generic.List[string]"
    foreach ($status in @("Vulnerable", "Weak", "Strength", "Vigor", "Focus", "Poison")) {
        if (Test-StatusMention $plainDescription $status) {
            [void]$mentionedStatuses.Add($status)
        }
    }

    if (Test-DoubleDamageSignal $plainDescription @()) {
        [void]$mentionedStatuses.Add("DoubleDamage")
    }

    $mentionOnly = $mentionedStatuses |
        Where-Object { $strictCallouts -notcontains $_ } |
        Sort-Object -Unique

    $row = [pscustomobject]@{
        class_name = $type.Name
        full_type = $type.FullName
        title = $title
        title_key = $titleKey
        description_key = $descriptionKey
        card_type = $cardType
        rarity = $rarity
        target_type = $targetType
        multiplayer_constraint = $multiplayerConstraint
        is_upgradable = Get-PropertyValueText $card "IsUpgradable"
        description = $description
        plain_description = $plainDescription
        mentions_statuses = ($mentionedStatuses -join ";")
        strict_callouts = ($strictCallouts -join ";")
        mention_only_statuses = ($mentionOnly -join ";")
        applies_vulnerable = Test-EnemyStatusApplication $clauses "Vulnerable"
        applies_weak = Test-EnemyStatusApplication $clauses "Weak"
        gains_strength = Test-SelfStatusGain $clauses "Strength"
        gains_vigor = Test-SelfStatusGain $clauses "Vigor"
        grants_double_damage = Test-DoubleDamageSignal $plainDescription $clauses
        gains_focus = Test-SelfStatusGain $clauses "Focus"
        applies_poison = Test-EnemyStatusApplication $clauses "Poison"
        support_signal = $supportSignal
    }

    [void]$rows.Add($row)
}

$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$cardsWithMentionsOnly = $rows |
    Where-Object { ![string]::IsNullOrWhiteSpace($_.mention_only_statuses) } |
    Sort-Object class_name

$topMentionOnly = $cardsWithMentionsOnly |
    Select-Object -First 40 |
    ForEach-Object {
        "- {0} ({1}): mentions {2} but strict signals are {3}" -f $_.title, $_.class_name, $_.mention_only_statuses, $_.strict_callouts
    }

$summary = New-Object "System.Collections.Generic.List[string]"
@(
    "# Card Status Detection Audit",
    "",
    "Exported $($rows.Count) card models from sts2.dll.",
    "",
    "Files:",
    "",
    "- cards.csv",
    "- cards.json",
    "",
    "The strict_callouts column is based on effect phrases such as Apply ... Vulnerable, Apply ... Weak, Gain ... Strength, and Gain ... Focus.",
    "The mention_only_statuses column marks cards that mention a status without a matching apply/gain signal.",
    "",
    "## Mention-Only Cards",
    ""
) | ForEach-Object { [void]$summary.Add($_) }

if ($topMentionOnly.Count -gt 0) {
    foreach ($line in $topMentionOnly) {
        [void]$summary.Add($line)
    }
}
else {
    [void]$summary.Add("- None found.")
}

@(
    "",
    "## Suggested Classifier Change",
    "",
    "Use exact card allowlists or strict effect-phrase matches for status callouts. Do not treat a raw status mention as a status-producing card.",
    "",
    "Good examples:",
    "",
    "- Apply {VulnerablePower:diff()} Vulnerable should produce Vulnerable.",
    "- Apply Weak and Vulnerable to ALL enemies should produce Weak and Vulnerable.",
    "- Gain Strength should produce Strength.",
    "",
    "False-positive examples:",
    "",
    "- If the enemy is Vulnerable, hits twice should not produce Vulnerable.",
    "- for each Vulnerable on the enemy should not produce Vulnerable unless the same card also applies it."
) | ForEach-Object { [void]$summary.Add($_) }

$summary | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Exported $($rows.Count) cards to $OutputDir"
