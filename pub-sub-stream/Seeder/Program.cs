using System.Text.Json;
using bio_seeder;
using Model;
using SimpleEventing;

var biographies = new BiographySeeder();
using var producer = new DataTypeChannelProducer<Biography>(biography => JsonSerializer.Serialize(biography));

foreach(var bio in biographies)
{
    producer.Send(bio);
}

var outstandingCount = producer.Flush(TimeSpan.FromSeconds(10));
while (outstandingCount != 0)
{
    //loop until we flush
    Console.WriteLine($"Still waiting on {outstandingCount} outstanding messages to flush");
    outstandingCount = producer.Flush(TimeSpan.FromSeconds(10));
}
