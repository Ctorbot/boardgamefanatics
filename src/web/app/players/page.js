import { db } from "../../lib/db";

export const dynamic = "force-dynamic";

export default async function PlayersPage() {
  const players = await db.player.findMany({ orderBy: { name: "asc" } });

  return (
    <>
      <h1>Players</h1>

      {players.length === 0 ? (
        <p>No players yet.</p>
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Games Won</th>
            </tr>
          </thead>
          <tbody>
            {players.map((player) => (
              <tr key={player.id}>
                <td>{player.name}</td>
                <td>{player.gamesWon}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
