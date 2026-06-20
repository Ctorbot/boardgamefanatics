import "bootstrap/dist/css/bootstrap.min.css";
import "./globals.css";
import NavMenu from "./nav-menu";

export const metadata = {
  title: "BoardGameFanatics",
  description: "Track board game wins, stats, and collections.",
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>
        <div className="page">
          <div className="sidebar">
            <NavMenu />
          </div>
          <main>{children}</main>
        </div>
      </body>
    </html>
  );
}
