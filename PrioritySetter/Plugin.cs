using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;

namespace PrioritySetter
{
    [NoEnableDisable]
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        private readonly IPALogger Logger;
        private readonly PrioritySetterConfig Config;
        private readonly Timer RefreshTimer;
        private readonly object RefreshLock = new object();

        [Init]
        public Plugin(IPALogger logger, Config config)
        {
            Config = config.Generated<PrioritySetterConfig>();
            Logger = logger;

            SetPriority(null);

            // Check processes started after the game, then refresh once per minute.
            RefreshTimer = new Timer(SetPriority, null, 10000, 60000);

            // Workaround for windows setting normal priority on window changing focus
            Application.focusChanged += (isFocused) => { if (isFocused) SetPriority(null); };
        }

        [OnExit] 
        public void OnExit()
        {
            RefreshTimer.Dispose();
        }

        private void SetPriority(object state)
        {
            lock (RefreshLock)
            {
                var priority = Config.ProcessPriority;
                using (var thisProcess = Process.GetCurrentProcess())
                {
                    var currentId = thisProcess.Id;
                    ApplyPriority(thisProcess, priority);

                    if (!Config.SetVrProcessPriority) return;

                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in (Config.VrProcessNames ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var name = entry.Trim();
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            name = name.Substring(0, name.Length - 4);

                        if (name.Length == 0 || !names.Add(name)) continue;

                        try
                        {
                            foreach (var process in Process.GetProcessesByName(name))
                            {
                                using (process)
                                {
                                    if (process.Id != currentId)
                                        ApplyPriority(process, priority);
                                }
                            }
                        }
                        catch (Win32Exception ex)
                        {
                            Logger.Warn($"Could not enumerate {name} processes: {ex.Message}");
                        }
                        catch (InvalidOperationException ex)
                        {
                            Logger.Warn($"Could not enumerate {name} processes: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void ApplyPriority(Process process, ProcessPriorityClass priority)
        {
            try
            {
                if (process.PriorityClass == priority) return;
                process.PriorityClass = priority;
                Logger.Info($"Set {process.ProcessName} ({process.Id}) priority to {priority}");
            }
            catch (Win32Exception ex)
            {
                Logger.Warn($"Could not set priority for process {process.Id}: {ex.Message}");
            }
            catch (InvalidOperationException)
            {
                // The process exited between discovery and the priority check.
            }
        }

        public class PrioritySetterConfig
        {
            [UseConverter(typeof(EnumConverter))]
            public virtual ProcessPriorityClass ProcessPriority { get; set; } = ProcessPriorityClass.High;

            public virtual bool SetVrProcessPriority { get; set; } = true;

            public virtual string VrProcessNames { get; set; } =
                "vrserver, vrcompositor, vrmonitor, vrdashboard, vrwebhelper, vrstartup, vrhomesteam, " +
                "OVRServer_x64, OVRServer_x86, OVRServiceLauncher, OVRRedir, OculusDash, OculusClient, " +
                "VirtualDesktop.Streamer";
        }
    }
}
