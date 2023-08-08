dotnet build -c Release ./src

export DB_CONNECTION_STRING="Host=localhost;Username=admin;Password=123;Database=rinha;Timeout=5;Command Timeout=5;Pooling=true;Maximum Pool Size=300;Connection Pruning Interval=1;Connection Idle Lifetime=2"

dotnet $HOME/projects/rinha-de-backend-2023-q3-csharp/src/bin/Release/net7.0/rinha-de-backend-2023-q3-csharp.dll