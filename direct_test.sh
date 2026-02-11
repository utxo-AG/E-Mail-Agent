#!/bin/bash
cd "/Users/saschatobler/RiderProjects/UTXO E-Mail Agent/UTXO E-Mail Agent/bin/Debug/net9.0"

# Start the application and trigger test mode
echo "Starting application and waiting for test mode prompt..."
(sleep 3; echo "t"; sleep 120) | dotnet "UTXO E-Mail Agent.dll"