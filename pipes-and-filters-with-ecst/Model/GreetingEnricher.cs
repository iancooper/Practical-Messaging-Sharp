using System;
using System.Data;
using Dapper;
using MySqlConnector;
using SimpleMessaging;

namespace Model
{
    public class GreetingEnricher : IAmAnOperation<Greeting, EnrichedGreeting>
    {
        private const string ConnectionString = "server=localhost; port=3306; uid=root; pwd=root; database=Lookup";

        public EnrichedGreeting Execute(Greeting message)
        {
            Console.WriteLine($"Received greeting {message.Salutation}");
            
            var enriched = new EnrichedGreeting();
            enriched.Salutation = message.Salutation;
            
            enriched.Recipient = "Clarissa Harlow";
            Console.WriteLine($"Enriched with {enriched.Recipient}");
           
            enriched.Bio = LookupBio(enriched.Recipient);
            Console.WriteLine($"Enriched with {enriched.Bio}");
            
            return enriched;
        }

        private string LookupBio(string enrichedRecipient)
        {
            //connect to Sqlite Db
            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();
            
            //create a query for the bio
            string biography = GetBiography(connection, enrichedRecipient);

            if (biography != null)
                Console.WriteLine($"Biography for {enrichedRecipient}:\n{biography}");
            else
                Console.WriteLine($"Biography for {enrichedRecipient} not found.");

            return biography;
        }
       
        static string GetBiography(IDbConnection connection, string name)
        {
            string query = "SELECT Biography FROM Biography WHERE Name = @Name";
            string result = connection.QueryFirstOrDefault<string>(query, new { Name = name });
            return result;
        }
        
    }
}