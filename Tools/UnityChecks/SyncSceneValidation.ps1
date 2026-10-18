param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$Scene = 'Assets/Scenes/MainScene.unity',
    [string]$Target = '.utmp/enemy-build'
)
$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath($ProjectRoot)
$targetRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot $Target))
if (!$targetRoot.StartsWith((Join-Path $sourceRoot '.utmp') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation target must be a dedicated folder under this project .utmp directory.'
}
if (!(Test-Path -LiteralPath (Join-Path $sourceRoot $Scene) -PathType Leaf)) { throw "Missing scene: $Scene" }
$index = @{}
foreach ($meta in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Assets') -Filter '*.meta' -Recurse -File) {
    $match = [regex]::Match([IO.File]::ReadAllText($meta.FullName), '(?m)^guid: ([a-f0-9]{32})')
    if ($match.Success) { $index[$match.Groups[1].Value] = $meta.FullName.Substring(0, $meta.FullName.Length - 5) }
}
$queue = [Collections.Generic.Queue[string]]::new()
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$queue.Enqueue((Join-Path $sourceRoot $Scene))
# Runtime-loaded Resources and terrain layers are not all referenced by scene YAML.
foreach ($folder in @('Assets/Scripts', 'Assets/Resources', 'Assets/Terrains', 'Assets/Settings')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $sourceRoot $folder) -Recurse -File) { $queue.Enqueue($file.FullName) }
}
$copied = 0
while ($queue.Count -gt 0) {
    $path = $queue.Dequeue()
    if (!$visited.Add($path) -or !(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $relative = [IO.Path]::GetRelativePath($sourceRoot, $path)
    $destination = Join-Path $targetRoot $relative
    $directory = Split-Path -Parent $destination
    if (!(Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    Copy-Item -LiteralPath $path -Destination $destination
    $copied++
    if (!$path.EndsWith('.meta') -and (Test-Path -LiteralPath ($path + '.meta'))) { $queue.Enqueue($path + '.meta') }
    # Only text serialization/import metadata is scanned. Large binary FBX and textures are copied intact.
    if ([IO.Path]::GetExtension($path) -notin @('.meta', '.unity', '.prefab', '.mat', '.asset', '.controller', '.anim', '.overridecontroller', '.terrainlayer')) { continue }
    $text = [IO.File]::ReadAllText($path)
    if ($text.Contains([char]0)) { continue }
    foreach ($match in [regex]::Matches($text, 'guid: ([a-f0-9]{32})')) {
        if ($index.ContainsKey($match.Groups[1].Value)) { $queue.Enqueue($index[$match.Groups[1].Value]) }
    }
}
$sourceHash = (Get-FileHash -LiteralPath (Join-Path $sourceRoot $Scene)).Hash
$targetHash = (Get-FileHash -LiteralPath (Join-Path $targetRoot $Scene)).Hash
if ($sourceHash -ne $targetHash) { throw 'Scene copy hash mismatch.' }
[pscustomobject]@{ Scene = $Scene; SourceHash = $sourceHash; ExactSceneCopy = $true; CopiedFiles = $copied; Target = $targetRoot }
