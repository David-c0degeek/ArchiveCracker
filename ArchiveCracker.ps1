param (
    [Parameter(Mandatory=$true)][string]$filename,
    [Parameter(Mandatory=$false)][string]$mask,
    [Parameter(Mandatory=$false)][switch]$restore
)

# Check if the file is a part of a multi-part archive
if ($filename.EndsWith(".001")) {
    # Combine all parts into a single archive
    Write-Host "Combining multi-part archive..."
    $parts = Get-ChildItem -Filter "*.7z.*" | Sort-Object Name
    $combinedFilename = $filename.TrimEnd(".001") + ".7z"
    foreach ($part in $parts) {
        $content = [System.IO.File]::ReadAllBytes($part.FullName)
        $fs = [System.IO.File]::OpenWrite($combinedFilename)
        $fs.Position = $fs.Length
        $fs.Write($content, 0, $content.Length)
        $fs.Close()
    }
    $filename = $combinedFilename
}

# Check for Hashcat
$hashcatPath = ".\hashcat\hashcat-6.2.6\hashcat.exe"
if (!(Test-Path -Path $hashcatPath)) {
    # Download Hashcat
    Write-Host "Hashcat is not installed. Downloading now..."
    $hashcatUrl = "https://hashcat.net/files/hashcat-6.2.6.7z"
    $hashcatFile = "hashcat.7z"
    Invoke-WebRequest -Uri $hashcatUrl -OutFile $hashcatFile

    # Extract Hashcat
    & 7z x $hashcatFile -o"hashcat"
}

# Check for 7z2hashcat
if (!(Test-Path -Path .\7z2hashcat)) {
    # Clone 7z2hashcat repository
    Write-Host "7z2hashcat repository not found. Cloning now..."
    & git clone https://github.com/philsmd/7z2hashcat.git
} else {
    # Update 7z2hashcat repository
    Write-Host "7z2hashcat repository found. Updating now..."
    Set-Location -Path .\7z2hashcat
    & git pull
    Set-Location -Path ..
}

# Run 7z2hashcat on the file
Write-Host "Running 7z2hashcat on the file..."
$hash = & perl .\7z2hashcat\7z2hashcat.pl $filename

# Run Hashcat on the hash
Write-Host "Running Hashcat on the hash..."

if ([string]::IsNullOrEmpty($mask)) {
    # If no mask was provided, use a default one
    $mask = "?a" * 50
}

$sessionName = $filename

if ($restore) {
    # If the -restore flag was provided, restore the previous session
    & $hashcatPath --restore --session=$sessionName
} else {
    # Otherwise, start a new session
    & $hashcatPath -m 11600 -a 3 -i -w 4 --status-timer=10 --session=$sessionName $hash $mask
}
