using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;
using Serilog.Core;
using Serilog.Events;

namespace ME3TweaksModManager.modmanager.milan
{
    /// <summary>
    /// What Project Milan needs to generate a slash manifest for an unslashed installation.
    /// </summary>
    internal class MilanSlashRequest
    {
        public string GameRoot { get; set; }
        public List<string> ModOutputs { get; set; }
        /// <summary>
        /// Basegame TFCs that weren't copied in; read them from LE3 instead.
        /// </summary>
        public Dictionary<string, string> FilesLeftInLE3 { get; set; }
        /// <summary>
        /// The vanilla LE3 copy of each file MilanDelta patched.
        /// </summary>
        public Dictionary<string, string> VanillaOfPatched { get; set; }
        /// <summary>
        /// Files that aren't part of the final state, so slash must not touch them.
        /// </summary>
        public List<string> ExcludedFiles { get; set; }
    }

    /// <summary>
    /// Lets a Project Milan build drive Mod Manager (--milanbuild). Project Milan hosts the pipe; we connect once the UI is up,
    /// say hello, and then wait for it to ask for a base install. Slash generation is handed back to Project Milan.
    /// </summary>
    [Localizable(false)]
    internal class MilanBuildSession
    {
        private const int ProtocolVersion = 1;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static MilanBuildSession Current { get; private set; }

        private readonly NamedPipeClientStream pipe;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;
        private readonly object writeLock = new();
        private TaskCompletionSource<JsonNode> slashReply;

        /// <summary>
        /// Set while a build is running, so the log only gets sent back for the build and not for whatever else the user does.
        /// </summary>
        private volatile bool forwardLogs;
        private volatile bool closed;

        private MilanBuildSession(NamedPipeClientStream pipe)
        {
            this.pipe = pipe;
            reader = new StreamReader(pipe);
            writer = new StreamWriter(pipe) { AutoFlush = true };
        }

        /// <summary>
        /// Connects to the Project Milan build at the given pipe. Called once the UI is ready.
        /// </summary>
        public static void Start(string pipeName)
        {
            Current?.Close();
            Task.Run(() =>
            {
                try
                {
                    var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    pipe.Connect(30000);
                    var session = new MilanBuildSession(pipe);
                    Current = session;
                    M3Log.Information($"Connected to Project Milan build on pipe {pipeName}");
                    session.Send(new { type = "hello", version = App.AppVersionHR, debug = App.IsDebug });
                    if (!App.IsDebug)
                    {
                        // Only development builds can do this. Project Milan tells the user.
                        session.Close();
                        return;
                    }
                    session.ReadLoop();
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, $"Project Milan build session on pipe {pipeName} failed:");
                }
            });
        }

        private void ReadLoop()
        {
            try
            {
                while (!closed && reader.ReadLine() is { } line)
                {
                    var message = JsonNode.Parse(line);
                    switch ((string)message?["type"])
                    {
                        case "buildBase":
                            StartBuild(message);
                            break;
                        case "slashGenerated":
                            slashReply?.TrySetResult(message);
                            break;
                        default:
                            M3Log.Warning($"Unknown message from Project Milan: {line}");
                            break;
                    }
                }
            }
            catch (Exception) when (closed)
            {
                // We closed it
            }
            catch (Exception e)
            {
                M3Log.Exception(e, "Lost connection to the Project Milan build:");
            }
            finally
            {
                slashReply?.TrySetException(new Exception("Lost connection to the Project Milan build"));
                Close();
            }
        }

        private void StartBuild(JsonNode message)
        {
            var moddescPath = (string)message["moddescPath"];
            var mod = moddescPath != null && File.Exists(moddescPath) ? new Mod(moddescPath, MEGame.Unknown) : null;
            if (mod is not { ValidMod: true, IsProjectMilan: true, Game: MEGame.LMP })
            {
                Finished(false, $"Not a valid Project Milan mod: {moddescPath}");
                return;
            }

            // Off = a normal install with the mod's slash manifest, which is verified against its final state
            bool generateSlash = message["generateSlash"]?.GetValue<bool>() ?? true;
            forwardLogs = true;
            M3Log.Information($"Project Milan build requested a base installation of {mod.ModName} {mod.ModVersionString}" + (generateSlash ? "" : " (using its slash manifest)"));
            App.Current.Dispatcher.Invoke(() => MilanBaseInstallerPanel.BuildBaseMilan(MainWindow.Instance, mod, this, (string)message["destination"], (string)message["le3Target"], generateSlash));
        }

        /// <summary>
        /// Asks Project Milan to generate the slash manifest, and waits for it. Returns once slash.json is in the mod's MilanDelta folder.
        /// </summary>
        public void GenerateSlash(MilanSlashRequest request)
        {
            slashReply = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            var node = JsonSerializer.SerializeToNode(request, JsonOptions)!.AsObject();
            node["type"] = "generateSlash";
            Send(node);
            var reply = slashReply.Task.GetAwaiter().GetResult();
            slashReply = null;
            if (reply["ok"]?.GetValue<bool>() != true)
            {
                throw new Exception($"Project Milan failed to generate the slash manifest: {(string)reply["error"]}");
            }
        }

        public void SetStatus(string text)
        {
            TrySend(new { type = "status", text });
        }

        /// <summary>
        /// Dialogs that came up during the build. A user would have seen them, so Project Milan treats any as a failure.
        /// </summary>
        private readonly List<string> dialogs = new();

        /// <summary>
        /// While a build is running, answers a dialog instead of showing it (it'd stall the build), picking the safe option:
        /// its default, else No/Cancel. Returns null when no build is running, so the dialog gets shown as normal.
        /// </summary>
        public System.Windows.MessageBoxResult? AnswerDialog(string caption, string message, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxResult defaultResult)
        {
            if (!forwardLogs)
                return null;

            lock (dialogs)
                dialogs.Add($"{caption}: {message}");
            M3Log.Error($"Dialog came up during the Project Milan build and was answered automatically: {caption}: {message}");
            if (defaultResult != System.Windows.MessageBoxResult.None)
                return defaultResult;
            return buttons switch
            {
                System.Windows.MessageBoxButton.YesNo => System.Windows.MessageBoxResult.No,
                System.Windows.MessageBoxButton.OK => System.Windows.MessageBoxResult.OK,
                _ => System.Windows.MessageBoxResult.Cancel
            };
        }

        /// <summary>
        /// Tells Project Milan how the build went. This ends the session.
        /// </summary>
        public void Finished(bool success, string error)
        {
            forwardLogs = false;
            List<string> shown;
            lock (dialogs)
                shown = dialogs.ToList();
            TrySend(new { type = "finished", success, error, dialogs = shown });
            Close();
        }

        private void Log(LogEvent logEvent)
        {
            if (forwardLogs && logEvent.Level >= LogEventLevel.Information)
            {
                TrySend(new { type = "log", level = logEvent.Level.ToString(), message = logEvent.RenderMessage() });
            }
        }

        private void Send(object message)
        {
            var node = message as JsonObject ?? JsonSerializer.SerializeToNode(message, JsonOptions)!.AsObject();
            node["v"] = ProtocolVersion;
            var line = node.ToJsonString();
            lock (writeLock)
            {
                writer.WriteLine(line);
            }
        }

        private void TrySend(object message)
        {
            if (closed)
                return;
            try
            {
                Send(message);
            }
            catch
            {
                // Don't log this, it would come straight back here. The read loop notices the pipe is gone.
            }
        }

        private void Close()
        {
            if (closed)
                return;
            closed = true;
            forwardLogs = false;
            if (Current == this)
                Current = null;
            try
            {
                pipe.Dispose();
            }
            catch
            {
                // Already gone
            }
        }

        /// <summary>
        /// Sends the log back to Project Milan while a build is running.
        /// </summary>
        public class LogSink : ILogEventSink
        {
            public void Emit(LogEvent logEvent) => Current?.Log(logEvent);
        }
    }
}
