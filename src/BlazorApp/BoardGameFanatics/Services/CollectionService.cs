using BoardGameFanatics.Data;
using Microsoft.EntityFrameworkCore;

namespace BoardGameFanatics.Services;

public class CollectionService(AppDbContext db, BggService bgg)
{
    public async Task AddGameAsync(Guid playerId, int bggId)
    {
        await bgg.FindOrCacheGameAsync(bggId);

        var exists = await db.CollectionEntries
            .AnyAsync(e => e.PlayerId == playerId && e.GameId == bggId);
        if (exists) return;

        db.CollectionEntries.Add(new CollectionEntry { PlayerId = playerId, GameId = bggId });
        await db.SaveChangesAsync();
    }

    public async Task RemoveGameAsync(Guid playerId, int entryId)
    {
        var entry = await db.CollectionEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.PlayerId == playerId);
        if (entry is null) return;

        db.CollectionEntries.Remove(entry);
        await db.SaveChangesAsync();
    }
}
