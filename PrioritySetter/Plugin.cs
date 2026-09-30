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
        private int RefreshQueued;
        private int RefreshRequested;
        private int Stopped;

        [Init]
        public Plugin(IPALogger logger, Config config)
        {
            Config = config.Generated<PrioritySetterConfig>();
            Logger = logger;

            QueuePriorityRefresh(null);

            // Check processes started after the game, then refresh once per minute.
            RefreshTimer = new Timer(QueuePriorityRefresh, null, 10000, 60000);

            // Workaround for windows setting normal priority on window changing focus
            Application.focusChanged += OnFocusChanged;
        }

        [OnExit] 
        public void OnExit()
        {
            Interlocked.Exchange(ref Stopped, 1);
            Application.focusChanged -= OnFocusChanged;
            RefreshTimer.Dispose();
        }

        private void OnFocusChanged(bool isFocused)
        {
            if (isFocused) QueuePriorityRefresh(null);
        }

        private void QueuePriorityRefresh(object state)
        {
            if (Volatile.Read(ref Stopped) != 0) return;
            Interlocked.Exchange(ref RefreshRequested, 1);
            if (Interlocked.CompareExchange(ref RefreshQueued, 1, 0) != 0) return;
            if (!ThreadPool.QueueUserWorkItem(RunPriorityRefresh)) Interlocked.Exchange(ref RefreshQueued, 0);
        }

        private void RunPriorityRefresh(object state)
        {
            try
            {
                while (Volatile.Read(ref Stopped) == 0 && Interlocked.Exchange(ref RefreshRequested, 0) != 0)
                    SetPriority(state);
            }
            catch (Exception exception) { Logger.Warn($"Could not refresh process priorities: {exception.Message}"); }
            finally
            {
                Interlocked.Exchange(ref RefreshQueued, 0);
                if (Volatile.Read(ref RefreshRequested) != 0) QueuePriorityRefresh(null);
            }
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
                        if (Volatile.Read(ref Stopped) != 0) return;
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
