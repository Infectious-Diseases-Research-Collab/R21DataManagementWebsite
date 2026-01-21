# R21 Data Management Website

## Features

- **Data Dashboard:**
  - View total enrollee record counts.
  - Monitor record counts grouped by Device ID.
  - Browse the 100 most recent enrollee records from the central SQL Server database.
- **FTP File Management:**
  - Securely list files from the project's data directory on a remote FTP server.
  - Download raw ZIP data files directly.
  - **Automated CSV Export:** Automatically extract SQLite databases from ZIP files and export specific tables (`enrollee`, `formchanges`) to CSV format for easier analysis.
- **Security:** Integrated Windows Authentication (Negotiate) for secure access within the organization.

## Technology Stack

- **Framework:** ASP.NET Core 10.0 Razor Pages
- **Databases:**
  - **MS SQL Server:** Central repository for aggregated enrollee data.
  - **SQLite:** Used for processing local data snapshots extracted from FTP files.
- **Libraries:**
  - `FluentFTP`: For robust FTP/FTPS communication.
  - `Microsoft.Data.SqlClient`: For SQL Server connectivity.
  - `Microsoft.Data.Sqlite`: For SQLite data manipulation and CSV export.
  - `Bootstrap`: For a responsive and clean user interface.

