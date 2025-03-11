# ROMMend

A modern ROM management application built with Avalonia UI.

## Features

- Modern dark theme with acrylic blur effects
- Platform-based filtering and ROM search
- Cover image display with caching
- Download progress tracking
- Responsive and intuitive UI
- Cross-platform support

## Getting Started

1. Clone the repository
2. Copy `settings.template.json` to `settings.json`
3. Configure your settings in `settings.json`:
   ```json
   {
       "Username": "your_username",
       "Password": "your_password",
       "Host": "your_host",
       "DownloadDirectory": "path_to_downloads"
   }
   ```
4. Copy `platform_folders.template.json` to `platform_folders.json`
5. Configure your platform folder names in `platform_folders.json`:
   ```json
   {
       "nes": "Nintendo Entertainment System",
       "snes": "Super Nintendo",
       "gb": "Game Boy",
       "gba": "Game Boy Advance",
       "n64": "Nintendo 64",
       "ps1": "PlayStation",
       "ps2": "PlayStation 2",
       "genesis": "Sega Genesis"
   }
   ```
6. Build and run the application:
   ```bash
   dotnet build
   dotnet run
   ```

## Development

- Built with Avalonia 11.0.5
- Uses MVVM pattern with CommunityToolkit.Mvvm
- JSON-based settings and caching system
- Async operations for improved responsiveness

## Requirements

- .NET 7.0 or later
- An IDE that supports .NET development (Visual Studio, VS Code, Rider)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.
