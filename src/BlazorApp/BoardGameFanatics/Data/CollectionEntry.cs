namespace BoardGameFanatics.Data;

public class CollectionEntry
{
    public int Id { get; set; }
    public Guid PlayerId { get; set; }
    public int GameId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Player Player { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
