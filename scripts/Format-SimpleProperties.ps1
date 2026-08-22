param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$files = Get-ChildItem -Path $repositoryRoot -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/]bin[\\/]|[\\/]obj[\\/]' }

# Only matches properties whose body contains get; set; or get; init; and no
# other accessor logic. The declaration is kept intact; only whitespace changes.
$propertyPattern = '(?ms)^(?<attributes>(?:[ \t]*\[[^\r\n]+\]\r?\n)*)(?<indent>[ \t]*)(?<declaration>(?:(?:public|private|protected|internal|static|sealed|virtual|override|abstract|new|partial|required|readonly|unsafe|ref)[ \t]+)*(?:[\w<>,.?\[\]]+)[ \t]+\w+)(?<body>\s*\{\s*(?<getter>get;)\s*(?<setter>set;|init;)\s*\})'

$violations = [System.Collections.Generic.List[string]]::new()

foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $updated = [regex]::Replace($text, $propertyPattern, {
        param($match)

        $attributes = $match.Groups['attributes'].Value
        $indent = $match.Groups['indent'].Value
        $declaration = $match.Groups['declaration'].Value.Trim()
        $getter = $match.Groups['getter'].Value
        $setter = $match.Groups['setter'].Value
        $inline = "$attributes$indent$declaration { $getter $setter }"

        if ($match.Value -ne $inline) {
            $relativePath = [System.IO.Path]::GetRelativePath($repositoryRoot, $file.FullName)
            $violations.Add($relativePath)
        }

        return $inline
    })

    if (-not $Check -and $updated -ne $text) {
        [System.IO.File]::WriteAllText($file.FullName, $updated)
    }
}

$uniqueViolations = @($violations | Sort-Object -Unique)
if ($uniqueViolations.Count -gt 0) {
    if ($Check) {
        Write-Error "Simple auto-properties are not inline in: $($uniqueViolations -join ', ')"
        exit 1
    }

    Write-Output "Formatted simple auto-properties in: $($uniqueViolations -join ', ')"
} else {
    Write-Output 'No simple auto-property formatting changes were needed.'
}
