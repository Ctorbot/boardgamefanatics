import Typography from "@mui/material/Typography";
import Table from "@mui/material/Table";
import TableHead from "@mui/material/TableHead";
import TableBody from "@mui/material/TableBody";
import TableRow from "@mui/material/TableRow";
import TableCell from "@mui/material/TableCell";
import { db } from "../../lib/db";

export const dynamic = "force-dynamic";

export default async function PlayersPage() {
  const players = await db.player.findMany({ orderBy: { name: "asc" } });

  return (
    <>
      <Typography variant="h4" component="h1" gutterBottom>
        Players
      </Typography>

      {players.length === 0 ? (
        <Typography>No players yet.</Typography>
      ) : (
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Games Won</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {players.map((player) => (
              <TableRow key={player.id}>
                <TableCell>{player.name}</TableCell>
                <TableCell>{player.gamesWon}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </>
  );
}
