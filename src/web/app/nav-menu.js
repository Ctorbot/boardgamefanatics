"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import Drawer from "@mui/material/Drawer";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";

export const drawerWidth = 250;

const navItems = [
  { label: "Home", href: "/" },
  { label: "Players", href: "/players" },
];

export default function NavMenu() {
  const pathname = usePathname();

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: drawerWidth,
        flexShrink: 0,
        "& .MuiDrawer-paper": {
          width: drawerWidth,
          boxSizing: "border-box",
          bgcolor: "grey.900",
          color: "common.white",
        },
      }}
    >
      <Toolbar>
        <Typography
          variant="h6"
          noWrap
          component={Link}
          href="/"
          sx={{ color: "inherit", textDecoration: "none" }}
        >
          BoardGameFanatics
        </Typography>
      </Toolbar>
      <List>
        {navItems.map((item) => (
          <ListItemButton
            key={item.href}
            component={Link}
            href={item.href}
            selected={pathname === item.href}
            sx={{ color: "inherit" }}
          >
            <ListItemText primary={item.label} />
          </ListItemButton>
        ))}
      </List>
    </Drawer>
  );
}
