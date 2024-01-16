using System.Text.Json;
using Dapper;
using Model;
using MySqlConnector;
using SimpleEventing;

const string ConnectionString = "server=localhost; port=3306; uid=root; pwd=root; database=Lookup";

CancellationTokenSource cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true; // terminate the pump
    cts.Cancel();
};

var messagePump = new DataTypeChannelConsumer<Biography>(
    message => JsonSerializer.Deserialize<Biography>(message.Value),
    biography =>
    {
        Console.WriteLine($"Received biography for {biography.Id}");
        
        var sql = "INSERT INTO Biography (Id, Description) VALUES (@Id, @Description)";
        using var connection = new MySqlConnection(ConnectionString);
        connection.Open();
        var rowsAffected = connection.Execute(sql, biography);
        Console.WriteLine($"{rowsAffected} row(s) inserted.");
        
        return true;
    });

await messagePump.Receive(cts.Token);