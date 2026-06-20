import Link from "next/link";

export default function NavMenu() {
  return (
    <div className="navbar navbar-dark ps-3">
      <div className="container-fluid">
        <Link className="navbar-brand text-white" href="/">
          BoardGameFanatics
        </Link>
      </div>

      <nav className="nav flex-column w-100">
        <div className="nav-item px-3">
          <Link className="nav-link text-white" href="/">
            Home
          </Link>
        </div>
        <div className="nav-item px-3">
          <Link className="nav-link text-white" href="/players">
            Players
          </Link>
        </div>
      </nav>
    </div>
  );
}
