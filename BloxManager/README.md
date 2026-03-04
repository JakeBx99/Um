# BloxManager

A modern Roblox account manager built with WPF and .NET 8.0.

## Features

### Core Features
- **Account Management**: Add, edit, and manage multiple Roblox accounts
- **Secure Storage**: Local encryption for account data
- **Multi-Roblox Support**: Launch multiple Roblox instances simultaneously
- **Browser Integration**: Built-in browser automation with PuppeteerSharp
- **Game Joining**: Quick join to any game or server
- **Server Browser**: Browse and join specific servers

### Advanced Features
- **Account Groups**: Organize accounts into custom groups
- **Favorites System**: Mark favorite games for quick access
- **Recent Games**: Track recently played games
- **Import/Export**: Backup and restore account data
- **Themes**: Modern Material Design with dark/light themes
- **Developer Mode**: Advanced features for power users
- **Web API**: RESTful API for external integrations

### Security Features
- **Local Encryption**: Account data encrypted using Windows Data Protection API
- **Password Protection**: Optional password-based encryption
- **Cookie Management**: Automatic cookie refresh and validation
- **Secure Storage**: No sensitive data stored in plain text

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Roblox installed (for game launching)

## Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/BloxManager/releases) page
2. Extract the ZIP file to a folder of your choice
3. Run `BloxManager.exe`

## Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/BloxManager.git
   cd BloxManager
   ```

2. Open the solution in Visual Studio 2022
3. Restore NuGet packages
4. Build and run the project

Or using the .NET CLI:
```bash
dotnet restore
dotnet build
dotnet run
```

## Usage

### Adding Accounts
1. Click "Add Account" button
2. Enter username and password, or import a cookie
3. Set alias and group (optional)
4. Click "Save"

### Launching Games
1. Select one or more accounts
2. Enter Place ID (and optionally Job ID)
3. Click "Launch Selected" or "Join Game"

### Browser Integration
1. Select an account
2. Click "Launch Browser" to open a logged-in browser session
3. Use the browser for Roblox web activities

### Settings
Access settings by clicking the gear icon in the top-right corner:
- Enable/disable multi-Roblox
- Configure theme preferences
- Set up developer mode
- Configure web server settings

## API

BloxManager includes a RESTful API for external integrations. By default, it runs on port 8080.

### Endpoints

- `GET /api/accounts` - List all accounts
- `POST /api/accounts/{id}/launch` - Launch account
- `GET /api/games/{placeId}/servers` - Get game servers
- `POST /api/games/join` - Join game with account

See the [API Documentation](docs/api.md) for more details.

## Configuration

Settings are stored in `%APPDATA%\BloxManager\settings.json` (encrypted).

### Key Settings
- `MultiRobloxEnabled`: Enable multiple Roblox instances
- `SavePasswordsEnabled`: Save passwords locally
- `Theme`: UI theme (Light/Dark/System)
- `DeveloperModeEnabled`: Enable developer features
- `WebServerPort`: API server port

## Security

- All account data is encrypted using Windows Data Protection API
- Optional password-based encryption for portable security
- No data is sent to external servers
- Cookies are automatically refreshed and validated

## Troubleshooting

### Common Issues

**Q: Roblox won't launch**
A: Ensure Roblox is properly installed and try enabling multi-Roblox in settings.

**Q: Accounts show as invalid**
A: Try refreshing the accounts. If the issue persists, re-login with credentials.

**Q: Browser automation doesn't work**
A: Make sure Chromium is downloaded automatically on first use.

**Q: Multi-Roblox doesn't work**
A: Enable multi-Roblox in settings and ensure no Roblox processes are running.

### Logs

Application logs are stored in `%APPDATA%\BloxManager\logs\`.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This software is for educational purposes only. Use responsibly and in accordance with Roblox's Terms of Service.

## Support

- Create an issue on GitHub for bug reports
- Join our Discord community for support
- Check the [Wiki](https://github.com/yourusername/BloxManager/wiki) for documentation

## Credits

Based on the original RBX Alt Manager by ic3w0lf22, completely rewritten with modern architecture.
