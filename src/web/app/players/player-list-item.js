"use client";

import Link from "next/link";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";

export default function PlayerListItem({ href, displayName }) {
  return (
    <ListItemButton component={Link} href={href} disableGutters>
      <ListItemText primary={displayName} />
    </ListItemButton>
  );
}
