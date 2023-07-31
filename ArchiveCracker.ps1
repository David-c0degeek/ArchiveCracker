param (
    [Parameter(Mandatory=$true)][string]$filename,
    [Parameter(Mandatory=$false)][string]$mask,
    [Parameter(Mandatory=$false)][switch]$restore
)

# Check for Hashcat
if (!(Get-Command "hashcat" -ErrorAction SilentlyContinue)) {
    # Download Hashcat
    Write-Host "Hashcat is not installed. Downloading now..."
    $hashcatUrl = "https://hashcat.net/files/hashcat-6.2.6.7z"
    $hashcatFile = "hashcat.7z"
    Invoke-WebRequest -Uri $hashcatUrl -OutFile $hashcatFile

    # Extract Hashcat
    & 7z x $hashcatFile -o"hashcat"

    # Add Hashcat to the system path
    $env:Path += ";$(Get-Location)\hashcat"
}

# Check for 7z2hashcat
if (!(Get-Command "7z2hashcat" -ErrorAction SilentlyContinue)) {
    # Download hashcat-utils
    Write-Host "7z2hashcat is not installed. Downloading now..."
    $utilsUrl = "https://github.com/hashcat/hashcat-utils/archive/refs/heads/master.zip"
    $utilsFile = "hashcat-utils.zip"
    Invoke-WebRequest -Uri $utilsUrl -OutFile $utilsFile

    # Extract hashcat-utils
    & 7z x $utilsFile -o"hashcat-utils"

    # Build 7z2hashcat
    Set-Location -Path "hashcat-utils\src"
    & gcc -o 7z2hashcat 7z2hashcat.c

    # Add 7z2hashcat to the system path
    $env:Path += ";$(Get-Location)"
    Set-Location -Path "..\.."
}

# Run 7z2hashcat on the file
Write-Host "Running 7z2hashcat on the file..."
$hash = & 7z2hashcat $filename

# Run Hashcat on the hash
Write-Host "Running Hashcat on the hash..."

if ([string]::IsNullOrEmpty($mask)) {
    # If no mask was provided, use a default one
    $mask = "?a?a?a?a?a?a?a?a?a?a?a?a?a?a?a?a?a?a?a?a"
}

$sessionName = $filename

if ($restore) {
    # If the -restore flag was provided, restore the previous session
    & hashcat --restore --session=$sessionName
} else {
    # Otherwise, start a new session
    & hashcat -m 11600 -a 3 --force -w 4 --status-timer=10 --session=$sessionName $hash $mask
}
