using Bloxstrap.AppData;
using Bloxstrap.Integrations;
using Bloxstrap.Models;

namespace Bloxstrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;
        
        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly DiscordRichPresence? RichPresence;

        // set when DogeStrap itself closes Roblox (tray menu, desktop app patch) so that is never mistaken for a crash
        private bool _robloxClosedByUser = false;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        _robloxClosedByUser = true;
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence)
                    RichPresence = new(ActivityWatcher);

                if (App.Settings.Prop.EnableAutoRejoin)
                {
                    ActivityWatcher.OnGameJoin += (_, _) => SaveLastGame();
                    ActivityWatcher.OnGameLeave += (_, _) => ClearLastGame();
                }
            }
            else if (App.Settings.Prop.EnableAutoRejoin)
            {
                App.Logger.WriteLine(LOG_IDENT, "Auto-rejoin needs activity tracking, which is turned off, so auto-rejoin will not work");
            }

            _notifyIcon = new(this);
        }

        public void KillRobloxProcess()
        {
            _robloxClosedByUser = true;
            CloseProcess(_watcherData!.ProcessId, true);
        }

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            ActivityWatcher?.Start();

            // auto-rejoin needs Roblox's exit code, so hold a handle to the process instead of polling the process list
            Process? robloxProcess = App.Settings.Prop.EnableAutoRejoin ? OpenRobloxProcess() : null;

            if (robloxProcess is not null)
            {
                await robloxProcess.WaitForExitAsync();
            }
            else
            {
                while (Utilities.GetProcessesSafe().Any(x => x.Id == _watcherData.ProcessId))
                    await Task.Delay(1000);
            }

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (robloxProcess is not null)
            {
                using (robloxProcess)
                    await TryAutoRejoin(robloxProcess);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        #region Auto-rejoin

        private const int MAX_REJOIN_ATTEMPTS = 3;
        private static readonly TimeSpan RejoinAttemptWindow = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RejoinDelay = TimeSpan.FromSeconds(5);

        private Process? OpenRobloxProcess()
        {
            const string LOG_IDENT = "Watcher::OpenRobloxProcess";

            try
            {
                var process = Process.GetProcessById(_watcherData!.ProcessId);
                _ = process.HasExited; // throws if the process can't be queried (e.g. Roblox is running as admin and we are not)
                return process;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Could not open the Roblox process, auto-rejoin will not be available for this session");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        /// <summary>
        /// Remembers the server that was just joined. The job id is only known once Roblox has written it to its log,
        /// which is why this is done here when the join happens and not when Roblox is launched.
        /// </summary>
        private void SaveLastGame()
        {
            var data = ActivityWatcher!.Data;

            // private and reserved servers can't be rejoined with just a place id and job id
            if (data.ServerType != ServerType.Public || data.PlaceId == 0 || !Guid.TryParse(data.JobId, out _))
            {
                ClearLastGame();
                return;
            }

            App.State.Prop.LastPlaceId = data.PlaceId;
            App.State.Prop.LastJobId = data.JobId;
            App.State.Save();
        }

        private void ClearLastGame()
        {
            if (App.State.Prop.LastPlaceId == 0 && App.State.Prop.LastJobId.Length == 0)
                return;

            App.State.Prop.LastPlaceId = 0;
            App.State.Prop.LastJobId = "";
            App.State.Save();
        }

        /// <summary>
        /// Windows reports an unhandled exception (access violation, stack overflow, ...) as an NTSTATUS error code (0xC0000000 and above)
        /// as the exit code. A normal exit is 0 and "End task" / TerminateProcess is typically 1, so neither counts as a crash.
        /// If your crashes exit with other codes, the exit code of every session is written to the log and this is the only place to change.
        /// </summary>
        private static bool IsCrashExitCode(int exitCode) => unchecked((uint)exitCode) >= 0xC0000000;

        private bool ReserveRejoinAttempt()
        {
            var now = DateTime.UtcNow;

            if (now - App.State.Prop.LastAutoRejoinTime > RejoinAttemptWindow)
                App.State.Prop.AutoRejoinAttempts = 0;

            if (App.State.Prop.AutoRejoinAttempts >= MAX_REJOIN_ATTEMPTS)
                return false;

            App.State.Prop.AutoRejoinAttempts++;
            App.State.Prop.LastAutoRejoinTime = now;
            App.State.Save();
            return true;
        }

        private async Task TryAutoRejoin(Process robloxProcess)
        {
            const string LOG_IDENT = "Watcher::TryAutoRejoin";

            try
            {
                int exitCode = robloxProcess.ExitCode;

                long placeId = App.State.Prop.LastPlaceId;
                string jobId = App.State.Prop.LastJobId;

                App.Logger.WriteLine(LOG_IDENT, $"Roblox exited with code {exitCode} (0x{exitCode:X8})");

                if (_robloxClosedByUser)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Roblox was closed by the user, not rejoining");
                }
                else if (!IsCrashExitCode(exitCode))
                {
                    App.Logger.WriteLine(LOG_IDENT, "The exit code does not look like a crash, not rejoining");
                }
                else if (placeId == 0 || !Guid.TryParse(jobId, out _))
                {
                    App.Logger.WriteLine(LOG_IDENT, "There is no saved public server to rejoin");
                }
                else if (!ReserveRejoinAttempt())
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Already tried {MAX_REJOIN_ATTEMPTS} times in the last {RejoinAttemptWindow.TotalMinutes} minutes, giving up");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Roblox appears to have crashed, rejoining place {placeId} in {RejoinDelay.TotalSeconds} seconds");

                    await Task.Delay(RejoinDelay);

                    // goes through DogeStrap itself, so the new session gets its own watcher (rich presence, auto-rejoin, ...)
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Paths.Process,
                        UseShellExecute = false
                    };

                    startInfo.ArgumentList.Add($"roblox://experiences/start?placeId={placeId}&gameInstanceId={jobId}");

                    Process.Start(startInfo)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to auto-rejoin");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                // a finished session's server must never be reused after some later, unrelated exit
                ClearLastGame();
            }
        }

        #endregion

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _notifyIcon?.Dispose();
            RichPresence?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
