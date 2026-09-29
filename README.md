# PrioritySetter

The PrioritySetter mod for Beat Saber allows you to adjust the priority of the game process to optimize performance. I wrote this because I got sick of setting the priority myself.

*This mod was written at 1 AM on a Wednesday, because who needs sleep?*

## Configuration

PrioritySetter also applies `ProcessPriority` to running SteamVR and Meta Quest Link/Oculus processes. It checks when Beat Saber starts, 10 seconds later, every 60 seconds, and when the game regains focus. New processes are picked up on the next check. A process that Windows does not allow Beat Saber to change is logged and skipped.

`SetVrProcessPriority` defaults to `true`. Set it to `false` to change only Beat Saber. `VrProcessNames` is a comma- or semicolon-separated list of executable names. The default includes SteamVR's server, compositor, monitor, dashboard, web helper, startup, and Home processes; Oculus runtime, service launcher, redirector, dashboard, and client processes; and Virtual Desktop Streamer. Names are matched exactly without the optional `.exe` suffix. Edit the list to add another headset-specific process or remove processes you do not want changed.

The available options are as follows:

| Enum Value    | Description                                                                 |
|---------------|-----------------------------------------------------------------------------|
| `Idle`        | Specifies an idle priority for the process.                                 |
| `BelowNormal` | Specifies a below-normal priority for the process.                          |
| `Normal`      | Specifies a normal priority for the process.                                |
| `AboveNormal` | Specifies an above-normal priority for the process.                         |
| `High`        | Specifies a high priority for the process.                                  |
| `RealTime`    | Specifies a real-time priority for the process. **Requires administrator privileges.** |

**Note:** Setting the process priority to `RealTime` requires administrator privileges. I did implement it but the game **WILL NEVER ASK FOR ADMINISTRATOR PRIVILEDGES**. As such, this mod **DOES NOT SUPPORT** setting the priority to `RealTime`
