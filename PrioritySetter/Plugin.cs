using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using IPALogger = IPA.Logging.Logger;

namespace PrioritySetter
{
    [NoEnableDisable]
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        private readonly IPALogger Logger;

        [Init]
        public Plugin(IPALogger logger, Config config)
        {
            var settings = config.Generated<PrioritySetterConfig>();
            Logger = logger;
            var priority = settings.ProcessPriority;
            int currentId;
            using (var process = Process.GetCurrentProcess())
            {
                currentId = process.Id;
                ApplyPriority(process, priority);
            }

            if (!settings.SetVrProcessPriority) return;
            var processNames = settings.VrProcessNames;
            ThreadPool.QueueUserWorkItem(_ => ApplyVrPriorities(priority, processNames, currentId));
        }

        private void ApplyVrPriorities(ProcessPriorityClass priority, string processNames, int currentId)
        {
            try
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in (processNames ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
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
            catch (Exception ex)
            {
                Logger.Warn($"Could not apply startup VR process priorities: {ex.Message}");
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
