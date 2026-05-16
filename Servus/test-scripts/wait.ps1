try {
    # Your long-running service loop here
    while ($true) {
        Write-Output "Service running..."
        Start-Sleep 1
    }
}
finally {
    Write-Output "Terminating on break event"
}

