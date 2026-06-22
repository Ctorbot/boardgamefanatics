import Typography from "@mui/material/Typography";
import List from "@mui/material/List";
import { db } from "../../lib/db";
import PlayerListItem from "./player-list-item";

export const dynamic = "force-dynamic";

export default async function PlayersPage() {
  const players = await db.player.findMany({
    where: { status: "APPROVED" },
    orderBy: { displayName: "asc" },
  });

  return (
    <>
      <Typography variant="h4" component="h1" gutterBottom>
        Players
      </Typography>

      {players.length === 0 ? (
        <Typography>No approved players yet.</Typography>
      ) : (
        <List>
          {players.map((player) => (
            <PlayerListItem
              key={player.id}
              href={`/players/${player.id}/collection`}
              displayName={player.displayName}
            />
          ))}
        </List>
      )}
    </>
  );
}
