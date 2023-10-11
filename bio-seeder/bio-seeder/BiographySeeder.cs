namespace bio_seeder;

public class BiographySeeder : IEnumerable<Biography>
{
    public IEnumerator<Biography>GetEnumerator()
    {
        yield return new Biography(
            "Clarissa Harlow",
            "A young woman whose quest for virtue is continually thwarted by her family"
            );

        yield return new Biography(
            "Pamela Andrews", 
            "A young woman whose virtue is rewarded.");

        yield return new Biography(
            "Harriet Byron",
            "An orphan, and heir to a considerable fortune of fifteen thousand pounds"
            );

        yield return new Biography(
            "Charles Grandison", 
            "A man of feeling who truly cannot be said to feel"
            );
    }
    
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}