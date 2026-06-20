import "./globals.css";
import Box from "@mui/material/Box";
import ThemeRegistry from "./theme-registry";
import NavMenu from "./nav-menu";

export const metadata = {
  title: "BoardGameFanatics",
  description: "Track board game wins, stats, and collections.",
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>
        <ThemeRegistry>
          <Box sx={{ display: "flex" }}>
            <NavMenu />
            <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
              {children}
            </Box>
          </Box>
        </ThemeRegistry>
      </body>
    </html>
  );
}
