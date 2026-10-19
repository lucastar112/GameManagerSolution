# GameManager

**GameManager** is a C# application designed to manage game execution, memory access, and communication between game-specific tools and the running game.

It acts as a central component between the game, the Dolphin Emulator, and optional child applications. By centralizing memory access and exposing an IPC interface, GameManager allows external tools to retrieve game data without independently managing Dolphin memory access.

> **Project status:** Under active development. Some interfaces and features may change.

## Features

- **Dolphin integration:** Launches and monitors Dolphin Emulator.
- **Centralized memory access:** Provides a single point of access to game memory.
- **Resource management:** Loads configured memory resources and supports game-specific data.
- **Inter-process communication:** Allows child applications to communicate with GameManager.
- **Child process management:** Starts and monitors configured applications, including optional game-specific tools.
- **Heartbeat monitoring:** Provides a mechanism for communicating GameManager status to the game.
- **Configuration-driven setup:** Supports game-specific configurations and development/release variants.

## Architecture

GameManager is designed around a central application that coordinates memory access and communication.

```text
                 ┌─────────────────────┐
                 │   Dolphin Emulator  │
                 │    Running Game     │
                 └──────────┬──────────┘
                            │
                     Memory Access
                            │
                 ┌──────────▼──────────┐
                 │     GameManager     │
                 │                     │
                 │ Resource Management │
                 │ API and IPC Server  │
                 │ Child Management    │
                 └──────────┬──────────┘
                            │
                      IPC Communication
                            │
               ┌────────────┴────────────┐
               │                         │
      ┌────────▼────────┐      ┌─────────▼────────┐
      │  Child Tool A   │      │   Child Tool B   │
      └─────────────────┘      └──────────────────┘
```

Child applications can request supported game data through GameManager instead of accessing Dolphin memory directly.

## Requirements

- Windows
- .NET SDK **10.0**
- Dolphin Emulator
- A supported game and its corresponding configuration

Additional requirements may depend on the game and its child applications.

## Configuration

Game-specific configurations are organized by game identifier and build variant.

```text
games/
└── MTW/
    ├── DEV/
    │   └── config/
    └── REL/
        └── config/
```

- `DEV`: Development configuration.
- `REL`: Release configuration.

The configuration files define the resources and child applications used by the selected game setup.

See [`docs/configuration.md`](docs/configuration.md) for further details.

## Documentation

The technical documentation is planned and will be added progressively as GameManager's architecture and interfaces evolve.

The `docs/` directory is currently reserved for documentation that will be developed in future stages of the project, including:

- **Architecture:** Overview of GameManager's internal components and how they interact.
- **Configuration:** Explanation of game-specific configuration files and build variants.
- **IPC communication:** Description of the inter-process communication protocol and client registration.
- **GameManager API:** Documentation of the available interfaces, operations, and resource access.
- **Doxygen:** Generated API documentation based on source-code comments.

Detailed explanations, API usage examples, and IPC examples will be provided once the relevant interfaces are sufficiently stable.

## NuGet Dependencies

| Project | Package | Version | Purpose |
|---|---|---|---|
| `GameManager.Dolphin` | `Dolphin.Memory.Access` | `1.0.3` | `Tiny barebones library used to access the memory of Dolphin emulator on Windows.` |

## Related Repositories

GameManager is part of a modular ecosystem. Its reusable libraries and mod-specific example applications are maintained in separate repositories.

| Repository | Purpose |
|---|---|
| [GameManager.Shared](REPLACE_WITH_SHARED_REPOSITORY_URL) | Shared interfaces and IPC communication components. |
| [GameManager.Dolphin](REPLACE_WITH_DOLPHIN_REPOSITORY_URL) | Reusable Dolphin Emulator integration and memory access functionality. |
| [MTW](REPLACE_WITH_MTW_REPOSITORY_URL) | Mod-specific applications, including MTW, DebugManager, and future child tools. This repository also serves as an example integration. |

The shared libraries are intended to be reusable independently of the MTW mod. The MTW repository demonstrates how the framework can be extended with game-specific functionality.

The related repositories can be included in a complete development checkout through Git submodules. See the development documentation for setup instructions once they are available.

## Development Status

GameManager is currently under development. Its architecture and interfaces may evolve as memory resources, IPC communication, and child-process management are refined.

API usage examples and detailed IPC documentation will be added to the `docs/` directory as the implementation progresses.

## License

This project is under a MIT license, see [`License`](LICENSE) for further details.
